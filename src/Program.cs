using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DmcKbmMod
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Self-test CLI flag for automated verification without spawning interactive UI
            if (args != null && args.Length > 0 && args[0].Equals("--selftest", StringComparison.OrdinalIgnoreCase))
            {
                RunSelfTest();
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, "Local\\DMC_KBM_Mod_SingleInstance_v1", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        Localization.T(
                            "DMC KBM Mod is already running (check the system tray).",
                            "DMC KBM Mod ya se encuentra en ejecución (revisa la bandeja del sistema)."),
                        "DMC KBM Mod",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try { NativeInput.SetProcessDPIAware(); } catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(exeDir, "dmc_kbm_config.ini");

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try { NativeInput.ClipCursor(IntPtr.Zero); } catch { }
                };

                Application.Run(new MainForm(configPath));
            }
        }

        private static void RunSelfTest()
        {
            string tempIni = Path.Combine(Path.GetTempPath(), "dmc_kbm_selftest.ini");
            try
            {
                if (File.Exists(tempIni)) File.Delete(tempIni);

                var cfg = ModConfig.LoadOrCreate(tempIni);
                if (!File.Exists(tempIni)) throw new Exception("Config INI was not created.");
                if (cfg.Games.Count != 5) throw new Exception("Expected 5 game profiles (DMC4, DMC3, DMC1, DMC2, DmC).");

                // Verify switch between DMC5 and Classic presets
                cfg.ActivePreset = PresetType.ClassicStyle;
                cfg.MouseSensitivityX = 1.75;
                cfg.Save(tempIni);

                var reloaded = ModConfig.LoadOrCreate(tempIni);
                if (reloaded.ActivePreset != PresetType.ClassicStyle)
                    throw new Exception("Preset persistence failed.");
                if (Math.Abs(reloaded.MouseSensitivityX - 1.75) > 0.01)
                    throw new Exception("Sensitivity persistence failed.");

                // Verify DirectInput scan codes for critical keys
                bool ext;
                if (NativeInput.GetScanCode((ushort)InputCode.I, out ext) != 0x17) throw new Exception("ScanCode I mismatch.");
                if (NativeInput.GetScanCode((ushort)InputCode.Space, out ext) != 0x39) throw new Exception("ScanCode Space mismatch.");
                if (NativeInput.GetScanCode((ushort)InputCode.Up, out ext) != 0x48 || !ext) throw new Exception("ScanCode Up mismatch.");
                if (NativeInput.GetScanCode((ushort)InputCode.Left, out ext) != 0x4B || !ext) throw new Exception("ScanCode Left mismatch.");

                // Verify preset difference (DMC5 RMB = StyleOrBuster, Classic RMB = Gun)
                if (reloaded.Dmc5Preset[GameAction.StyleOrBuster].Primary != InputCode.MouseRight)
                    throw new Exception("DMC5 preset RMB binding mismatch.");
                if (reloaded.ClassicPreset[GameAction.Gun].Primary != InputCode.MouseRight)
                    throw new Exception("Classic preset RMB binding mismatch.");

                // Verify engine initialization & preset toggle
                using (var engine = new InputEngine(reloaded))
                {
                    engine.TogglePreset(false);
                    if (engine.Config.ActivePreset != PresetType.Dmc5Style)
                        throw new Exception("Engine TogglePreset failed.");
                }

                Console.WriteLine("SELFTEST_OK");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELFTEST_FAIL: " + ex.Message);
                Environment.Exit(1);
            }
            finally
            {
                try { if (File.Exists(tempIni)) File.Delete(tempIni); } catch { }
            }
        }
    }
}
