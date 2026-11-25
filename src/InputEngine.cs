using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DmcKbmMod
{
    public class LiveInputTelemetry
    {
        public string Timestamp;
        public string PhysicalInput;
        public string ActionName;
        public string TargetKey;
        public string ScanCodeHex;
        public bool IsDown;
    }

    public class InputEngine : IDisposable
    {
        public ModConfig Config { get; private set; }
        public bool ModEnabled { get; private set; }
        public bool TestModeActive { get; set; }
        public bool BypassForegroundCheckForTests { get; set; }
        public GameProfile TestModeProfile { get; set; }
        public IntPtr AppWindowHandle { get; set; }
        public NativeInput.RECT TestCanvasScreenRect { get; set; }

        public GameProfile ActiveGame { get; private set; }
        public string ActiveProcessName { get; private set; }
        public bool ActiveGameIsElevatedMismatch { get; private set; }

        // Camera visualizer state for Live Tester (-1, 0, +1)
        public int CameraDirX { get; private set; }
        public int CameraDirY { get; private set; }
        public double CameraImpulseX { get; private set; }
        public double CameraImpulseY { get; private set; }

        public event Action StateChanged;
        public event Action<string, string> ToastRequested;
        public event Action<LiveInputTelemetry> TelemetryLogged;

        private IntPtr _kbHook = IntPtr.Zero;
        private IntPtr _mouseHook = IntPtr.Zero;
        private NativeInput.LowLevelHookProc _kbProc;
        private NativeInput.LowLevelHookProc _mouseProc;

        private readonly bool _isAdmin;
        private IntPtr _activeGameHwnd = IntPtr.Zero;
        private NativeInput.RECT _activeClientRect;
        private bool _hasValidClientRect = false;
        private bool _cursorClipped = false;

        // Fast lookup tables rebuilt whenever preset or game changes
        private Dictionary<InputCode, GameAction> _inputToAction = new Dictionary<InputCode, GameAction>();
        private Dictionary<string, GameProfile> _processLookup = new Dictionary<string, GameProfile>(StringComparer.OrdinalIgnoreCase);

        // Held state tracking (prevents stuck keys & preserves hold across dual bindings)
        private readonly Dictionary<InputCode, ushort> _activeSourceToTargetVk = new Dictionary<InputCode, ushort>();
        private readonly Dictionary<ushort, int> _heldTargetVkRefCount = new Dictionary<ushort, int>();

        // Timed pulse releases (e.g. MouseWheelUp / MouseWheelDown)
        private readonly Dictionary<ushort, long> _pulseReleaseAtMs = new Dictionary<ushort, long>();

        // Camera state
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _camHoldUntilX = 0;
        private long _camHoldUntilY = 0;
        private ushort _camHeldVkX = 0;
        private ushort _camHeldVkY = 0;

        // Bind capture mode
        private volatile bool _isCapturingBind = false;
        private bool _captureAllowMouse = true;
        private Action<InputCode, bool> _captureCallback;

        // Track hotkey debounce (prevent auto-repeat firing F1/F2/F3 rapidly)
        private readonly HashSet<InputCode> _heldHotkeys = new HashSet<InputCode>();

        private readonly object _stateLock = new object();

        public InputEngine(ModConfig config)
        {
            Config = config;
            ModEnabled = config.StartEnabled;
            _isAdmin = NativeInput.IsCurrentProcessAdmin();
            TestModeProfile = config.Games.Count > 0 ? config.Games[0] : null;
            RebuildLookupTables();
        }

        public void InstallHooks()
        {
            if (_kbHook != IntPtr.Zero || _mouseHook != IntPtr.Zero) return;

            _kbProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;

            IntPtr hMod;
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule mod = cur.MainModule)
            {
                hMod = NativeInput.GetModuleHandle(mod != null ? mod.ModuleName : null);
            }

            _kbHook = NativeInput.SetWindowsHookEx(NativeInput.WH_KEYBOARD_LL, _kbProc, hMod, 0);
            _mouseHook = NativeInput.SetWindowsHookEx(NativeInput.WH_MOUSE_LL, _mouseProc, hMod, 0);
        }

        public void UninstallHooks()
        {
            ReleaseAllHeldKeys();
            UnclipCursorIfNeeded();

            if (_kbHook != IntPtr.Zero)
            {
                NativeInput.UnhookWindowsHookEx(_kbHook);
                _kbHook = IntPtr.Zero;
            }
            if (_mouseHook != IntPtr.Zero)
            {
                NativeInput.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
        }

        public void RebuildLookupTables()
        {
            lock (_stateLock)
            {
                ReleaseAllHeldKeysLocked();

                var map = new Dictionary<InputCode, GameAction>();
                var preset = Config.GetPresetBindings(Config.ActivePreset);
                foreach (var kv in preset)
                {
                    if (kv.Value.Primary != InputCode.None && !map.ContainsKey(kv.Value.Primary))
                        map[kv.Value.Primary] = kv.Key;
                    if (kv.Value.Secondary != InputCode.None && !map.ContainsKey(kv.Value.Secondary))
                        map[kv.Value.Secondary] = kv.Key;
                }
                _inputToAction = map;

                var procMap = new Dictionary<string, GameProfile>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in Config.Games)
                {
                    foreach (var p in g.ProcessNames)
                    {
                        if (!string.IsNullOrWhiteSpace(p))
                            procMap[p.Trim()] = g;
                    }
                }
                _processLookup = procMap;
            }
        }

        public void SetModEnabled(bool enabled, bool showToast = false)
        {
            if (ModEnabled == enabled) return;
            ModEnabled = enabled;
            if (!ModEnabled)
            {
                ReleaseAllHeldKeys();
                UnclipCursorIfNeeded();
            }
            if (showToast && Config.ShowOverlayToast)
            {
                RaiseToast(
                    ModEnabled ? "DMC KBM MOD: ACTIVADO" : "DMC KBM MOD: PAUSADO",
                    ModEnabled ? GetPresetDisplayName(Config.ActivePreset) : "Pulsa " + ModConfig.GetInputLabel(Config.HotkeyToggleMod) + " para reactivar"
                );
            }
            RaiseStateChanged();
        }

        public void SetPreset(PresetType preset, bool showToast = false)
        {
            if (Config.ActivePreset == preset) return;
            Config.ActivePreset = preset;
            RebuildLookupTables();
            if (showToast && Config.ShowOverlayToast)
            {
                string detail = preset == PresetType.Dmc5Style
                    ? "LMB: Espada | RMB: Estilo/Buster | Lat4/MMB: Pistola"
                    : "LMB: Espada | RMB: Pistola | Lat4/MMB: Estilo/Buster";
                RaiseToast("PERFIL: " + GetPresetDisplayName(preset), detail);
            }
            RaiseStateChanged();
        }

        public void TogglePreset(bool showToast = true)
        {
            PresetType next = Config.ActivePreset == PresetType.Dmc5Style
                ? PresetType.ClassicStyle
                : PresetType.Dmc5Style;
            SetPreset(next, showToast);
        }

        public void ToggleMouseCamera(bool showToast = true)
        {
            Config.MouseCameraMasterEnabled = !Config.MouseCameraMasterEnabled;
            if (!Config.MouseCameraMasterEnabled)
            {
                ReleaseCameraKeys();
                UnclipCursorIfNeeded();
            }
            if (showToast && Config.ShowOverlayToast)
            {
                RaiseToast(
                    Config.MouseCameraMasterEnabled ? "CAMARA RATON: ON" : "CAMARA RATON: OFF",
                    Config.MouseCameraMasterEnabled ? "Movimiento de raton -> Camara" : "Solo botones del raton activos"
                );
            }
            RaiseStateChanged();
        }

        public static string GetPresetDisplayName(PresetType preset)
        {
            return preset == PresetType.Dmc5Style ? "Estilo DMC5" : "Estilo Clásico";
        }

        public void StartBindCapture(bool allowMouse, Action<InputCode, bool> callback)
        {
            ReleaseAllHeldKeys();
            UnclipCursorIfNeeded();
            _captureAllowMouse = allowMouse;
            _captureCallback = callback;
            _isCapturingBind = true;
        }

        public void CancelBindCapture()
        {
            _isCapturingBind = false;
            _captureCallback = null;
        }

        // Called from UI timer every ~150ms (NEVER inside the hook callback!)
        public void PollForegroundWindow()
        {
            IntPtr hwnd = NativeInput.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                SetActiveGame(null, null, IntPtr.Zero, false);
                return;
            }

            if (TestModeActive && AppWindowHandle != IntPtr.Zero && hwnd == AppWindowHandle)
            {
                // Keep TestMode responsive without triggering real game detection
                if (ActiveGame != null)
                {
                    SetActiveGame(null, null, IntPtr.Zero, false);
                }
                UnclipCursorIfNeeded();
                return;
            }

            uint pid;
            NativeInput.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0)
            {
                SetActiveGame(null, null, IntPtr.Zero, false);
                return;
            }

            string procName = null;
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    procName = p.ProcessName;
                }
            }
            catch
            {
                // Process exited or access denied
            }

            GameProfile matched = null;
            if (!string.IsNullOrEmpty(procName))
            {
                _processLookup.TryGetValue(procName, out matched);
            }

            bool elevMismatch = false;
            if (matched != null && !_isAdmin)
            {
                elevMismatch = NativeInput.IsProcessElevated(pid);
            }

            if (matched != null)
            {
                NativeInput.RECT rc;
                _hasValidClientRect = NativeInput.GetWindowClientScreenRect(hwnd, out rc);
                if (_hasValidClientRect) _activeClientRect = rc;
            }
            else
            {
                _hasValidClientRect = false;
            }

            SetActiveGame(matched, procName, matched != null ? hwnd : IntPtr.Zero, elevMismatch);
            UpdateCursorClip();
        }

        private void SetActiveGame(GameProfile profile, string procName, IntPtr hwnd, bool elevMismatch)
        {
            bool changed = !ReferenceEquals(ActiveGame, profile) ||
                           ActiveGameIsElevatedMismatch != elevMismatch ||
                           _activeGameHwnd != hwnd;

            if (ActiveGame != null && profile == null)
            {
                ReleaseAllHeldKeys();
                UnclipCursorIfNeeded();
            }

            ActiveGame = profile;
            ActiveProcessName = profile != null ? procName : null;
            _activeGameHwnd = hwnd;
            ActiveGameIsElevatedMismatch = elevMismatch;

            if (changed) RaiseStateChanged();
        }

        private void UpdateCursorClip()
        {
            bool shouldClip = ModEnabled &&
                              !_isCapturingBind &&
                              !TestModeActive &&
                              ActiveGame != null &&
                              Config.LockCursorToGame &&
                              Config.MouseCameraMasterEnabled &&
                              ActiveGame.EnableMouseCamera &&
                              _hasValidClientRect;

            if (shouldClip)
            {
                NativeInput.RECT clip = _activeClientRect;
                // Inset by 4px so cursor never hits window resize border
                if (clip.Width > 40 && clip.Height > 40)
                {
                    clip.Left += 4;
                    clip.Top += 4;
                    clip.Right -= 4;
                    clip.Bottom -= 4;
                }
                NativeInput.ClipCursor(ref clip);
                _cursorClipped = true;
            }
            else
            {
                UnclipCursorIfNeeded();
            }
        }

        private void UnclipCursorIfNeeded()
        {
            if (_cursorClipped)
            {
                NativeInput.ClipCursor(IntPtr.Zero);
                _cursorClipped = false;
            }
        }

        private bool IsTestModeEngaged()
        {
            if (!TestModeActive || !ModEnabled || _isCapturingBind) return false;
            if (BypassForegroundCheckForTests) return true;
            if (AppWindowHandle == IntPtr.Zero) return false;
            return NativeInput.GetForegroundWindow() == AppWindowHandle;
        }

        private GameProfile GetEffectiveProfile(out bool isSimulated)
        {
            if (ActiveGame != null && ModEnabled && !_isCapturingBind)
            {
                isSimulated = false;
                return ActiveGame;
            }
            if (IsTestModeEngaged())
            {
                isSimulated = true;
                return TestModeProfile ?? (Config.Games.Count > 0 ? Config.Games[0] : null);
            }
            isSimulated = false;
            return null;
        }

        // Called on WM_INPUT (Raw Mouse) from MainForm
        public void OnRawMouseDelta(int dx, int dy)
        {
            bool isSimulated;
            GameProfile prof = GetEffectiveProfile(out isSimulated);
            if (prof == null) return;
            if (!Config.MouseCameraMasterEnabled || !prof.EnableMouseCamera) return;

            int deadzone = Config.MouseDeadzone;
            long now = _clock.ElapsedMilliseconds;

            double effX = Math.Abs(dx) > deadzone ? dx * Config.MouseSensitivityX : 0;
            double effY = (!prof.HorizontalOnlyCamera && Math.Abs(dy) > deadzone) ? dy * Config.MouseSensitivityY : 0;

            if (Config.InvertCameraX) effX = -effX;
            if (Config.InvertCameraY) effY = -effY;

            lock (_stateLock)
            {
                if (Math.Abs(effX) >= 1.5)
                {
                    GameAction actX = effX > 0 ? GameAction.CameraRight : GameAction.CameraLeft;
                    ushort targetVk = GetGameTargetVk(prof, actX);
                    int holdMs = Config.CameraReleaseDelayMs + Math.Min(25, (int)(Math.Abs(effX) * 0.5));
                    _camHoldUntilX = now + holdMs;
                    CameraDirX = effX > 0 ? 1 : -1;
                    CameraImpulseX = Math.Max(-1.0, Math.Min(1.0, effX / 25.0));

                    if (_camHeldVkX != targetVk)
                    {
                        if (_camHeldVkX != 0 && !isSimulated) ReleaseTargetVkLocked(_camHeldVkX);
                        _camHeldVkX = targetVk;
                        if (_camHeldVkX != 0 && !isSimulated) PressTargetVkLocked(_camHeldVkX);
                        if (isSimulated) EmitTelemetry("Ratón " + (effX > 0 ? "→" : "←"), actX, _camHeldVkX, true);
                    }
                }

                if (Math.Abs(effY) >= 1.5)
                {
                    GameAction actY = effY > 0 ? GameAction.CameraDown : GameAction.CameraUp;
                    ushort targetVk = GetGameTargetVk(prof, actY);
                    int holdMs = Config.CameraReleaseDelayMs + Math.Min(25, (int)(Math.Abs(effY) * 0.5));
                    _camHoldUntilY = now + holdMs;
                    CameraDirY = effY > 0 ? 1 : -1;
                    CameraImpulseY = Math.Max(-1.0, Math.Min(1.0, effY / 25.0));

                    if (_camHeldVkY != targetVk)
                    {
                        if (_camHeldVkY != 0 && !isSimulated) ReleaseTargetVkLocked(_camHeldVkY);
                        _camHeldVkY = targetVk;
                        if (_camHeldVkY != 0 && !isSimulated) PressTargetVkLocked(_camHeldVkY);
                        if (isSimulated) EmitTelemetry("Ratón " + (effY > 0 ? "↓" : "↑"), actY, _camHeldVkY, true);
                    }
                }
            }
        }

        // Called every ~12ms from UI timer to release expired camera holds & wheel pulses
        public void UpdateTimers()
        {
            long now = _clock.ElapsedMilliseconds;
            bool isSimulated = IsTestModeEngaged() && ActiveGame == null;

            lock (_stateLock)
            {
                if (_camHeldVkX != 0 && now >= _camHoldUntilX)
                {
                    if (!isSimulated) ReleaseTargetVkLocked(_camHeldVkX);
                    _camHeldVkX = 0;
                    CameraDirX = 0;
                    CameraImpulseX = 0;
                }
                else if (_camHeldVkX == 0 && CameraDirX != 0 && now >= _camHoldUntilX)
                {
                    CameraDirX = 0;
                    CameraImpulseX = 0;
                }

                if (_camHeldVkY != 0 && now >= _camHoldUntilY)
                {
                    if (!isSimulated) ReleaseTargetVkLocked(_camHeldVkY);
                    _camHeldVkY = 0;
                    CameraDirY = 0;
                    CameraImpulseY = 0;
                }
                else if (_camHeldVkY == 0 && CameraDirY != 0 && now >= _camHoldUntilY)
                {
                    CameraDirY = 0;
                    CameraImpulseY = 0;
                }

                if (_pulseReleaseAtMs.Count > 0)
                {
                    List<ushort> expired = null;
                    foreach (var kv in _pulseReleaseAtMs)
                    {
                        if (now >= kv.Value)
                        {
                            if (expired == null) expired = new List<ushort>();
                            expired.Add(kv.Key);
                        }
                    }
                    if (expired != null)
                    {
                        foreach (ushort vk in expired)
                        {
                            _pulseReleaseAtMs.Remove(vk);
                            if (!isSimulated) ReleaseTargetVkLocked(vk);
                        }
                    }
                }
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info = (NativeInput.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeInput.KBDLLHOOKSTRUCT));

                // Never re-process our own injected SendInput events
                if (info.dwExtraInfo == NativeInput.INJECTED_SIGNATURE)
                    return NativeInput.CallNextHookEx(_kbHook, nCode, wParam, lParam);

                int msg = wParam.ToInt32();
                bool isDown = (msg == NativeInput.WM_KEYDOWN || msg == NativeInput.WM_SYSKEYDOWN);
                bool isUp = (msg == NativeInput.WM_KEYUP || msg == NativeInput.WM_SYSKEYUP);

                if (isDown || isUp)
                {
                    InputCode code = ModConfig.FromVirtualKey(info.vkCode);
                    if (code != InputCode.None)
                    {
                        // 1) Bind capture mode
                        if (_isCapturingBind)
                        {
                            if (isDown)
                            {
                                var cb = _captureCallback;
                                _isCapturingBind = false;
                                _captureCallback = null;
                                if (cb != null)
                                {
                                    if (code == InputCode.Escape)
                                        cb(InputCode.None, true); // cancelled
                                    else if (code == InputCode.Delete)
                                        cb(InputCode.None, false); // cleared
                                    else
                                        cb(code, false);
                                }
                            }
                            return (IntPtr)1;
                        }

                        // 2) Global hotkeys (active anytime a game or test mode is running, or always for F1/F2/F3 when focused on game)
                        if (TryHandleHotkey(code, isDown))
                            return (IntPtr)1;

                        // 3) Action remapping
                        bool isSimulated;
                        GameProfile prof = GetEffectiveProfile(out isSimulated);
                        if (prof != null)
                        {
                            bool swallow = HandleRemappedInput(prof, code, isDown, isSimulated);
                            if (swallow) return (IntPtr)1;
                        }
                    }
                }
            }
            return NativeInput.CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeInput.WM_MOUSEMOVE)
                    return NativeInput.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                var info = (NativeInput.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeInput.MSLLHOOKSTRUCT));
                if (info.dwExtraInfo == NativeInput.INJECTED_SIGNATURE)
                    return NativeInput.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                InputCode code = InputCode.None;
                bool isDown = false;
                bool isPulse = false;

                switch (msg)
                {
                    case NativeInput.WM_LBUTTONDOWN: code = InputCode.MouseLeft; isDown = true; break;
                    case NativeInput.WM_LBUTTONUP: code = InputCode.MouseLeft; isDown = false; break;
                    case NativeInput.WM_RBUTTONDOWN: code = InputCode.MouseRight; isDown = true; break;
                    case NativeInput.WM_RBUTTONUP: code = InputCode.MouseRight; isDown = false; break;
                    case NativeInput.WM_MBUTTONDOWN: code = InputCode.MouseMiddle; isDown = true; break;
                    case NativeInput.WM_MBUTTONUP: code = InputCode.MouseMiddle; isDown = false; break;
                    case NativeInput.WM_XBUTTONDOWN:
                        int xbDown = (int)((info.mouseData >> 16) & 0xFFFF);
                        code = xbDown == NativeInput.XBUTTON1 ? InputCode.MouseX1 : (xbDown == NativeInput.XBUTTON2 ? InputCode.MouseX2 : InputCode.None);
                        isDown = true;
                        break;
                    case NativeInput.WM_XBUTTONUP:
                        int xbUp = (int)((info.mouseData >> 16) & 0xFFFF);
                        code = xbUp == NativeInput.XBUTTON1 ? InputCode.MouseX1 : (xbUp == NativeInput.XBUTTON2 ? InputCode.MouseX2 : InputCode.None);
                        isDown = false;
                        break;
                    case NativeInput.WM_MOUSEWHEEL:
                        short delta = (short)((info.mouseData >> 16) & 0xFFFF);
                        if (delta > 0) { code = InputCode.MouseWheelUp; isDown = true; isPulse = true; }
                        else if (delta < 0) { code = InputCode.MouseWheelDown; isDown = true; isPulse = true; }
                        break;
                }

                if (code != InputCode.None)
                {
                    if (_isCapturingBind && _captureAllowMouse)
                    {
                        if (isDown)
                        {
                            var cb = _captureCallback;
                            _isCapturingBind = false;
                            _captureCallback = null;
                            if (cb != null) cb(code, false);
                        }
                        return (IntPtr)1;
                    }

                    bool isSimulated;
                    GameProfile prof = GetEffectiveProfile(out isSimulated);
                    if (prof != null)
                    {
                        // In Test Mode, only intercept mouse inputs when pointer is inside the Test Canvas
                        // so UI buttons (Preset switch, Stop Test Mode, tabs) remain clickable!
                        if (isSimulated && !IsPointInsideRect(info.pt, TestCanvasScreenRect))
                        {
                            return NativeInput.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                        }

                        if (isPulse)
                        {
                            bool swallow = HandlePulseInput(prof, code, isSimulated);
                            if (swallow) return (IntPtr)1;
                        }
                        else
                        {
                            bool swallow = HandleRemappedInput(prof, code, isDown, isSimulated);
                            if (swallow) return (IntPtr)1;
                        }
                    }
                }
            }
            return NativeInput.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private bool TryHandleHotkey(InputCode code, bool isDown)
        {
            // Only trigger global hotkeys while a supported DMC game or our app is foreground
            IntPtr fg = NativeInput.GetForegroundWindow();
            bool relevantWindow = ActiveGame != null || (AppWindowHandle != IntPtr.Zero && fg == AppWindowHandle);
            if (!relevantWindow) return false;

            bool isToggle = code == Config.HotkeyToggleMod;
            bool isSwitch = code == Config.HotkeySwitchPreset;
            bool isCam = code == Config.HotkeyToggleCamera;
            if (!isToggle && !isSwitch && !isCam) return false;

            if (isDown)
            {
                if (_heldHotkeys.Contains(code)) return true; // ignore key repeat
                _heldHotkeys.Add(code);

                if (isToggle) SetModEnabled(!ModEnabled, true);
                else if (isSwitch) TogglePreset(true);
                else if (isCam) ToggleMouseCamera(true);
            }
            else
            {
                _heldHotkeys.Remove(code);
            }
            return true;
        }

        private bool HandleRemappedInput(GameProfile prof, InputCode source, bool isDown, bool isSimulated)
        {
            GameAction action;
            ushort targetVk;

            lock (_stateLock)
            {
                if (!_inputToAction.TryGetValue(source, out action))
                    return false;

                targetVk = GetGameTargetVk(prof, action);
                if (targetVk == 0)
                    return false;

                // Optimization: if a keyboard key maps to the exact same key in-game (e.g. Q -> Q),
                // let the native key pass through untouched in real game mode (no duplicate SendInput).
                if (!isSimulated && ModConfig.IsKeyboardCode(source) && (ushort)source == targetVk)
                {
                    return false;
                }

                if (isDown)
                {
                    if (!_activeSourceToTargetVk.ContainsKey(source))
                    {
                        _activeSourceToTargetVk[source] = targetVk;
                        if (!isSimulated) PressTargetVkLocked(targetVk);
                        if (isSimulated) EmitTelemetry(ModConfig.GetInputLabel(source), action, targetVk, true);
                    }
                }
                else
                {
                    ushort prevVk;
                    if (_activeSourceToTargetVk.TryGetValue(source, out prevVk))
                    {
                        _activeSourceToTargetVk.Remove(source);
                        if (!isSimulated) ReleaseTargetVkLocked(prevVk);
                        if (isSimulated) EmitTelemetry(ModConfig.GetInputLabel(source), action, prevVk, false);
                    }
                    else
                    {
                        // Key-up for something pressed before activation: still swallow mouse up to avoid stray clicks
                        if (ModConfig.IsMouseCode(source)) return true;
                        return false;
                    }
                }
            }

            return true;
        }

        private bool HandlePulseInput(GameProfile prof, InputCode source, bool isSimulated)
        {
            lock (_stateLock)
            {
                GameAction action;
                if (!_inputToAction.TryGetValue(source, out action))
                    return false;

                ushort targetVk = GetGameTargetVk(prof, action);
                if (targetVk == 0)
                    return false;

                long releaseAt = _clock.ElapsedMilliseconds + Config.WheelPulseMs;
                if (!_pulseReleaseAtMs.ContainsKey(targetVk))
                {
                    if (!isSimulated) PressTargetVkLocked(targetVk);
                }
                _pulseReleaseAtMs[targetVk] = releaseAt;

                if (isSimulated)
                    EmitTelemetry(ModConfig.GetInputLabel(source), action, targetVk, true);
            }
            return true;
        }

        private static ushort GetGameTargetVk(GameProfile prof, GameAction action)
        {
            InputCode target;
            if (!prof.GameKeys.TryGetValue(action, out target)) return 0;
            if (!ModConfig.IsKeyboardCode(target)) return 0;
            return (ushort)target;
        }

        private void PressTargetVkLocked(ushort vk)
        {
            int count;
            _heldTargetVkRefCount.TryGetValue(vk, out count);
            _heldTargetVkRefCount[vk] = count + 1;
            if (count == 0)
            {
                NativeInput.SendScanKey(vk, true);
            }
        }

        private void ReleaseTargetVkLocked(ushort vk)
        {
            int count;
            if (!_heldTargetVkRefCount.TryGetValue(vk, out count) || count <= 0)
                return;

            count--;
            if (count <= 0)
            {
                _heldTargetVkRefCount.Remove(vk);
                NativeInput.SendScanKey(vk, false);
            }
            else
            {
                _heldTargetVkRefCount[vk] = count;
            }
        }

        private void ReleaseCameraKeys()
        {
            lock (_stateLock)
            {
                if (_camHeldVkX != 0)
                {
                    ReleaseTargetVkLocked(_camHeldVkX);
                    _camHeldVkX = 0;
                }
                if (_camHeldVkY != 0)
                {
                    ReleaseTargetVkLocked(_camHeldVkY);
                    _camHeldVkY = 0;
                }
                CameraDirX = 0;
                CameraDirY = 0;
                CameraImpulseX = 0;
                CameraImpulseY = 0;
            }
        }

        public void ReleaseAllHeldKeys()
        {
            lock (_stateLock)
            {
                ReleaseAllHeldKeysLocked();
            }
        }

        private void ReleaseAllHeldKeysLocked()
        {
            foreach (ushort vk in new List<ushort>(_heldTargetVkRefCount.Keys))
            {
                NativeInput.SendScanKey(vk, false);
            }
            _heldTargetVkRefCount.Clear();
            _activeSourceToTargetVk.Clear();
            _pulseReleaseAtMs.Clear();
            _camHeldVkX = 0;
            _camHeldVkY = 0;
            CameraDirX = 0;
            CameraDirY = 0;
            CameraImpulseX = 0;
            CameraImpulseY = 0;
        }

        private static bool IsPointInsideRect(NativeInput.POINT pt, NativeInput.RECT rc)
        {
            return pt.X >= rc.Left && pt.X <= rc.Right && pt.Y >= rc.Top && pt.Y <= rc.Bottom;
        }

        private void EmitTelemetry(string physicalLabel, GameAction action, ushort targetVk, bool isDown)
        {
            var handler = TelemetryLogged;
            if (handler == null) return;

            bool ext;
            ushort scan = NativeInput.GetScanCode(targetVk, out ext);
            InputCode targetCode = ModConfig.FromVirtualKey(targetVk);
            handler(new LiveInputTelemetry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                PhysicalInput = physicalLabel,
                ActionName = ModConfig.GetActionLabel(action),
                TargetKey = ModConfig.GetInputLabel(targetCode),
                ScanCodeHex = string.Format("0x{0:X2}{1}", scan, ext ? " (EXT)" : ""),
                IsDown = isDown
            });
        }

        private void RaiseStateChanged()
        {
            var handler = StateChanged;
            if (handler != null) handler();
        }

        private void RaiseToast(string title, string subtitle)
        {
            var handler = ToastRequested;
            if (handler != null) handler(title, subtitle);
        }

        public void Dispose()
        {
            UninstallHooks();
        }
    }
}
