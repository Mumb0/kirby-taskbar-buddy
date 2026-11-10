using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Core application orchestrator wiring the system tray menu, registry persistence,
    /// global keyboard hook, desktop environment monitor, 60 Hz timer, and <see cref="TaskbarWorld"/>.
    /// </summary>
    public class KirbyTaskbarBuddyContext : ApplicationContext {

        private readonly AppSettings _settings = new AppSettings();
        private readonly TrayMenuController _trayMenu;
        private readonly OverlayStage _stage;
        private readonly GlobalKeyboardHook _keyboardHook;
        private readonly IdleActivityMonitor _idleMonitor;
        private readonly DesktopEnvironmentMonitor _envMonitor;
        private readonly TaskbarWorld _world;
        private readonly Timer _animTimer;
        private readonly Stopwatch _stopwatch;
        private long _lastTickMs;

        public KirbyTaskbarBuddyContext() {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef initialForm;
            List<EnemyDef> enemies = new List<EnemyDef>();
            SpriteTile[] sleepFrames;
            Icon loadedTrayIcon;
            IntPtr trayIconHandle;

            FormDefinitions.PopulateForms(forms, keyToForm, out initialForm);
            FormDefinitions.PopulateEnemies(enemies);
            SpriteLoader.LoadAllSpritesheets(forms, enemies, out sleepFrames, out loadedTrayIcon, out trayIconHandle);

            RegistrySettingsService.LoadSettings(_settings);

            _world = new TaskbarWorld(
                forms,
                keyToForm,
                initialForm,
                enemies,
                sleepFrames,
                new Random(),
                _settings.Scale);

            bool startupEnabled = RegistrySettingsService.IsStartupEnabled();

            if (startupEnabled) {
                RegistrySettingsService.SetStartup(true);
            }

            _trayMenu = new TrayMenuController(_settings, startupEnabled, loadedTrayIcon, trayIconHandle);
            _trayMenu.OverlayToggled += OnToggleOverlay;
            _trayMenu.CombatToggled += OnToggleCombat;
            _trayMenu.ScaleSelected += SetScale;
            _trayMenu.SleepTimeoutSelected += SetSleepTimeout;
            _trayMenu.PauseToggled += OnTogglePause;
            _trayMenu.StartupToggled += OnToggleStartup;
            _trayMenu.ExitRequested += OnExit;

            _envMonitor = new DesktopEnvironmentMonitor();
            _stage = new OverlayStage(_settings.Scale);

            _stopwatch = Stopwatch.StartNew();
            _lastTickMs = _stopwatch.ElapsedMilliseconds;
            _idleMonitor = new IdleActivityMonitor(_lastTickMs, Cursor.Position);

            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += OnTick;
            _animTimer.Start();

            _keyboardHook = new GlobalKeyboardHook(OnGlobalKeyDown);

            Diagnostics.TrimMemory();
        }

        /// <summary>
        /// Marshals a clean shutdown onto the UI thread when signaled by a newly launched instance.
        /// </summary>
        public void RequestCleanExit() {
            if (_stage != null) {
                _stage.BeginInvokeOnUiThread(OnExit);
            }
        }

        private void SetScale(int newScale) {
            newScale = AppSettings.NormalizeScale(newScale);

            if (_settings.Scale == newScale) {
                return;
            }

            _settings.Scale = newScale;
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
            RegistrySettingsService.SaveSettings(_settings);
            Diagnostics.TrimMemory();
        }

        private void SetSleepTimeout(double seconds) {
            _settings.IdleSleepSeconds = AppSettings.NormalizeSleepTimeout(seconds);
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());

            if (_settings.IdleSleepSeconds < 0) {
                _world.Kirby.Sleep.Wake();
            }

            RegistrySettingsService.SaveSettings(_settings);
        }

        /// <summary>
        /// Resets the idle timer on keyboard/mouse activity and transitions Kirby out of sleep if napping.
        /// </summary>
        private void NotifyUserActivity() {
            if (_stopwatch != null && _idleMonitor != null) {
                _idleMonitor.RecordActivity(_stopwatch.ElapsedMilliseconds);
            }

            _world.Kirby.Sleep.Wake();
        }

        /// <summary>
        /// Main ~60 Hz update loop driving input idle detection, fullscreen auto-hide,
        /// and <see cref="TaskbarWorld.Tick"/>.
        /// </summary>
        private void OnTick(object sender, EventArgs e) {
            long nowMs = _stopwatch.ElapsedMilliseconds;
            double dtSeconds = JumpPhysicsState.ClampDeltaTime((nowMs - _lastTickMs) / 1000.0);
            _lastTickMs = nowMs;

            Point mouse = Cursor.Position;

            if (_idleMonitor.PollActivity(mouse)) {
                NotifyUserActivity();
            }
            else {
                _world.CheckIdleSleep(_idleMonitor.GetIdleSeconds(nowMs), _settings.IdleSleepSeconds);
            }

            if (_envMonitor.StepTick(_stage) && _settings.OverlayEnabled && !_envMonitor.IsFullscreenCached &&
                _stage != null) {
                _stage.RefreshTopmostOrder();
            }

            if (_stage == null) {
                return;
            }

            if (!_settings.OverlayEnabled || _envMonitor.IsFullscreenCached) {
                _stage.ClearAll(_settings.Scale);
                return;
            }

            _world.Tick(dtSeconds, mouse, _envMonitor.CachedWorkArea, _settings, _stage);
        }

        private void OnGlobalKeyDown(int vkCode, bool isInitialKeyDown) {
            bool wasSleeping = !_world.Kirby.Sleep.IsAwake;
            NotifyUserActivity();

            if (isInitialKeyDown && !_settings.IsPaused && !wasSleeping) {
                string keyName = KeyBindings.VkCodeToName(vkCode);

                if (keyName != null) {
                    _world.Kirby.HandleKeySwitch(keyName);
                }
            }
        }

        private void OnToggleOverlay() {
            _settings.OverlayEnabled = !_settings.OverlayEnabled;
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
        }

        private void OnToggleCombat() {
            _settings.CombatEnabled = !_settings.CombatEnabled;

            if (!_settings.CombatEnabled) {
                _world.DisableCombat(_stage, _settings.Scale);
            }

            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
            RegistrySettingsService.SaveSettings(_settings);
        }

        private void OnTogglePause() {
            _settings.IsPaused = !_settings.IsPaused;
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
        }

        private void OnToggleStartup() {
            bool nextState = !RegistrySettingsService.IsStartupEnabled();
            RegistrySettingsService.SetStartup(nextState);
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
        }

        private void OnExit() {
            if (_animTimer != null) {
                _animTimer.Stop();
            }

            if (_stage != null) {
                _stage.Dispose();
            }

            if (_keyboardHook != null) {
                _keyboardHook.Dispose();
            }

            if (_trayMenu != null) {
                _trayMenu.Dispose();
            }

            Application.Exit();
        }

    }

}