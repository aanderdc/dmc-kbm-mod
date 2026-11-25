using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DmcKbmMod
{
    public enum PresetType
    {
        Dmc5Style = 0,
        ClassicStyle = 1
    }

    public enum GameAction
    {
        Melee,
        Gun,
        StyleOrBuster,
        Jump,
        LockOn,
        DevilTrigger,
        ChangeGunOrExceed,
        ChangeSword,
        CameraReset,
        ChangeTarget,
        Taunt,
        CameraUp,
        CameraDown,
        CameraLeft,
        CameraRight
    }

    public enum InputCode
    {
        None = 0,

        // Mouse virtual codes (custom negative/high range outside Win32 VK 0..255)
        MouseLeft = 1001,
        MouseRight = 1002,
        MouseMiddle = 1003,
        MouseX1 = 1004,
        MouseX2 = 1005,
        MouseWheelUp = 1006,
        MouseWheelDown = 1007,

        // Keyboard virtual keys (Win32 VK codes)
        Backspace = 0x08,
        Tab = 0x09,
        Enter = 0x0D,
        Escape = 0x1B,
        Space = 0x20,
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Insert = 0x2D,
        Delete = 0x2E,

        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,

        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46, G = 0x47,
        H = 0x48, I = 0x49, J = 0x4A, K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E,
        O = 0x4F, P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54, U = 0x55,
        V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,

        Num0 = 0x60, Num1 = 0x61, Num2 = 0x62, Num3 = 0x63, Num4 = 0x64,
        Num5 = 0x65, Num6 = 0x66, Num7 = 0x67, Num8 = 0x68, Num9 = 0x69,

        F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75,
        F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,

        LShift = 0xA0,
        RShift = 0xA1,
        LCtrl = 0xA2,
        RCtrl = 0xA3,
        LAlt = 0xA4,
        RAlt = 0xA5
    }

    public class ActionBinding
    {
        public InputCode Primary { get; set; }
        public InputCode Secondary { get; set; }

        public ActionBinding(InputCode primary = InputCode.None, InputCode secondary = InputCode.None)
        {
            Primary = primary;
            Secondary = secondary;
        }
    }

    public class GameProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public List<string> ProcessNames { get; set; }
        public bool EnableMouseCamera { get; set; }
        public bool HorizontalOnlyCamera { get; set; }
        public Dictionary<GameAction, InputCode> GameKeys { get; set; }

        public GameProfile()
        {
            ProcessNames = new List<string>();
            GameKeys = new Dictionary<GameAction, InputCode>();
            EnableMouseCamera = true;
            HorizontalOnlyCamera = false;
        }
    }

    public class ModConfig
    {
        public string Language { get; set; }
        public PresetType ActivePreset { get; set; }
        public bool StartEnabled { get; set; }
        public bool ShowOverlayToast { get; set; }
        public bool LockCursorToGame { get; set; }
        public bool MouseCameraMasterEnabled { get; set; }

        public double MouseSensitivityX { get; set; }
        public double MouseSensitivityY { get; set; }
        public int MouseDeadzone { get; set; }
        public int CameraReleaseDelayMs { get; set; }
        public int WheelPulseMs { get; set; }
        public bool InvertCameraX { get; set; }
        public bool InvertCameraY { get; set; }

        public InputCode HotkeyToggleMod { get; set; }
        public InputCode HotkeySwitchPreset { get; set; }
        public InputCode HotkeyToggleCamera { get; set; }

        public Dictionary<GameAction, ActionBinding> Dmc5Preset { get; set; }
        public Dictionary<GameAction, ActionBinding> ClassicPreset { get; set; }
        public List<GameProfile> Games { get; set; }

        public ModConfig()
        {
            Language = "auto";
            ActivePreset = PresetType.Dmc5Style;
            StartEnabled = true;
            ShowOverlayToast = true;
            LockCursorToGame = true;
            MouseCameraMasterEnabled = true;

            MouseSensitivityX = 1.0;
            MouseSensitivityY = 0.85;
            MouseDeadzone = 2;
            CameraReleaseDelayMs = 45;
            WheelPulseMs = 45;
            InvertCameraX = false;
            InvertCameraY = false;

            HotkeyToggleMod = InputCode.F1;
            HotkeySwitchPreset = InputCode.F2;
            HotkeyToggleCamera = InputCode.F3;

            Dmc5Preset = CreateDefaultDmc5Preset();
            ClassicPreset = CreateDefaultClassicPreset();
            Games = CreateDefaultGames();
        }

        public Dictionary<GameAction, ActionBinding> GetPresetBindings(PresetType preset)
        {
            return preset == PresetType.Dmc5Style ? Dmc5Preset : ClassicPreset;
        }

        public static Dictionary<GameAction, ActionBinding> CreateDefaultDmc5Preset()
        {
            // Inspired by DMC5 PC native KBM layout:
            // LMB = Melee, RMB = Style/Buster, MMB / Mouse4 / E = Gun, Space = Jump, LShift = Lock-On
            return new Dictionary<GameAction, ActionBinding>
            {
                { GameAction.Melee, new ActionBinding(InputCode.MouseLeft, InputCode.None) },
                { GameAction.StyleOrBuster, new ActionBinding(InputCode.MouseRight, InputCode.None) },
                { GameAction.Gun, new ActionBinding(InputCode.MouseX1, InputCode.MouseMiddle) },
                { GameAction.Jump, new ActionBinding(InputCode.Space, InputCode.None) },
                { GameAction.LockOn, new ActionBinding(InputCode.LShift, InputCode.None) },
                { GameAction.DevilTrigger, new ActionBinding(InputCode.F, InputCode.None) },
                { GameAction.ChangeGunOrExceed, new ActionBinding(InputCode.Q, InputCode.MouseWheelUp) },
                { GameAction.ChangeSword, new ActionBinding(InputCode.E, InputCode.MouseWheelDown) },
                { GameAction.CameraReset, new ActionBinding(InputCode.MouseX2, InputCode.R) },
                { GameAction.ChangeTarget, new ActionBinding(InputCode.Tab, InputCode.C) },
                { GameAction.Taunt, new ActionBinding(InputCode.V, InputCode.None) }
            };
        }

        public static Dictionary<GameAction, ActionBinding> CreateDefaultClassicPreset()
        {
            // Classic Action KBM layout:
            // LMB = Melee, RMB = Gun, Mouse4 / MMB = Style/Buster, Space = Jump, LShift = Lock-On
            return new Dictionary<GameAction, ActionBinding>
            {
                { GameAction.Melee, new ActionBinding(InputCode.MouseLeft, InputCode.None) },
                { GameAction.Gun, new ActionBinding(InputCode.MouseRight, InputCode.None) },
                { GameAction.StyleOrBuster, new ActionBinding(InputCode.MouseX1, InputCode.MouseMiddle) },
                { GameAction.Jump, new ActionBinding(InputCode.Space, InputCode.None) },
                { GameAction.LockOn, new ActionBinding(InputCode.LShift, InputCode.None) },
                { GameAction.DevilTrigger, new ActionBinding(InputCode.F, InputCode.None) },
                { GameAction.ChangeGunOrExceed, new ActionBinding(InputCode.Q, InputCode.MouseWheelUp) },
                { GameAction.ChangeSword, new ActionBinding(InputCode.E, InputCode.MouseWheelDown) },
                { GameAction.CameraReset, new ActionBinding(InputCode.MouseX2, InputCode.R) },
                { GameAction.ChangeTarget, new ActionBinding(InputCode.Tab, InputCode.C) },
                { GameAction.Taunt, new ActionBinding(InputCode.V, InputCode.None) }
            };
        }

        public static List<GameProfile> CreateDefaultGames()
        {
            // Default PC keyboard bindings for each Capcom release:
            var dmc4 = new GameProfile
            {
                Id = "DMC4",
                DisplayName = "Devil May Cry 4 / Special Edition",
                ProcessNames = new List<string> { "DevilMayCry4SpecialEdition", "DevilMayCry4_DX9", "DevilMayCry4_DX10" },
                EnableMouseCamera = true,
                HorizontalOnlyCamera = false,
                GameKeys = new Dictionary<GameAction, InputCode>
                {
                    { GameAction.Melee, InputCode.I },
                    { GameAction.Gun, InputCode.J },
                    { GameAction.Jump, InputCode.K },
                    { GameAction.StyleOrBuster, InputCode.L },
                    { GameAction.LockOn, InputCode.Space },
                    { GameAction.DevilTrigger, InputCode.N },
                    { GameAction.ChangeGunOrExceed, InputCode.Q },
                    { GameAction.ChangeSword, InputCode.E },
                    { GameAction.CameraReset, InputCode.P },
                    { GameAction.ChangeTarget, InputCode.O },
                    { GameAction.Taunt, InputCode.M },
                    { GameAction.CameraUp, InputCode.Up },
                    { GameAction.CameraDown, InputCode.Down },
                    { GameAction.CameraLeft, InputCode.Left },
                    { GameAction.CameraRight, InputCode.Right }
                }
            };

            var dmc3 = new GameProfile
            {
                Id = "DMC3",
                DisplayName = "DMC HD Collection - DMC3",
                ProcessNames = new List<string> { "dmc3" },
                EnableMouseCamera = true,
                HorizontalOnlyCamera = true,
                GameKeys = new Dictionary<GameAction, InputCode>
                {
                    { GameAction.Melee, InputCode.I },
                    { GameAction.Gun, InputCode.J },
                    { GameAction.Jump, InputCode.K },
                    { GameAction.StyleOrBuster, InputCode.L },
                    { GameAction.LockOn, InputCode.Space },
                    { GameAction.DevilTrigger, InputCode.F },
                    { GameAction.ChangeGunOrExceed, InputCode.Q },
                    { GameAction.ChangeSword, InputCode.E },
                    { GameAction.CameraReset, InputCode.C },
                    { GameAction.ChangeTarget, InputCode.O },
                    { GameAction.Taunt, InputCode.M },
                    { GameAction.CameraUp, InputCode.Up },
                    { GameAction.CameraDown, InputCode.Down },
                    { GameAction.CameraLeft, InputCode.Left },
                    { GameAction.CameraRight, InputCode.Right }
                }
            };

            var dmc1 = new GameProfile
            {
                Id = "DMC1",
                DisplayName = "DMC HD Collection - DMC1",
                ProcessNames = new List<string> { "dmc1" },
                EnableMouseCamera = false,
                HorizontalOnlyCamera = true,
                GameKeys = new Dictionary<GameAction, InputCode>
                {
                    { GameAction.Melee, InputCode.I },
                    { GameAction.Gun, InputCode.J },
                    { GameAction.Jump, InputCode.K },
                    { GameAction.StyleOrBuster, InputCode.L },
                    { GameAction.LockOn, InputCode.Space },
                    { GameAction.DevilTrigger, InputCode.F },
                    { GameAction.ChangeGunOrExceed, InputCode.Q },
                    { GameAction.ChangeSword, InputCode.E },
                    { GameAction.CameraReset, InputCode.C },
                    { GameAction.ChangeTarget, InputCode.O },
                    { GameAction.Taunt, InputCode.M },
                    { GameAction.CameraUp, InputCode.Up },
                    { GameAction.CameraDown, InputCode.Down },
                    { GameAction.CameraLeft, InputCode.Left },
                    { GameAction.CameraRight, InputCode.Right }
                }
            };

            var dmc2 = new GameProfile
            {
                Id = "DMC2",
                DisplayName = "DMC HD Collection - DMC2",
                ProcessNames = new List<string> { "dmc2" },
                EnableMouseCamera = false,
                HorizontalOnlyCamera = true,
                GameKeys = new Dictionary<GameAction, InputCode>
                {
                    { GameAction.Melee, InputCode.I },
                    { GameAction.Gun, InputCode.J },
                    { GameAction.Jump, InputCode.K },
                    { GameAction.StyleOrBuster, InputCode.L },
                    { GameAction.LockOn, InputCode.Space },
                    { GameAction.DevilTrigger, InputCode.F },
                    { GameAction.ChangeGunOrExceed, InputCode.Q },
                    { GameAction.ChangeSword, InputCode.E },
                    { GameAction.CameraReset, InputCode.C },
                    { GameAction.ChangeTarget, InputCode.O },
                    { GameAction.Taunt, InputCode.M },
                    { GameAction.CameraUp, InputCode.Up },
                    { GameAction.CameraDown, InputCode.Down },
                    { GameAction.CameraLeft, InputCode.Left },
                    { GameAction.CameraRight, InputCode.Right }
                }
            };

            var dmcReboot = new GameProfile
            {
                Id = "DmC",
                DisplayName = "DmC: Devil May Cry (2013)",
                ProcessNames = new List<string> { "DMC-DevilMayCry" },
                EnableMouseCamera = false, // DmC (2013) has native PC mouse camera
                HorizontalOnlyCamera = false,
                GameKeys = new Dictionary<GameAction, InputCode>
                {
                    { GameAction.Melee, InputCode.MouseLeft },
                    { GameAction.Gun, InputCode.MouseRight },
                    { GameAction.StyleOrBuster, InputCode.F },          // Special / Launch / Pull
                    { GameAction.Jump, InputCode.Space },
                    { GameAction.LockOn, InputCode.LShift },            // Evade / Dodge in DmC
                    { GameAction.DevilTrigger, InputCode.V },
                    { GameAction.ChangeGunOrExceed, InputCode.Q },      // Angel Mode
                    { GameAction.ChangeSword, InputCode.E },            // Demon Mode
                    { GameAction.CameraReset, InputCode.C },
                    { GameAction.ChangeTarget, InputCode.Tab },
                    { GameAction.Taunt, InputCode.T },
                    { GameAction.CameraUp, InputCode.Up },
                    { GameAction.CameraDown, InputCode.Down },
                    { GameAction.CameraLeft, InputCode.Left },
                    { GameAction.CameraRight, InputCode.Right }
                }
            };

            return new List<GameProfile> { dmc4, dmc3, dmc1, dmc2, dmcReboot };
        }

        public static string GetActionLabel(GameAction action)
        {
            return Localization.ActionLabel(action);
        }

        public static string GetInputLabel(InputCode code)
        {
            return Localization.InputLabel(code);
        }

        public static bool IsMouseCode(InputCode code)
        {
            int v = (int)code;
            return v >= 1001 && v <= 1007;
        }

        public static bool IsKeyboardCode(InputCode code)
        {
            int v = (int)code;
            return v > 0 && v <= 255;
        }

        public static InputCode FromVirtualKey(uint vk)
        {
            // Normalize generic Shift/Ctrl/Alt to left variant if needed
            if (vk == 0x10) vk = 0xA0;
            if (vk == 0x11) vk = 0xA2;
            if (vk == 0x12) vk = 0xA4;
            if (Enum.IsDefined(typeof(InputCode), (int)vk))
                return (InputCode)(int)vk;
            return InputCode.None;
        }

        public static readonly GameAction[] EditablePlayerActions = new[]
        {
            GameAction.Melee,
            GameAction.StyleOrBuster,
            GameAction.Gun,
            GameAction.Jump,
            GameAction.LockOn,
            GameAction.DevilTrigger,
            GameAction.ChangeGunOrExceed,
            GameAction.ChangeSword,
            GameAction.CameraReset,
            GameAction.ChangeTarget,
            GameAction.Taunt
        };

        public static readonly GameAction[] AllGameKeyActions = new[]
        {
            GameAction.Melee,
            GameAction.Gun,
            GameAction.Jump,
            GameAction.StyleOrBuster,
            GameAction.LockOn,
            GameAction.DevilTrigger,
            GameAction.ChangeGunOrExceed,
            GameAction.ChangeSword,
            GameAction.CameraReset,
            GameAction.ChangeTarget,
            GameAction.Taunt,
            GameAction.CameraUp,
            GameAction.CameraDown,
            GameAction.CameraLeft,
            GameAction.CameraRight
        };

        public void Save(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; DMC KBM Mod - Configuration file / Archivo de configuracion");
            sb.AppendLine("; Edit manually or via the app UI. Language: auto, en, es");
            sb.AppendLine();

            sb.AppendLine("[General]");
            sb.AppendLine("Language=" + (string.IsNullOrEmpty(Language) ? "auto" : Language));
            sb.AppendLine("ActivePreset=" + ActivePreset);
            sb.AppendLine("StartEnabled=" + StartEnabled);
            sb.AppendLine("ShowOverlayToast=" + ShowOverlayToast);
            sb.AppendLine("LockCursorToGame=" + LockCursorToGame);
            sb.AppendLine("MouseCameraMasterEnabled=" + MouseCameraMasterEnabled);
            sb.AppendLine("HotkeyToggleMod=" + HotkeyToggleMod);
            sb.AppendLine("HotkeySwitchPreset=" + HotkeySwitchPreset);
            sb.AppendLine("HotkeyToggleCamera=" + HotkeyToggleCamera);
            sb.AppendLine();

            sb.AppendLine("[Camera]");
            sb.AppendLine("MouseSensitivityX=" + MouseSensitivityX.ToString("0.00", CultureInfo.InvariantCulture));
            sb.AppendLine("MouseSensitivityY=" + MouseSensitivityY.ToString("0.00", CultureInfo.InvariantCulture));
            sb.AppendLine("MouseDeadzone=" + MouseDeadzone);
            sb.AppendLine("CameraReleaseDelayMs=" + CameraReleaseDelayMs);
            sb.AppendLine("WheelPulseMs=" + WheelPulseMs);
            sb.AppendLine("InvertCameraX=" + InvertCameraX);
            sb.AppendLine("InvertCameraY=" + InvertCameraY);
            sb.AppendLine();

            WritePresetSection(sb, "Preset.Dmc5Style", Dmc5Preset);
            WritePresetSection(sb, "Preset.ClassicStyle", ClassicPreset);

            foreach (var game in Games)
            {
                sb.AppendLine("[Game." + game.Id + "]");
                sb.AppendLine("DisplayName=" + game.DisplayName);
                sb.AppendLine("ProcessNames=" + string.Join(",", game.ProcessNames.ToArray()));
                sb.AppendLine("EnableMouseCamera=" + game.EnableMouseCamera);
                sb.AppendLine("HorizontalOnlyCamera=" + game.HorizontalOnlyCamera);
                foreach (var action in AllGameKeyActions)
                {
                    InputCode code;
                    if (!game.GameKeys.TryGetValue(action, out code)) code = InputCode.None;
                    sb.AppendLine("Key." + action + "=" + code);
                }
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void WritePresetSection(StringBuilder sb, string sectionName, Dictionary<GameAction, ActionBinding> preset)
        {
            sb.AppendLine("[" + sectionName + "]");
            foreach (var action in EditablePlayerActions)
            {
                ActionBinding bind;
                if (!preset.TryGetValue(action, out bind)) bind = new ActionBinding();
                sb.AppendLine(action + "=" + bind.Primary + "," + bind.Secondary);
            }
            sb.AppendLine();
        }

        public static ModConfig LoadOrCreate(string path)
        {
            var cfg = new ModConfig();
            if (!File.Exists(path))
            {
                try { cfg.Save(path); } catch { }
                return cfg;
            }

            try
            {
                string currentSection = "";
                var gamesById = new Dictionary<string, GameProfile>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in cfg.Games) gamesById[g.Id] = g;

                foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (currentSection.Equals("General", StringComparison.OrdinalIgnoreCase))
                    {
                        if (key == "Language") cfg.Language = val;
                        else if (key == "ActivePreset") cfg.ActivePreset = ParseEnum(val, cfg.ActivePreset);
                        else if (key == "StartEnabled") cfg.StartEnabled = ParseBool(val, cfg.StartEnabled);
                        else if (key == "ShowOverlayToast") cfg.ShowOverlayToast = ParseBool(val, cfg.ShowOverlayToast);
                        else if (key == "LockCursorToGame") cfg.LockCursorToGame = ParseBool(val, cfg.LockCursorToGame);
                        else if (key == "MouseCameraMasterEnabled") cfg.MouseCameraMasterEnabled = ParseBool(val, cfg.MouseCameraMasterEnabled);
                        else if (key == "HotkeyToggleMod") cfg.HotkeyToggleMod = ParseEnum(val, cfg.HotkeyToggleMod);
                        else if (key == "HotkeySwitchPreset") cfg.HotkeySwitchPreset = ParseEnum(val, cfg.HotkeySwitchPreset);
                        else if (key == "HotkeyToggleCamera") cfg.HotkeyToggleCamera = ParseEnum(val, cfg.HotkeyToggleCamera);
                    }
                    else if (currentSection.Equals("Camera", StringComparison.OrdinalIgnoreCase))
                    {
                        if (key == "MouseSensitivityX") cfg.MouseSensitivityX = ParseDouble(val, cfg.MouseSensitivityX, 0.1, 5.0);
                        else if (key == "MouseSensitivityY") cfg.MouseSensitivityY = ParseDouble(val, cfg.MouseSensitivityY, 0.1, 5.0);
                        else if (key == "MouseDeadzone") cfg.MouseDeadzone = ParseInt(val, cfg.MouseDeadzone, 0, 50);
                        else if (key == "CameraReleaseDelayMs") cfg.CameraReleaseDelayMs = ParseInt(val, cfg.CameraReleaseDelayMs, 15, 250);
                        else if (key == "WheelPulseMs") cfg.WheelPulseMs = ParseInt(val, cfg.WheelPulseMs, 20, 200);
                        else if (key == "InvertCameraX") cfg.InvertCameraX = ParseBool(val, cfg.InvertCameraX);
                        else if (key == "InvertCameraY") cfg.InvertCameraY = ParseBool(val, cfg.InvertCameraY);
                    }
                    else if (currentSection.Equals("Preset.Dmc5Style", StringComparison.OrdinalIgnoreCase))
                    {
                        ParsePresetEntry(cfg.Dmc5Preset, key, val);
                    }
                    else if (currentSection.Equals("Preset.ClassicStyle", StringComparison.OrdinalIgnoreCase))
                    {
                        ParsePresetEntry(cfg.ClassicPreset, key, val);
                    }
                    else if (currentSection.StartsWith("Game.", StringComparison.OrdinalIgnoreCase))
                    {
                        string gameId = currentSection.Substring(5);
                        GameProfile prof;
                        if (!gamesById.TryGetValue(gameId, out prof))
                        {
                            prof = new GameProfile { Id = gameId, DisplayName = gameId };
                            gamesById[gameId] = prof;
                            cfg.Games.Add(prof);
                        }

                        if (key == "DisplayName") prof.DisplayName = val;
                        else if (key == "ProcessNames")
                        {
                            prof.ProcessNames.Clear();
                            foreach (string p in val.Split(','))
                            {
                                string cleaned = p.Trim();
                                if (cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                    cleaned = cleaned.Substring(0, cleaned.Length - 4);
                                if (cleaned.Length > 0) prof.ProcessNames.Add(cleaned);
                            }
                        }
                        else if (key == "EnableMouseCamera") prof.EnableMouseCamera = ParseBool(val, prof.EnableMouseCamera);
                        else if (key == "HorizontalOnlyCamera") prof.HorizontalOnlyCamera = ParseBool(val, prof.HorizontalOnlyCamera);
                        else if (key.StartsWith("Key."))
                        {
                            GameAction act;
                            if (Enum.TryParse(key.Substring(4), true, out act))
                                prof.GameKeys[act] = ParseEnum(val, InputCode.None);
                        }
                    }
                }
            }
            catch
            {
                // Fallback to defaults if config file was corrupted
            }

            return cfg;
        }

        private static void ParsePresetEntry(Dictionary<GameAction, ActionBinding> target, string key, string val)
        {
            GameAction act;
            if (!Enum.TryParse(key, true, out act)) return;
            string[] parts = val.Split(',');
            InputCode primary = parts.Length > 0 ? ParseEnum(parts[0].Trim(), InputCode.None) : InputCode.None;
            InputCode secondary = parts.Length > 1 ? ParseEnum(parts[1].Trim(), InputCode.None) : InputCode.None;
            target[act] = new ActionBinding(primary, secondary);
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            T result;
            return Enum.TryParse(value, true, out result) ? result : fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool b;
            return bool.TryParse(value, out b) ? b : fallback;
        }

        private static int ParseInt(string value, int fallback, int min, int max)
        {
            int n;
            if (!int.TryParse(value, out n)) return fallback;
            if (n < min) return min;
            if (n > max) return max;
            return n;
        }

        private static double ParseDouble(string value, double fallback, double min, double max)
        {
            double d;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return fallback;
            if (d < min) return min;
            if (d > max) return max;
            return d;
        }
    }
}
