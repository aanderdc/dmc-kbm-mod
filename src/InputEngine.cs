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

        // Held state tracking (prevents stuck keys/buttons & preserves hold across dual bindings)
        private readonly Dictionary<InputCode, InputCode> _activeSourceToTarget = new Dictionary<InputCode, InputCode>();
        private readonly Dictionary<InputCode, int> _heldTargetRefCount = new Dictionary<InputCode, int>();

        // Timed pulse releases (e.g. MouseWheelUp / MouseWheelDown)
        private readonly Dictionary<InputCode, long> _pulseReleaseAtMs = new Dictionary<InputCode, long>();

        // Camera state
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _camHoldUntilX = 0;
        private long _camHoldUntilY = 0;
        private InputCode _camHeldX = InputCode.None;
        private InputCode _camHeldY = InputCode.None;

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
                    ModEnabled
                        ? Localization.T("DMC KBM MOD: ENABLED", "DMC KBM MOD: ACTIVADO")
                        : Localization.T("DMC KBM MOD: PAUSED", "DMC KBM MOD: PAUSADO"),
                    ModEnabled
                        ? GetPresetDisplayName(Config.ActivePreset)
                        : Localization.T("Press ", "Pulsa ") + ModConfig.GetInputLabel(Config.HotkeyToggleMod) + Localization.T(" to resume", " para reactivar")
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
                    ? Localization.T("LMB: Sword | RMB: Style/Buster | Side4/MMB: Gun", "LMB: Espada | RMB: Estilo/Buster | Lat4/MMB: Pistola")
                    : Localization.T("LMB: Sword | RMB: Gun | Side4/MMB: Style/Buster", "LMB: Espada | RMB: Pistola | Lat4/MMB: Estilo/Buster");
                RaiseToast(Localization.T("PRESET: ", "PERFIL: ") + GetPresetDisplayName(preset), detail);
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
                    Config.MouseCameraMasterEnabled
                        ? Localization.T("MOUSE CAMERA: ON", "CAMARA RATON: ON")
                        : Localization.T("MOUSE CAMERA: OFF", "CAMARA RATON: OFF"),
                    Config.MouseCameraMasterEnabled
                        ? Localization.T("Mouse movement -> Camera", "Movimiento de raton -> Camara")
                        : Localization.T("Mouse buttons still active", "Solo botones del raton activos")
                );
            }
            RaiseStateChanged();
        }

        public static string GetPresetDisplayName(PresetType preset)
        {
            return Localization.PresetName(preset);
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
                    InputCode target = GetGameTargetInput(prof, actX);
                    int holdMs = Config.CameraReleaseDelayMs + Math.Min(25, (int)(Math.Abs(effX) * 0.5));
                    _camHoldUntilX = now + holdMs;
                    CameraDirX = effX > 0 ? 1 : -1;
                    CameraImpulseX = Math.Max(-1.0, Math.Min(1.0, effX / 25.0));

                    if (_camHeldX != target)
                    {
                        if (_camHeldX != InputCode.None && !isSimulated) ReleaseTargetInputLocked(_camHeldX);
                        _camHeldX = target;
                        if (_camHeldX != InputCode.None && !isSimulated) PressTargetInputLocked(_camHeldX);
                        if (isSimulated) EmitTelemetry(Localization.T("Mouse ", "Ratón ") + (effX > 0 ? "→" : "←"), actX, _camHeldX, true);
                    }
                }

                if (Math.Abs(effY) >= 1.5)
                {
                    GameAction actY = effY > 0 ? GameAction.CameraDown : GameAction.CameraUp;
                    InputCode target = GetGameTargetInput(prof, actY);
                    int holdMs = Config.CameraReleaseDelayMs + Math.Min(25, (int)(Math.Abs(effY) * 0.5));
                    _camHoldUntilY = now + holdMs;
                    CameraDirY = effY > 0 ? 1 : -1;
                    CameraImpulseY = Math.Max(-1.0, Math.Min(1.0, effY / 25.0));

                    if (_camHeldY != target)
                    {
                        if (_camHeldY != InputCode.None && !isSimulated) ReleaseTargetInputLocked(_camHeldY);
                        _camHeldY = target;
                        if (_camHeldY != InputCode.None && !isSimulated) PressTargetInputLocked(_camHeldY);
                        if (isSimulated) EmitTelemetry(Localization.T("Mouse ", "Ratón ") + (effY > 0 ? "↓" : "↑"), actY, _camHeldY, true);
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
                if (_camHeldX != InputCode.None && now >= _camHoldUntilX)
                {
                    if (!isSimulated) ReleaseTargetInputLocked(_camHeldX);
                    _camHeldX = InputCode.None;
                    CameraDirX = 0;
                    CameraImpulseX = 0;
                }
                else if (_camHeldX == InputCode.None && CameraDirX != 0 && now >= _camHoldUntilX)
                {
                    CameraDirX = 0;
                    CameraImpulseX = 0;
                }

                if (_camHeldY != InputCode.None && now >= _camHoldUntilY)
                {
                    if (!isSimulated) ReleaseTargetInputLocked(_camHeldY);
                    _camHeldY = InputCode.None;
                    CameraDirY = 0;
                    CameraImpulseY = 0;
                }
                else if (_camHeldY == InputCode.None && CameraDirY != 0 && now >= _camHoldUntilY)
                {
                    CameraDirY = 0;
                    CameraImpulseY = 0;
                }

                if (_pulseReleaseAtMs.Count > 0)
                {
                    List<InputCode> expired = null;
                    foreach (var kv in _pulseReleaseAtMs)
                    {
                        if (now >= kv.Value)
                        {
                            if (expired == null) expired = new List<InputCode>();
                            expired.Add(kv.Key);
                        }
                    }
                    if (expired != null)
                    {
                        foreach (InputCode target in expired)
                        {
                            _pulseReleaseAtMs.Remove(target);
                            if (!isSimulated) ReleaseTargetInputLocked(target);
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
            InputCode target;

            lock (_stateLock)
            {
                if (!_inputToAction.TryGetValue(source, out action))
                    return false;

                target = GetGameTargetInput(prof, action);
                if (target == InputCode.None)
                    return false;

                // Optimization: if the physical input equals the exact target input in-game
                // (e.g., Q -> Q, Space -> Space, LMB -> LMB in DmC), let it pass natively untouched.
                if (!isSimulated && source == target)
                {
                    return false;
                }

                if (isDown)
                {
                    if (!_activeSourceToTarget.ContainsKey(source))
                    {
                        _activeSourceToTarget[source] = target;
                        if (!isSimulated) PressTargetInputLocked(target);
                        if (isSimulated) EmitTelemetry(ModConfig.GetInputLabel(source), action, target, true);
                    }
                }
                else
                {
                    InputCode prevTarget;
                    if (_activeSourceToTarget.TryGetValue(source, out prevTarget))
                    {
                        _activeSourceToTarget.Remove(source);
                        if (!isSimulated) ReleaseTargetInputLocked(prevTarget);
                        if (isSimulated) EmitTelemetry(ModConfig.GetInputLabel(source), action, prevTarget, false);
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

                InputCode target = GetGameTargetInput(prof, action);
                if (target == InputCode.None)
                    return false;

                long releaseAt = _clock.ElapsedMilliseconds + Config.WheelPulseMs;
                if (!_pulseReleaseAtMs.ContainsKey(target))
                {
                    if (!isSimulated) PressTargetInputLocked(target);
                }
                _pulseReleaseAtMs[target] = releaseAt;

                if (isSimulated)
                    EmitTelemetry(ModConfig.GetInputLabel(source), action, target, true);
            }
            return true;
        }

        private static InputCode GetGameTargetInput(GameProfile prof, GameAction action)
        {
            InputCode target;
            if (!prof.GameKeys.TryGetValue(action, out target)) return InputCode.None;
            if (ModConfig.IsKeyboardCode(target) || ModConfig.IsMouseCode(target))
                return target;
            return InputCode.None;
        }

        private void PressTargetInputLocked(InputCode target)
        {
            int count;
            _heldTargetRefCount.TryGetValue(target, out count);
            _heldTargetRefCount[target] = count + 1;
            if (count == 0)
            {
                NativeInput.SendTargetInput(target, true);
            }
        }

        private void ReleaseTargetInputLocked(InputCode target)
        {
            int count;
            if (!_heldTargetRefCount.TryGetValue(target, out count) || count <= 0)
                return;

            count--;
            if (count <= 0)
            {
                _heldTargetRefCount.Remove(target);
                NativeInput.SendTargetInput(target, false);
            }
            else
            {
                _heldTargetRefCount[target] = count;
            }
        }

        private void ReleaseCameraKeys()
        {
            lock (_stateLock)
            {
                if (_camHeldX != InputCode.None)
                {
                    ReleaseTargetInputLocked(_camHeldX);
                    _camHeldX = InputCode.None;
                }
                if (_camHeldY != InputCode.None)
                {
                    ReleaseTargetInputLocked(_camHeldY);
                    _camHeldY = InputCode.None;
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
            foreach (InputCode target in new List<InputCode>(_heldTargetRefCount.Keys))
            {
                NativeInput.SendTargetInput(target, false);
            }
            _heldTargetRefCount.Clear();
            _activeSourceToTarget.Clear();
            _pulseReleaseAtMs.Clear();
            _camHeldX = InputCode.None;
            _camHeldY = InputCode.None;
            CameraDirX = 0;
            CameraDirY = 0;
            CameraImpulseX = 0;
            CameraImpulseY = 0;
        }

        private static bool IsPointInsideRect(NativeInput.POINT pt, NativeInput.RECT rc)
        {
            return pt.X >= rc.Left && pt.X <= rc.Right && pt.Y >= rc.Top && pt.Y <= rc.Bottom;
        }

        private void EmitTelemetry(string physicalLabel, GameAction action, InputCode targetCode, bool isDown)
        {
            var handler = TelemetryLogged;
            if (handler == null) return;

            string scanStr;
            if (ModConfig.IsKeyboardCode(targetCode))
            {
                bool ext;
                ushort scan = NativeInput.GetScanCode((ushort)targetCode, out ext);
                scanStr = string.Format("0x{0:X2}{1}", scan, ext ? " (EXT)" : "");
            }
            else
            {
                scanStr = "MOUSE";
            }

            handler(new LiveInputTelemetry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                PhysicalInput = physicalLabel,
                ActionName = ModConfig.GetActionLabel(action),
                TargetKey = ModConfig.GetInputLabel(targetCode),
                ScanCodeHex = scanStr,
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
