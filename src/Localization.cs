using System;
using System.Globalization;

namespace DmcKbmMod
{
    public enum AppLanguage
    {
        En = 0,
        Es = 1
    }

    public static class Localization
    {
        public static AppLanguage Current { get; private set; }

        static Localization()
        {
            Current = DetectSystemLanguage();
        }

        public static AppLanguage DetectSystemLanguage()
        {
            try
            {
                string iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return iso.Equals("es", StringComparison.OrdinalIgnoreCase) ? AppLanguage.Es : AppLanguage.En;
            }
            catch
            {
                return AppLanguage.En;
            }
        }

        public static void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                Current = DetectSystemLanguage();
                return;
            }
            Current = code.Equals("es", StringComparison.OrdinalIgnoreCase) ? AppLanguage.Es : AppLanguage.En;
        }

        public static void SetLanguage(AppLanguage lang)
        {
            Current = lang;
        }

        public static bool IsEs { get { return Current == AppLanguage.Es; } }

        public static string T(string en, string es)
        {
            return IsEs ? es : en;
        }

        public static string ActionLabel(GameAction action)
        {
            switch (action)
            {
                case GameAction.Melee: return T("Sword / Melee Attack", "Espada / Ataque Melee");
                case GameAction.Gun: return T("Gun / Shoot", "Pistola / Disparo");
                case GameAction.StyleOrBuster: return T("Style / Devil Bringer / Action", "Estilo / Devil Bringer / Acción");
                case GameAction.Jump: return T("Jump", "Saltar");
                case GameAction.LockOn: return T("Lock-On", "Fijar Objetivo (Lock-On)");
                case GameAction.DevilTrigger: return "Devil Trigger";
                case GameAction.ChangeGunOrExceed: return T("Switch Gun / Exceed", "Cambiar Pistola / Exceed");
                case GameAction.ChangeSword: return T("Switch Melee Weapon", "Cambiar Arma Melee");
                case GameAction.CameraReset: return T("Center Camera", "Centrar Cámara");
                case GameAction.ChangeTarget: return T("Switch Target", "Cambiar Objetivo");
                case GameAction.Taunt: return T("Taunt", "Burla (Taunt)");
                case GameAction.CameraUp: return T("Camera Up", "Cámara Arriba");
                case GameAction.CameraDown: return T("Camera Down", "Cámara Abajo");
                case GameAction.CameraLeft: return T("Camera Left", "Cámara Izquierda");
                case GameAction.CameraRight: return T("Camera Right", "Cámara Derecha");
                default: return action.ToString();
            }
        }

        public static string InputLabel(InputCode code)
        {
            switch (code)
            {
                case InputCode.None: return T("(Unbound)", "(Sin asignar)");
                case InputCode.MouseLeft: return T("Left Click (LMB)", "Clic Izq (LMB)");
                case InputCode.MouseRight: return T("Right Click (RMB)", "Clic Der (RMB)");
                case InputCode.MouseMiddle: return T("Middle Click (MMB)", "Clic Medio (MMB)");
                case InputCode.MouseX1: return T("Mouse Side 4 (Back)", "Ratón Lateral 4 (Atrás)");
                case InputCode.MouseX2: return T("Mouse Side 5 (Forward)", "Ratón Lateral 5 (Adelante)");
                case InputCode.MouseWheelUp: return T("Wheel Up", "Rueda Arriba");
                case InputCode.MouseWheelDown: return T("Wheel Down", "Rueda Abajo");
                case InputCode.Space: return T("Space", "Espacio");
                case InputCode.LShift: return T("Left Shift", "Shift Izq");
                case InputCode.RShift: return T("Right Shift", "Shift Der");
                case InputCode.LCtrl: return T("Left Ctrl", "Ctrl Izq");
                case InputCode.RCtrl: return T("Right Ctrl", "Ctrl Der");
                case InputCode.LAlt: return T("Left Alt", "Alt Izq");
                case InputCode.RAlt: return T("Right Alt", "Alt Der");
                case InputCode.Up: return T("Up Arrow", "Flecha Arriba");
                case InputCode.Down: return T("Down Arrow", "Flecha Abajo");
                case InputCode.Left: return T("Left Arrow", "Flecha Izquierda");
                case InputCode.Right: return T("Right Arrow", "Flecha Derecha");
                case InputCode.D0: return "0";
                case InputCode.D1: return "1";
                case InputCode.D2: return "2";
                case InputCode.D3: return "3";
                case InputCode.D4: return "4";
                case InputCode.D5: return "5";
                case InputCode.D6: return "6";
                case InputCode.D7: return "7";
                case InputCode.D8: return "8";
                case InputCode.D9: return "9";
                default: return code.ToString();
            }
        }

        public static string PresetName(PresetType preset)
        {
            return preset == PresetType.Dmc5Style
                ? T("DMC5 Style", "Estilo DMC5")
                : T("Classic Style", "Estilo Clásico");
        }
    }
}
