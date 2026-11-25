using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DmcKbmMod
{
    public class MainForm : Form
    {
        // DMC Dark Crimson Palette
        private static readonly Color BgDark = Color.FromArgb(18, 18, 24);
        private static readonly Color BgCard = Color.FromArgb(28, 28, 38);
        private static readonly Color BgInput = Color.FromArgb(38, 38, 52);
        private static readonly Color BorderColor = Color.FromArgb(56, 56, 74);
        private static readonly Color AccentCrimson = Color.FromArgb(204, 38, 58);
        private static readonly Color AccentCrimsonHover = Color.FromArgb(228, 52, 72);
        private static readonly Color AccentBlue = Color.FromArgb(52, 132, 224);
        private static readonly Color AccentGreen = Color.FromArgb(46, 178, 108);
        private static readonly Color AccentAmber = Color.FromArgb(230, 162, 46);
        private static readonly Color TextPrimary = Color.FromArgb(242, 244, 250);
        private static readonly Color TextMuted = Color.FromArgb(164, 170, 188);

        private readonly string _configPath;
        private readonly ModConfig _config;
        private readonly InputEngine _engine;
        private readonly OverlayToast _toast;

        private readonly Timer _pollTimer;
        private readonly Timer _engineTickTimer;

        private NotifyIcon _trayIcon;
        private ToolStripMenuItem _trayToggleItem;
        private ToolStripMenuItem _trayPresetDmc5Item;
        private ToolStripMenuItem _trayPresetClassicItem;
        private ToolStripMenuItem _trayCameraItem;
        private ToolStripMenuItem _trayOpenItem;
        private ToolStripMenuItem _trayExitItem;

        // Top bar controls
        private Label _lblStatusDot;
        private Label _lblStatusText;
        private Label _lblAdminWarning;
        private Button _btnLangSwitch;
        private Button _btnRestartAdmin;
        private Button _btnMasterToggle;

        // Preset switch controls
        private Label _lblSwitchTitle;
        private Button _btnPresetDmc5;
        private Button _btnPresetClassic;
        private Label _lblPresetSummary;

        // Tabs
        private TabPage _tabBindings;
        private TabPage _tabCamera;
        private TabPage _tabGames;
        private TabPage _tabTester;

        // Bindings tab
        private Panel _bindingsScrollPanel;
        private Label _lblCaptureBanner;
        private Button _btnResetPreset;
        private Label _lblColAction;
        private Label _lblColPrimary;
        private Label _lblColSecondary;
        private Label _lblBindingsNote;
        private readonly Dictionary<GameAction, Label> _actionLabels = new Dictionary<GameAction, Label>();
        private readonly Dictionary<GameAction, Tuple<Button, Button>> _bindButtons = new Dictionary<GameAction, Tuple<Button, Button>>();

        // Camera tab
        private CheckBox _chkCamMaster;
        private CheckBox _chkLockCursor;
        private CheckBox _chkShowToast;
        private CheckBox _chkInvertX;
        private CheckBox _chkInvertY;
        private Label _lblSensX;
        private Label _lblSensY;
        private Label _lblDeadzone;
        private Label _lblRelease;
        private Label _lblWheel;
        private TrackBar _trkSensX;
        private TrackBar _trkSensY;
        private TrackBar _trkDeadzone;
        private TrackBar _trkReleaseMs;
        private TrackBar _trkWheelMs;
        private Label _lblSensXVal;
        private Label _lblSensYVal;
        private Label _lblDeadzoneVal;
        private Label _lblReleaseVal;
        private Label _lblWheelVal;
        private Label _lblCameraTip;
        private bool _suppressUiEvents = false;

        // Games tab
        private Label _lblGameSel;
        private Label _lblGameProcs;
        private Label _lblGameKeysSub;
        private ComboBox _cmbGameProfile;
        private TextBox _txtProcessNames;
        private CheckBox _chkGameEnableCam;
        private CheckBox _chkGameHorizOnly;
        private Panel _gameKeysPanel;

        // Live Tester tab
        private Button _btnToggleTestMode;
        private Label _lblSimProfile;
        private Button _btnClearLog;
        private ComboBox _cmbTestGame;
        private Panel _testCanvas;
        private ListBox _lstTelemetry;
        private string _lastTesterAction = "";

        private bool _forceClose = false;

        public MainForm(string configPath)
        {
            _configPath = configPath;
            _config = ModConfig.LoadOrCreate(_configPath);
            Localization.SetLanguage(_config.Language);

            _engine = new InputEngine(_config);
            _toast = new OverlayToast();
            _lastTesterAction = Localization.T("Move mouse or press keys inside the box", "Mueve el ratón o pulsa teclas dentro del recuadro");

            Text = "DMC KBM Mod — DMC4 / DMC4SE & DMC HD Collection";
            Size = new Size(900, 710);
            MinimumSize = new Size(840, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgDark;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;
            Icon = CreateAppIcon();

            BuildLayout();
            BuildTrayIcon();
            ApplyLocalization();

            _engine.StateChanged += () => SafeInvoke(RefreshAllUiState);
            _engine.ToastRequested += (t, s) => SafeInvoke(() => _toast.ShowMessage(t, s));
            _engine.TelemetryLogged += (ev) => SafeInvoke(() => OnTelemetryEvent(ev));

            _pollTimer = new Timer { Interval = 150 };
            _pollTimer.Tick += (s, e) =>
            {
                UpdateTestCanvasBounds();
                _engine.PollForegroundWindow();
            };

            _engineTickTimer = new Timer { Interval = 12 };
            _engineTickTimer.Tick += (s, e) =>
            {
                _engine.UpdateTimers();
                if (_engine.TestModeActive && _testCanvas != null && _testCanvas.Visible)
                    _testCanvas.Invalidate();
            };

            Load += OnFormLoad;
            FormClosing += OnFormClosing;
            Resize += (s, e) =>
            {
                UpdateTestCanvasBounds();
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    _trayIcon.ShowBalloonTip(
                        1500,
                        "DMC KBM Mod",
                        Localization.T("Running in background (system tray).", "Ejecutándose en segundo plano (bandeja del sistema)."),
                        ToolTipIcon.Info);
                }
            };
            Move += (s, e) => UpdateTestCanvasBounds();
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            _engine.AppWindowHandle = Handle;
            NativeInput.RegisterMouseRawInput(Handle);
            _engine.InstallHooks();
            _pollTimer.Start();
            _engineTickTimer.Start();
            RefreshAllUiState();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_forceClose && e.CloseReason == CloseReason.UserClosing)
            {
                // Clean exit by default, save config first
            }
            SaveConfigSafe();
            _pollTimer.Stop();
            _engineTickTimer.Stop();
            _engine.Dispose();
            if (_toast != null) _toast.Dispose();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeInput.WM_INPUT)
            {
                int dx, dy;
                if (NativeInput.TryReadRawMouseDelta(m.LParam, out dx, out dy))
                {
                    _engine.OnRawMouseDelta(dx, dy);
                }
            }
            base.WndProc(ref m);
        }

        private void SafeInvoke(Action act)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(act); } catch { }
            }
            else
            {
                act();
            }
        }

        private void SaveConfigSafe()
        {
            try { _config.Save(_configPath); } catch { }
        }

        private void ToggleLanguage()
        {
            AppLanguage next = Localization.IsEs ? AppLanguage.En : AppLanguage.Es;
            Localization.SetLanguage(next);
            _config.Language = next == AppLanguage.Es ? "es" : "en";
            SaveConfigSafe();
            ApplyLocalization();
            RefreshAllUiState();
            RefreshSelectedGameProfileUi();
            if (_testCanvas != null) _testCanvas.Invalidate();
        }

        private void ApplyLocalization()
        {
            _btnLangSwitch.Text = Localization.IsEs ? "ES / EN" : "EN / ES";
            _lblAdminWarning.Text = Localization.T("⚠ Game running as Admin", "⚠ Juego ejecutado como Admin");
            _btnRestartAdmin.Text = Localization.T("Run as Admin", "Reiniciar Admin");

            _lblSwitchTitle.Text = Localization.T(
                "CONTROL SCHEME (Quick in-game switch: F2)",
                "ESQUEMA DE CONTROLES (Switch rápido en juego: F2)");
            _btnPresetDmc5.Text = Localization.T(
                "DMC5 Style  (LMB Sword · RMB Style)",
                "Estilo DMC5  (LMB Espada · RMB Estilo)");
            _btnPresetClassic.Text = Localization.T(
                "Classic Style  (LMB Sword · RMB Gun)",
                "Estilo Clásico  (LMB Espada · RMB Pistola)");
            _lblPresetSummary.Text = Localization.T(
                "F1: Enable/Pause   |   F2: Switch preset   |   F3: Mouse camera",
                "F1: Activar/Pausar   |   F2: Cambiar estilo   |   F3: Cámara ratón");

            _tabBindings.Text = Localization.T("Controls (Preset)", "Controles (Preset)");
            _tabCamera.Text = Localization.T("Camera & Mouse", "Cámara y Ratón");
            _tabGames.Text = Localization.T("Game Profiles", "Perfiles por Juego");
            _tabTester.Text = Localization.T("Live Tester", "Probador en Vivo");

            _lblCaptureBanner.Text = Localization.T(
                "Click a button to rebind (mouse, wheel, or keyboard). Press Del to clear or Esc to cancel.",
                "Haz clic en un botón para reasignar (ratón, rueda o teclado). Pulsa Supr para limpiar o Esc para cancelar.");
            _btnResetPreset.Text = Localization.T("Reset preset", "Restaurar preset");
            _lblColAction.Text = Localization.T("ACTION", "ACCIÓN");
            _lblColPrimary.Text = Localization.T("PRIMARY INPUT", "ENTRADA PRINCIPAL");
            _lblColSecondary.Text = Localization.T("SECONDARY INPUT", "ENTRADA SECUNDARIA");
            foreach (var kv in _actionLabels)
                kv.Value.Text = ModConfig.GetActionLabel(kv.Key);
            _lblBindingsNote.Text = Localization.T(
                "Note: Movement (W, A, S, D) and Dante Styles (1, 2, 3, 4 in DMC4 / DDMK) pass natively to the game with zero latency.",
                "Nota: Movimiento (W, A, S, D) y Estilos de Dante (1, 2, 3, 4 en DMC4 / DDMK) pasan de forma nativa al juego sin latencia.");

            _chkCamMaster.Text = Localization.T("Enable mouse camera control (In-game hotkey: F3)", "Activar control de cámara con el ratón (Atajo en juego: F3)");
            _chkLockCursor.Text = Localization.T("Confine cursor inside the game window (prevents multi-monitor escape)", "Confinar cursor dentro de la ventana del juego (evita salir a otro monitor)");
            _chkShowToast.Text = Localization.T("Show floating HUD toast when switching DMC5 / Classic preset or pausing", "Mostrar aviso flotante en pantalla al cambiar entre Estilo DMC5 / Clásico o pausar");
            _chkInvertX.Text = Localization.T("Invert X axis", "Invertir eje X");
            _chkInvertY.Text = Localization.T("Invert Y axis", "Invertir eje Y");
            _lblSensX.Text = Localization.T("Horizontal Sensitivity (X):", "Sensibilidad Horizontal (X):");
            _lblSensY.Text = Localization.T("Vertical Sensitivity (Y):", "Sensibilidad Vertical (Y):");
            _lblDeadzone.Text = Localization.T("Mouse Deadzone:", "Zona muerta del ratón (Deadzone):");
            _lblRelease.Text = Localization.T("Camera hold smoothing (ms):", "Suavizado de retención de cámara (ms):");
            _lblWheel.Text = Localization.T("Mouse wheel pulse duration (ms):", "Duración pulso rueda del ratón (ms):");
            _lblCameraTip.Text = Localization.T(
                "Tip: DMC4/DMC4SE have free camera in most areas (X + Y). In DMC3, horizontal-only usually feels best (configurable per game in 'Game Profiles').",
                "Consejo: DMC4/DMC4SE tienen cámara libre en casi todas las zonas (X + Y). En DMC3 suele sentirse mejor solo Horizontal (configurable por juego en 'Perfiles por Juego').");

            _lblGameSel.Text = Localization.T("Game:", "Juego:");
            _lblGameProcs.Text = Localization.T("Processes (.exe without extension, comma-separated):", "Procesos (.exe sin extensión, separados por coma):");
            _chkGameEnableCam.Text = Localization.T("Enable mouse camera for this game", "Activar cámara por ratón en este juego");
            _chkGameHorizOnly.Text = Localization.T("Horizontal movement only (recommended for DMC1/2/3)", "Solo movimiento horizontal (recomendado en DMC1/2/3)");
            _lblGameKeysSub.Text = Localization.T(
                "  In-game target keys (only edit these if you rebound keys inside the game's own options):",
                "  Teclas reales configuradas dentro del juego (solo cámbialas si modificaste los controles dentro del menú del juego):");

            _btnToggleTestMode.Text = _engine.TestModeActive
                ? Localization.T("■ Stop Test Mode", "■ Detener Modo Prueba")
                : Localization.T("▶ Start Local Test Mode", "▶ Activar Modo Prueba Local");
            _lblSimProfile.Text = Localization.T("Simulate profile:", "Simular perfil:");
            _btnClearLog.Text = Localization.T("Clear log", "Limpiar log");

            if (_trayToggleItem != null)
            {
                _trayToggleItem.Text = Localization.T("Mod Enabled (F1)", "Mod Activado (F1)");
                _trayPresetDmc5Item.Text = Localization.T("Preset: DMC5 Style", "Esquema: Estilo DMC5");
                _trayPresetClassicItem.Text = Localization.T("Preset: Classic Style", "Esquema: Estilo Clásico");
                _trayCameraItem.Text = Localization.T("Mouse Camera (F3)", "Cámara con Ratón (F3)");
                _trayOpenItem.Text = Localization.T("Open Window", "Abrir Ventana");
                _trayExitItem.Text = Localization.T("Exit", "Salir");
            }
        }

        private void BuildLayout()
        {
            // 1. Header / Status Bar
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = BgCard
            };
            headerPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(AccentCrimson, 2))
                    e.Graphics.DrawLine(p, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var headerButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 20, 14, 0)
            };

            _btnMasterToggle = CreateStyledButton("MOD: ON (F1)", AccentGreen, 152, 36);
            _btnMasterToggle.Margin = new Padding(8, 0, 0, 0);
            _btnMasterToggle.Click += (s, e) =>
            {
                _engine.SetModEnabled(!_engine.ModEnabled, false);
                _config.StartEnabled = _engine.ModEnabled;
                SaveConfigSafe();
            };

            _btnRestartAdmin = CreateStyledButton("Run as Admin", BgInput, 118, 36);
            _btnRestartAdmin.Margin = new Padding(8, 0, 0, 0);
            _btnRestartAdmin.Visible = !NativeInput.IsCurrentProcessAdmin();
            _btnRestartAdmin.Click += (s, e) => RestartAsAdmin();

            _btnLangSwitch = CreateStyledButton("EN / ES", AccentBlue, 82, 36);
            _btnLangSwitch.Margin = new Padding(8, 0, 0, 0);
            _btnLangSwitch.Click += (s, e) => ToggleLanguage();

            headerButtons.Controls.Add(_btnMasterToggle);
            headerButtons.Controls.Add(_btnRestartAdmin);
            headerButtons.Controls.Add(_btnLangSwitch);

            var lblTitle = new Label
            {
                Text = "DEVIL MAY CRY — KBM MOD",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(16, 10)
            };

            _lblStatusDot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = AccentAmber,
                AutoSize = true,
                Location = new Point(16, 40)
            };

            _lblStatusText = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9.2f),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(36, 42)
            };

            _lblAdminWarning = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = AccentAmber,
                AutoSize = true,
                Visible = false,
                Location = new Point(36, 60)
            };

            headerPanel.Controls.AddRange(new Control[]
            {
                lblTitle, _lblStatusDot, _lblStatusText, _lblAdminWarning, headerButtons
            });

            // 2. Prominent Dual-Preset Switch Bar
            var switchBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Color.FromArgb(22, 22, 30)
            };

            _lblSwitchTitle = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(16, 8)
            };

            _btnPresetDmc5 = CreateStyledButton("", AccentCrimson, 290, 36);
            _btnPresetDmc5.Location = new Point(16, 29);
            _btnPresetDmc5.Click += (s, e) =>
            {
                _engine.SetPreset(PresetType.Dmc5Style, false);
                SaveConfigSafe();
            };

            _btnPresetClassic = CreateStyledButton("", BgInput, 290, 36);
            _btnPresetClassic.Location = new Point(314, 29);
            _btnPresetClassic.Click += (s, e) =>
            {
                _engine.SetPreset(PresetType.ClassicStyle, false);
                SaveConfigSafe();
            };

            _lblPresetSummary = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.3f),
                ForeColor = TextMuted,
                AutoSize = false,
                Size = new Size(255, 42),
                Location = new Point(618, 26)
            };
            switchBar.Resize += (s, e) =>
            {
                _lblPresetSummary.Width = Math.Max(140, switchBar.ClientSize.Width - _lblPresetSummary.Left - 14);
            };

            switchBar.Controls.AddRange(new Control[]
            {
                _lblSwitchTitle, _btnPresetDmc5, _btnPresetClassic, _lblPresetSummary
            });

            // 3. Main Tabs
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(14, 6),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _tabBindings = new TabPage { BackColor = BgDark, ForeColor = TextPrimary };
            _tabCamera = new TabPage { BackColor = BgDark, ForeColor = TextPrimary };
            _tabGames = new TabPage { BackColor = BgDark, ForeColor = TextPrimary };
            _tabTester = new TabPage { BackColor = BgDark, ForeColor = TextPrimary };

            BuildBindingsTab(_tabBindings);
            BuildCameraTab(_tabCamera);
            BuildGamesTab(_tabGames);
            BuildTesterTab(_tabTester);

            tabs.TabPages.AddRange(new[] { _tabBindings, _tabCamera, _tabGames, _tabTester });
            tabs.SelectedIndexChanged += (s, e) =>
            {
                if (tabs.SelectedTab != _tabTester && _engine.TestModeActive)
                {
                    SetTestMode(false);
                }
                UpdateTestCanvasBounds();
            };

            Controls.Add(tabs);
            Controls.Add(switchBar);
            Controls.Add(headerPanel);
        }

        private void BuildBindingsTab(TabPage page)
        {
            var topBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = BgDark };

            _lblCaptureBanner = new Label
            {
                Text = "",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.8f),
                AutoSize = true,
                Location = new Point(14, 14)
            };

            _btnResetPreset = CreateStyledButton("", BgInput, 138, 30);
            _btnResetPreset.Location = new Point(720, 8);
            topBar.Resize += (s, e) =>
            {
                _btnResetPreset.Left = Math.Max(520, topBar.ClientSize.Width - _btnResetPreset.Width - 14);
            };
            _btnResetPreset.Click += (s, e) =>
            {
                if (_config.ActivePreset == PresetType.Dmc5Style)
                    _config.Dmc5Preset = ModConfig.CreateDefaultDmc5Preset();
                else
                    _config.ClassicPreset = ModConfig.CreateDefaultClassicPreset();
                _engine.RebuildLookupTables();
                SaveConfigSafe();
                RefreshBindingsGrid();
            };

            topBar.Controls.Add(_lblCaptureBanner);
            topBar.Controls.Add(_btnResetPreset);

            var colHeader = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = BgCard };
            _lblColAction = new Label { Text = "", ForeColor = TextMuted, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Location = new Point(18, 6), AutoSize = true };
            _lblColPrimary = new Label { Text = "", ForeColor = TextMuted, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Location = new Point(290, 6), AutoSize = true };
            _lblColSecondary = new Label { Text = "", ForeColor = TextMuted, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Location = new Point(510, 6), AutoSize = true };
            colHeader.Controls.AddRange(new Control[] { _lblColAction, _lblColPrimary, _lblColSecondary });

            _bindingsScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgDark,
                Padding = new Padding(12)
            };

            int y = 8;
            var bindingRows = new List<Panel>();
            foreach (var action in ModConfig.EditablePlayerActions)
            {
                var row = new Panel
                {
                    Location = new Point(8, y),
                    Size = new Size(820, 38),
                    BackColor = BgCard
                };
                bindingRows.Add(row);

                var lblAct = new Label
                {
                    Text = ModConfig.GetActionLabel(action),
                    ForeColor = TextPrimary,
                    Font = new Font("Segoe UI", 9.5f),
                    Location = new Point(12, 9),
                    Size = new Size(260, 22)
                };

                var btnPrimary = CreateStyledButton("", BgInput, 200, 28);
                btnPrimary.Location = new Point(282, 5);
                GameAction capturedAct = action;
                btnPrimary.Click += (s, e) => BeginCapturePresetBind(capturedAct, true, btnPrimary);

                var btnSecondary = CreateStyledButton("", BgInput, 200, 28);
                btnSecondary.Location = new Point(502, 5);
                btnSecondary.Click += (s, e) => BeginCapturePresetBind(capturedAct, false, btnSecondary);

                row.Controls.AddRange(new Control[] { lblAct, btnPrimary, btnSecondary });
                _bindingsScrollPanel.Controls.Add(row);
                _actionLabels[action] = lblAct;
                _bindButtons[action] = Tuple.Create(btnPrimary, btnSecondary);
                y += 43;
            }

            _lblBindingsNote = new Label
            {
                Text = "",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(12, y + 6),
                AutoSize = true
            };
            _bindingsScrollPanel.Controls.Add(_lblBindingsNote);
            _bindingsScrollPanel.Resize += (s, e) =>
            {
                int w = Math.Max(740, _bindingsScrollPanel.ClientSize.Width - 28);
                foreach (var r in bindingRows) r.Width = w;
            };

            page.Controls.Add(_bindingsScrollPanel);
            page.Controls.Add(colHeader);
            page.Controls.Add(topBar);
        }

        private void BeginCapturePresetBind(GameAction action, bool isPrimary, Button targetBtn)
        {
            targetBtn.Text = Localization.T("[ Press key or mouse... ]", "[ Pulsa tecla o ratón... ]");
            targetBtn.BackColor = AccentBlue;
            _lblCaptureBanner.Text = Localization.T(
                "Waiting for input for \"" + ModConfig.GetActionLabel(action) + "\"... (Esc: cancel | Del: clear)",
                "Esperando entrada para \"" + ModConfig.GetActionLabel(action) + "\"... (Esc: cancelar | Supr: borrar)");
            _lblCaptureBanner.ForeColor = AccentAmber;

            _engine.StartBindCapture(true, (code, cancelled) => SafeInvoke(() =>
            {
                _lblCaptureBanner.Text = Localization.T(
                    "Click a button to rebind (mouse, wheel, or keyboard). Press Del to clear or Esc to cancel.",
                    "Haz clic en un botón para reasignar (ratón, rueda o teclado). Pulsa Supr para limpiar o Esc para cancelar.");
                _lblCaptureBanner.ForeColor = TextMuted;

                if (!cancelled)
                {
                    var preset = _config.GetPresetBindings(_config.ActivePreset);
                    ActionBinding binding;
                    if (!preset.TryGetValue(action, out binding))
                    {
                        binding = new ActionBinding();
                        preset[action] = binding;
                    }
                    if (isPrimary) binding.Primary = code;
                    else binding.Secondary = code;

                    _engine.RebuildLookupTables();
                    SaveConfigSafe();
                }
                RefreshBindingsGrid();
            }));
        }

        private void BuildCameraTab(TabPage page)
        {
            var container = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20), BackColor = BgDark };

            var cardToggles = new Panel { Location = new Point(18, 16), Size = new Size(825, 130), BackColor = BgCard };
            _chkCamMaster = CreateCheckBox("", 16, 14);
            _chkLockCursor = CreateCheckBox("", 16, 44);
            _chkShowToast = CreateCheckBox("", 16, 74);
            _chkInvertX = CreateCheckBox("", 460, 14);
            _chkInvertY = CreateCheckBox("", 605, 14);

            EventHandler onToggleChanged = (s, e) =>
            {
                if (_suppressUiEvents) return;
                _config.MouseCameraMasterEnabled = _chkCamMaster.Checked;
                _config.LockCursorToGame = _chkLockCursor.Checked;
                _config.ShowOverlayToast = _chkShowToast.Checked;
                _config.InvertCameraX = _chkInvertX.Checked;
                _config.InvertCameraY = _chkInvertY.Checked;
                SaveConfigSafe();
                RefreshAllUiState();
            };

            _chkCamMaster.CheckedChanged += onToggleChanged;
            _chkLockCursor.CheckedChanged += onToggleChanged;
            _chkShowToast.CheckedChanged += onToggleChanged;
            _chkInvertX.CheckedChanged += onToggleChanged;
            _chkInvertY.CheckedChanged += onToggleChanged;

            cardToggles.Controls.AddRange(new Control[] { _chkCamMaster, _chkLockCursor, _chkShowToast, _chkInvertX, _chkInvertY });

            var cardSliders = new Panel { Location = new Point(18, 162), Size = new Size(825, 310), BackColor = BgCard };

            AddSliderRow(cardSliders, 18, 10, 400, out _lblSensX, out _trkSensX, out _lblSensXVal);
            AddSliderRow(cardSliders, 72, 10, 400, out _lblSensY, out _trkSensY, out _lblSensYVal);
            AddSliderRow(cardSliders, 126, 0, 20, out _lblDeadzone, out _trkDeadzone, out _lblDeadzoneVal);
            AddSliderRow(cardSliders, 180, 20, 150, out _lblRelease, out _trkReleaseMs, out _lblReleaseVal);
            AddSliderRow(cardSliders, 234, 25, 120, out _lblWheel, out _trkWheelMs, out _lblWheelVal);

            EventHandler onSliderChanged = (s, e) =>
            {
                if (_suppressUiEvents) return;
                _config.MouseSensitivityX = _trkSensX.Value / 100.0;
                _config.MouseSensitivityY = _trkSensY.Value / 100.0;
                _config.MouseDeadzone = _trkDeadzone.Value;
                _config.CameraReleaseDelayMs = _trkReleaseMs.Value;
                _config.WheelPulseMs = _trkWheelMs.Value;
                UpdateCameraValueLabels();
                SaveConfigSafe();
            };

            _trkSensX.ValueChanged += onSliderChanged;
            _trkSensY.ValueChanged += onSliderChanged;
            _trkDeadzone.ValueChanged += onSliderChanged;
            _trkReleaseMs.ValueChanged += onSliderChanged;
            _trkWheelMs.ValueChanged += onSliderChanged;

            _lblCameraTip = new Label
            {
                Text = "",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(18, 482),
                Size = new Size(825, 36)
            };

            container.Controls.AddRange(new Control[] { cardToggles, cardSliders, _lblCameraTip });
            page.Controls.Add(container);
        }

        private void AddSliderRow(Panel parent, int y, int min, int max, out Label lbl, out TrackBar track, out Label valLabel)
        {
            lbl = new Label
            {
                Text = "",
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(18, y + 6),
                Size = new Size(290, 24)
            };
            track = new TrackBar
            {
                Minimum = min,
                Maximum = max,
                TickStyle = TickStyle.None,
                Location = new Point(316, y),
                Size = new Size(380, 38),
                BackColor = BgCard
            };
            valLabel = new Label
            {
                Text = "",
                ForeColor = AccentCrimsonHover,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(708, y + 6),
                Size = new Size(90, 24)
            };
            parent.Controls.AddRange(new Control[] { lbl, track, valLabel });
        }

        private void BuildGamesTab(TabPage page)
        {
            var topCard = new Panel { Dock = DockStyle.Top, Height = 122, BackColor = BgCard, Padding = new Padding(16) };

            _lblGameSel = new Label { Text = "", ForeColor = TextMuted, Location = new Point(16, 16), AutoSize = true };
            _cmbGameProfile = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextPrimary,
                Location = new Point(80, 12),
                Size = new Size(300, 28)
            };
            foreach (var g in _config.Games) _cmbGameProfile.Items.Add(g.DisplayName);
            _cmbGameProfile.SelectedIndex = 0;
            _cmbGameProfile.SelectedIndexChanged += (s, e) => RefreshSelectedGameProfileUi();

            _lblGameProcs = new Label { Text = "", ForeColor = TextMuted, Location = new Point(400, 16), AutoSize = true };
            _txtProcessNames = new TextBox
            {
                BackColor = BgInput,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(400, 40),
                Size = new Size(430, 26)
            };
            _txtProcessNames.Leave += (s, e) => SaveSelectedGameProcessNames();

            _chkGameEnableCam = CreateCheckBox("", 16, 52);
            _chkGameHorizOnly = CreateCheckBox("", 16, 82);

            _chkGameEnableCam.CheckedChanged += (s, e) =>
            {
                if (_suppressUiEvents) return;
                var prof = GetSelectedGameProfile();
                if (prof == null) return;
                prof.EnableMouseCamera = _chkGameEnableCam.Checked;
                SaveConfigSafe();
            };

            _chkGameHorizOnly.CheckedChanged += (s, e) =>
            {
                if (_suppressUiEvents) return;
                var prof = GetSelectedGameProfile();
                if (prof == null) return;
                prof.HorizontalOnlyCamera = _chkGameHorizOnly.Checked;
                SaveConfigSafe();
            };

            topCard.Controls.AddRange(new Control[]
            {
                _lblGameSel, _cmbGameProfile, _lblGameProcs, _txtProcessNames, _chkGameEnableCam, _chkGameHorizOnly
            });

            _lblGameKeysSub = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = BgDark
            };

            _gameKeysPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgDark,
                Padding = new Padding(12)
            };

            page.Controls.Add(_gameKeysPanel);
            page.Controls.Add(_lblGameKeysSub);
            page.Controls.Add(topCard);

            RefreshSelectedGameProfileUi();
        }

        private GameProfile GetSelectedGameProfile()
        {
            int idx = _cmbGameProfile.SelectedIndex;
            if (idx < 0 || idx >= _config.Games.Count) return null;
            return _config.Games[idx];
        }

        private void SaveSelectedGameProcessNames()
        {
            var prof = GetSelectedGameProfile();
            if (prof == null) return;
            prof.ProcessNames.Clear();
            foreach (string p in _txtProcessNames.Text.Split(','))
            {
                string c = p.Trim();
                if (c.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    c = c.Substring(0, c.Length - 4);
                if (c.Length > 0) prof.ProcessNames.Add(c);
            }
            _engine.RebuildLookupTables();
            SaveConfigSafe();
        }

        private void RefreshSelectedGameProfileUi()
        {
            var prof = GetSelectedGameProfile();
            if (prof == null) return;

            _suppressUiEvents = true;
            _txtProcessNames.Text = string.Join(", ", prof.ProcessNames.ToArray());
            _chkGameEnableCam.Checked = prof.EnableMouseCamera;
            _chkGameHorizOnly.Checked = prof.HorizontalOnlyCamera;
            _suppressUiEvents = false;

            _gameKeysPanel.SuspendLayout();
            _gameKeysPanel.Controls.Clear();

            int y = 6;
            foreach (var action in ModConfig.AllGameKeyActions)
            {
                var row = new Panel
                {
                    Location = new Point(8, y),
                    Size = new Size(825, 34),
                    BackColor = BgCard
                };

                var lbl = new Label
                {
                    Text = ModConfig.GetActionLabel(action),
                    ForeColor = TextPrimary,
                    Location = new Point(12, 7),
                    Size = new Size(340, 22)
                };

                InputCode currentKey;
                if (!prof.GameKeys.TryGetValue(action, out currentKey)) currentKey = InputCode.None;

                var btnKey = CreateStyledButton(ModConfig.GetInputLabel(currentKey), BgInput, 210, 26);
                btnKey.Location = new Point(370, 4);
                GameAction capAct = action;
                btnKey.Click += (s, e) =>
                {
                    btnKey.Text = Localization.T("[ Press game key... ]", "[ Pulsa tecla del juego... ]");
                    btnKey.BackColor = AccentBlue;
                    _engine.StartBindCapture(false, (code, cancelled) => SafeInvoke(() =>
                    {
                        if (!cancelled)
                        {
                            prof.GameKeys[capAct] = code;
                            SaveConfigSafe();
                        }
                        RefreshSelectedGameProfileUi();
                    }));
                };

                bool ext;
                ushort scan = ModConfig.IsKeyboardCode(currentKey) ? NativeInput.GetScanCode((ushort)currentKey, out ext) : (ushort)0;
                var lblScan = new Label
                {
                    Text = scan != 0 ? string.Format("DirectInput ScanCode: 0x{0:X2}", scan) : "—",
                    ForeColor = TextMuted,
                    Font = new Font("Consolas", 8.5f),
                    Location = new Point(595, 8),
                    AutoSize = true
                };

                row.Controls.AddRange(new Control[] { lbl, btnKey, lblScan });
                _gameKeysPanel.Controls.Add(row);
                y += 38;
            }
            _gameKeysPanel.ResumeLayout();
        }

        private void BuildTesterTab(TabPage page)
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = BgCard, Padding = new Padding(14) };

            _btnToggleTestMode = CreateStyledButton("", AccentCrimson, 220, 34);
            _btnToggleTestMode.Location = new Point(14, 11);
            _btnToggleTestMode.Click += (s, e) => SetTestMode(!_engine.TestModeActive);

            _lblSimProfile = new Label { Text = "", ForeColor = TextMuted, Location = new Point(252, 18), AutoSize = true };
            _cmbTestGame = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextPrimary,
                Location = new Point(365, 14),
                Size = new Size(270, 28)
            };
            foreach (var g in _config.Games) _cmbTestGame.Items.Add(g.DisplayName);
            _cmbTestGame.SelectedIndex = 0;
            _cmbTestGame.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbTestGame.SelectedIndex >= 0 && _cmbTestGame.SelectedIndex < _config.Games.Count)
                    _engine.TestModeProfile = _config.Games[_cmbTestGame.SelectedIndex];
            };

            _btnClearLog = CreateStyledButton("", BgInput, 100, 32);
            _btnClearLog.Location = new Point(738, 12);
            topPanel.Resize += (s, e) =>
            {
                _btnClearLog.Left = Math.Max(650, topPanel.ClientSize.Width - _btnClearLog.Width - 14);
            };
            _btnClearLog.Click += (s, e) => _lstTelemetry.Items.Clear();

            topPanel.Controls.AddRange(new Control[] { _btnToggleTestMode, _lblSimProfile, _cmbTestGame, _btnClearLog });

            var body = new Panel { Dock = DockStyle.Fill, BackColor = BgDark, Padding = new Padding(14) };

            _testCanvas = new Panel
            {
                Dock = DockStyle.Left,
                Width = 340,
                BackColor = Color.FromArgb(24, 24, 34),
                BorderStyle = BorderStyle.FixedSingle
            };
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, _testCanvas, new object[] { true });
            _testCanvas.Paint += PaintTestCanvas;

            var spacer = new Panel { Dock = DockStyle.Left, Width = 14, BackColor = BgDark };

            _lstTelemetry = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 14, 20),
                ForeColor = Color.FromArgb(215, 225, 240),
                Font = new Font("Consolas", 8.8f),
                BorderStyle = BorderStyle.FixedSingle
            };

            body.Controls.Add(_lstTelemetry);
            body.Controls.Add(spacer);
            body.Controls.Add(_testCanvas);

            page.Controls.Add(body);
            page.Controls.Add(topPanel);
        }

        private void SetTestMode(bool active)
        {
            _engine.TestModeActive = active;
            UpdateTestCanvasBounds();
            _btnToggleTestMode.Text = active
                ? Localization.T("■ Stop Test Mode", "■ Detener Modo Prueba")
                : Localization.T("▶ Start Local Test Mode", "▶ Activar Modo Prueba Local");
            _btnToggleTestMode.BackColor = active ? AccentGreen : AccentCrimson;
            if (active)
            {
                _lstTelemetry.Items.Insert(0, Localization.T(
                    "--- Test Mode ACTIVE (" + InputEngine.GetPresetDisplayName(_config.ActivePreset) + ") ---",
                    "--- Modo Prueba ACTIVADO (" + InputEngine.GetPresetDisplayName(_config.ActivePreset) + ") ---"));
                _lstTelemetry.Items.Insert(1, Localization.T(
                    "Click/scroll/move mouse inside the left box or press Space/Shift/Q/E/F/R.",
                    "Haz clic/rueda/mueve ratón dentro del cuadro izquierdo o pulsa Espacio/Shift/Q/E/F/R."));
            }
            else
            {
                _engine.ReleaseAllHeldKeys();
            }
            RefreshAllUiState();
            _testCanvas.Invalidate();
        }

        private void UpdateTestCanvasBounds()
        {
            if (_testCanvas == null || !_testCanvas.IsHandleCreated || !_testCanvas.Visible)
            {
                _engine.TestCanvasScreenRect = new NativeInput.RECT();
                return;
            }
            Point tl = _testCanvas.PointToScreen(Point.Empty);
            _engine.TestCanvasScreenRect = new NativeInput.RECT
            {
                Left = tl.X,
                Top = tl.Y,
                Right = tl.X + _testCanvas.Width,
                Bottom = tl.Y + _testCanvas.Height
            };
        }

        private void PaintTestCanvas(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = _testCanvas.Width / 2;
            int cy = _testCanvas.Height / 2 - 15;
            int radius = 92;

            using (var ringPen = new Pen(_engine.TestModeActive ? AccentCrimson : BorderColor, 2))
                g.DrawEllipse(ringPen, cx - radius, cy - radius, radius * 2, radius * 2);

            using (var crossPen = new Pen(Color.FromArgb(48, 48, 66), 1))
            {
                g.DrawLine(crossPen, cx - radius, cy, cx + radius, cy);
                g.DrawLine(crossPen, cx, cy - radius, cx, cy + radius);
            }

            DrawDirBadge(g, cx, cy - radius - 18, "▲", _engine.CameraDirY < 0);
            DrawDirBadge(g, cx, cy + radius + 18, "▼", _engine.CameraDirY > 0);
            DrawDirBadge(g, cx - radius - 22, cy, "◀", _engine.CameraDirX < 0);
            DrawDirBadge(g, cx + radius + 22, cy, "▶", _engine.CameraDirX > 0);

            int dotX = cx + (int)(_engine.CameraImpulseX * (radius - 16));
            int dotY = cy + (int)(_engine.CameraImpulseY * (radius - 16));
            using (var vecPen = new Pen(AccentCrimsonHover, 2.5f))
                g.DrawLine(vecPen, cx, cy, dotX, dotY);
            using (var dotBrush = new SolidBrush(_engine.TestModeActive ? AccentCrimsonHover : TextMuted))
                g.FillEllipse(dotBrush, dotX - 8, dotY - 8, 16, 16);

            using (var fTitle = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var fSub = new Font("Segoe UI", 8.2f))
            using (var brTitle = new SolidBrush(TextPrimary))
            using (var brSub = new SolidBrush(TextMuted))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                string header = _engine.TestModeActive
                    ? Localization.T("TEST ZONE ACTIVE", "ZONA DE PRUEBA ACTIVA")
                    : Localization.T("TEST ZONE (INACTIVE)", "ZONA DE PRUEBA (INACTIVA)");
                g.DrawString(header, fTitle, brTitle, new RectangleF(8, 10, _testCanvas.Width - 16, 22), sf);
                g.DrawString(_lastTesterAction, fSub, brSub, new RectangleF(10, _testCanvas.Height - 44, _testCanvas.Width - 20, 36), sf);
            }
        }

        private void DrawDirBadge(Graphics g, int x, int y, string symbol, bool active)
        {
            using (var f = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var br = new SolidBrush(active ? AccentGreen : Color.FromArgb(75, 75, 95)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(symbol, f, br, new PointF(x, y), sf);
            }
        }

        private void OnTelemetryEvent(LiveInputTelemetry ev)
        {
            string state = ev.IsDown ? "DOWN" : "UP  ";
            string keyWord = Localization.T("Key", "Tecla");
            string line = string.Format("[{0}] {1} {2,-16} -> {3,-26} => {4} {5,-12} (Scan {6})",
                ev.Timestamp, state, ev.PhysicalInput, ev.ActionName, keyWord, ev.TargetKey, ev.ScanCodeHex);

            _lastTesterAction = ev.PhysicalInput + "  →  " + ev.ActionName + " (" + ev.TargetKey + ")";
            _lstTelemetry.Items.Insert(0, line);
            while (_lstTelemetry.Items.Count > 120)
                _lstTelemetry.Items.RemoveAt(_lstTelemetry.Items.Count - 1);
            _testCanvas.Invalidate();
        }

        private void RefreshAllUiState()
        {
            if (!_engine.ModEnabled)
            {
                _lblStatusDot.ForeColor = AccentCrimson;
                _lblStatusText.Text = Localization.T(
                    "PAUSED — Press F1 or click the button to resume",
                    "PAUSADO — Pulsa F1 o el botón derecho para reactivar");
                _btnMasterToggle.Text = Localization.T("MOD: PAUSED (F1)", "MOD: PAUSADO (F1)");
                _btnMasterToggle.BackColor = AccentCrimson;
            }
            else if (_engine.ActiveGame != null)
            {
                _lblStatusDot.ForeColor = AccentGreen;
                _lblStatusText.Text = Localization.T("ACTIVE in ", "ACTIVO en ") + _engine.ActiveGame.DisplayName + " (" + _engine.ActiveProcessName + ".exe)";
                _btnMasterToggle.Text = Localization.T("MOD: ACTIVE (F1)", "MOD: ACTIVO (F1)");
                _btnMasterToggle.BackColor = AccentGreen;
            }
            else if (_engine.TestModeActive)
            {
                _lblStatusDot.ForeColor = AccentBlue;
                _lblStatusText.Text = Localization.T(
                    "LOCAL TEST MODE ACTIVE (no real keystrokes sent to Windows)",
                    "MODO PRUEBA LOCAL ACTIVO (sin enviar teclas reales a Windows)");
                _btnMasterToggle.Text = Localization.T("MOD: ACTIVE (F1)", "MOD: ACTIVO (F1)");
                _btnMasterToggle.BackColor = AccentGreen;
            }
            else
            {
                _lblStatusDot.ForeColor = AccentAmber;
                _lblStatusText.Text = Localization.T(
                    "Waiting — Launch DMC4, DMC4SE or DMC HD Collection (DMC1/2/3)",
                    "En espera — Abre DMC4, DMC4SE o DMC HD Collection (DMC1/2/3)");
                _btnMasterToggle.Text = Localization.T("MOD: ACTIVE (F1)", "MOD: ACTIVO (F1)");
                _btnMasterToggle.BackColor = AccentGreen;
            }

            _lblAdminWarning.Visible = _engine.ActiveGameIsElevatedMismatch;

            bool isDmc5 = _config.ActivePreset == PresetType.Dmc5Style;
            _btnPresetDmc5.BackColor = isDmc5 ? AccentCrimson : BgInput;
            _btnPresetDmc5.FlatAppearance.BorderColor = isDmc5 ? AccentCrimsonHover : BorderColor;
            _btnPresetClassic.BackColor = !isDmc5 ? AccentCrimson : BgInput;
            _btnPresetClassic.FlatAppearance.BorderColor = !isDmc5 ? AccentCrimsonHover : BorderColor;

            RefreshBindingsGrid();
            RefreshCameraControls();
            RefreshTrayMenu();
        }

        private void RefreshBindingsGrid()
        {
            var preset = _config.GetPresetBindings(_config.ActivePreset);
            foreach (var kv in _bindButtons)
            {
                ActionBinding bind;
                if (!preset.TryGetValue(kv.Key, out bind)) bind = new ActionBinding();
                kv.Value.Item1.Text = ModConfig.GetInputLabel(bind.Primary);
                kv.Value.Item1.BackColor = BgInput;
                kv.Value.Item2.Text = ModConfig.GetInputLabel(bind.Secondary);
                kv.Value.Item2.BackColor = BgInput;
            }
        }

        private void RefreshCameraControls()
        {
            _suppressUiEvents = true;
            _chkCamMaster.Checked = _config.MouseCameraMasterEnabled;
            _chkLockCursor.Checked = _config.LockCursorToGame;
            _chkShowToast.Checked = _config.ShowOverlayToast;
            _chkInvertX.Checked = _config.InvertCameraX;
            _chkInvertY.Checked = _config.InvertCameraY;

            _trkSensX.Value = Clamp((int)Math.Round(_config.MouseSensitivityX * 100), _trkSensX.Minimum, _trkSensX.Maximum);
            _trkSensY.Value = Clamp((int)Math.Round(_config.MouseSensitivityY * 100), _trkSensY.Minimum, _trkSensY.Maximum);
            _trkDeadzone.Value = Clamp(_config.MouseDeadzone, _trkDeadzone.Minimum, _trkDeadzone.Maximum);
            _trkReleaseMs.Value = Clamp(_config.CameraReleaseDelayMs, _trkReleaseMs.Minimum, _trkReleaseMs.Maximum);
            _trkWheelMs.Value = Clamp(_config.WheelPulseMs, _trkWheelMs.Minimum, _trkWheelMs.Maximum);
            _suppressUiEvents = false;
            UpdateCameraValueLabels();
        }

        private void UpdateCameraValueLabels()
        {
            _lblSensXVal.Text = _config.MouseSensitivityX.ToString("0.00") + "x";
            _lblSensYVal.Text = _config.MouseSensitivityY.ToString("0.00") + "x";
            _lblDeadzoneVal.Text = _config.MouseDeadzone + " px";
            _lblReleaseVal.Text = _config.CameraReleaseDelayMs + " ms";
            _lblWheelVal.Text = _config.WheelPulseMs + " ms";
        }

        private void BuildTrayIcon()
        {
            var menu = new ContextMenuStrip();
            _trayToggleItem = new ToolStripMenuItem("", null, (s, e) => _engine.SetModEnabled(!_engine.ModEnabled, true));
            _trayPresetDmc5Item = new ToolStripMenuItem("", null, (s, e) => { _engine.SetPreset(PresetType.Dmc5Style, true); SaveConfigSafe(); });
            _trayPresetClassicItem = new ToolStripMenuItem("", null, (s, e) => { _engine.SetPreset(PresetType.ClassicStyle, true); SaveConfigSafe(); });
            _trayCameraItem = new ToolStripMenuItem("", null, (s, e) => { _engine.ToggleMouseCamera(true); SaveConfigSafe(); });
            _trayOpenItem = new ToolStripMenuItem("", null, (s, e) => RestoreFromTray());
            _trayExitItem = new ToolStripMenuItem("", null, (s, e) => { _forceClose = true; Close(); });

            menu.Items.Add(_trayToggleItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_trayPresetDmc5Item);
            menu.Items.Add(_trayPresetClassicItem);
            menu.Items.Add(_trayCameraItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_trayOpenItem);
            menu.Items.Add(_trayExitItem);

            _trayIcon = new NotifyIcon
            {
                Icon = Icon,
                Text = "DMC KBM Mod",
                Visible = true,
                ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RefreshTrayMenu()
        {
            if (_trayToggleItem == null) return;
            _trayToggleItem.Checked = _engine.ModEnabled;
            _trayPresetDmc5Item.Checked = _config.ActivePreset == PresetType.Dmc5Style;
            _trayPresetClassicItem.Checked = _config.ActivePreset == PresetType.ClassicStyle;
            _trayCameraItem.Checked = _config.MouseCameraMasterEnabled;
            _trayIcon.Text = "DMC KBM Mod — " + (_engine.ModEnabled ? InputEngine.GetPresetDisplayName(_config.ActivePreset) : Localization.T("Paused", "Pausado"));
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void RestartAsAdmin()
        {
            try
            {
                SaveConfigSafe();
                var psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                _forceClose = true;
                Close();
            }
            catch
            {
                // User cancelled UAC prompt
            }
        }

        private static Button CreateStyledButton(string text, Color bg, int w, int h)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(w, h),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = BorderColor;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static CheckBox CreateCheckBox(string text, int x, int y)
        {
            return new CheckBox
            {
                Text = text,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.2f),
                AutoSize = true,
                Location = new Point(x, y)
            };
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static Icon CreateAppIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(20, 20, 28));
                using (var br = new SolidBrush(AccentCrimson))
                    g.FillEllipse(br, 2, 2, 28, 28);
                using (var f = new Font("Segoe UI", 13f, FontStyle.Bold))
                using (var w = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("D", f, w, new RectangleF(0, 0, 32, 32), sf);
                }
                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }
    }
}
