using System;
using System.Drawing;
using System.Windows.Forms;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Builds and manages the system tray <see cref="NotifyIcon"/> and its context menu items
    /// (overlay visibility, combat toggle, scale submenu, sleep timer submenu, key-switching pause,
    /// Windows startup registration, and application exit).
    /// </summary>
    public sealed class TrayMenuController : IDisposable {

        private readonly NotifyIcon _trayIcon;
        private readonly MenuItem _overlayItem;
        private readonly MenuItem _combatItem;
        private readonly MenuItem _pauseItem;
        private readonly MenuItem _startupItem;
        private readonly MenuItem _scale1xItem;
        private readonly MenuItem _scale2xItem;
        private readonly MenuItem _scale3xItem;
        private readonly MenuItem _scale4xItem;
        private readonly MenuItem[] _sleepMenuItems;
        private IntPtr _trayIconHandle;

        public event Action OverlayToggled;

        public event Action CombatToggled;

        public event Action<int> ScaleSelected;

        public event Action<double> SleepTimeoutSelected;

        public event Action PauseToggled;

        public event Action StartupToggled;

        public event Action ExitRequested;

        public TrayMenuController(
            AppSettings settings,
            bool startupEnabled,
            Icon trayIcon,
            IntPtr trayIconHandle) {
            _trayIconHandle = trayIconHandle;

            _overlayItem = new MenuItem("Show Foreground Taskbar Kirby", OnToggleOverlay)
            {
                Checked = settings.OverlayEnabled
            };
            _combatItem = new MenuItem("Combat", OnToggleCombat)
            {
                Checked = settings.CombatEnabled
            };
            _pauseItem = new MenuItem("Pause Key Switching", OnTogglePause)
            {
                Checked = settings.IsPaused
            };
            _startupItem = new MenuItem("Start with Windows", OnToggleStartup)
            {
                Checked = startupEnabled
            };

            _scale1xItem = new MenuItem("1x (Small)", (s, e) => OnSelectScale(1)) { Checked = settings.Scale == 1 };
            _scale2xItem = new MenuItem("2x (Default)", (s, e) => OnSelectScale(2)) { Checked = settings.Scale == 2 };
            _scale3xItem = new MenuItem("3x (Large)", (s, e) => OnSelectScale(3)) { Checked = settings.Scale == 3 };
            _scale4xItem = new MenuItem("4x (Extra Large)", (s, e) => OnSelectScale(4)) { Checked = settings.Scale == 4 };
            MenuItem scaleMenu = new MenuItem("Kirby Size",
                new[] { _scale1xItem, _scale2xItem, _scale3xItem, _scale4xItem });

            _sleepMenuItems = new MenuItem[AppSettings.SleepTimeoutLabels.Length];

            for (int i = 0; i < AppSettings.SleepTimeoutLabels.Length; i++) {
                double val = AppSettings.SleepTimeoutValues[i];
                _sleepMenuItems[i] = new MenuItem(AppSettings.SleepTimeoutLabels[i], (s, e) => OnSelectSleepTimeout(val))
                {
                    Checked = settings.IdleSleepSeconds == val
                };
            }

            MenuItem sleepMenu = new MenuItem("Sleep Timer", _sleepMenuItems);

            ContextMenu menu = new ContextMenu(new[]
            {
                new MenuItem("Kirby Taskbar Buddy") { Enabled = false },
                new MenuItem("-"),
                _overlayItem,
                _combatItem,
                scaleMenu,
                sleepMenu,
                _pauseItem,
                _startupItem,
                new MenuItem("-"),
                new MenuItem("Exit", OnExit)
            });

            _trayIcon = new NotifyIcon
            {
                Icon = trayIcon ?? SystemIcons.Application,
                Text = "Kirby Taskbar Buddy",
                ContextMenu = menu,
                Visible = true
            };
        }

        /// <summary>
        /// Synchronizes menu item checkmarks and tooltip text with <paramref name="settings"/> and <paramref name="startupEnabled"/>.
        /// </summary>
        public void SyncState(AppSettings settings, bool startupEnabled) {
            if (settings == null) {
                return;
            }

            _overlayItem.Checked = settings.OverlayEnabled;
            _combatItem.Checked = settings.CombatEnabled;
            _pauseItem.Checked = settings.IsPaused;
            _startupItem.Checked = startupEnabled;

            _scale1xItem.Checked = settings.Scale == 1;
            _scale2xItem.Checked = settings.Scale == 2;
            _scale3xItem.Checked = settings.Scale == 3;
            _scale4xItem.Checked = settings.Scale == 4;

            for (int i = 0; i < _sleepMenuItems.Length; i++) {
                _sleepMenuItems[i].Checked = AppSettings.SleepTimeoutValues[i] == settings.IdleSleepSeconds;
            }

            _trayIcon.Text = settings.IsPaused ? "Kirby Taskbar Buddy (Paused)" : "Kirby Taskbar Buddy";
        }

        private void OnToggleOverlay(object sender, EventArgs e) {
            Action handler = OverlayToggled;

            if (handler != null) {
                handler();
            }
        }

        private void OnToggleCombat(object sender, EventArgs e) {
            Action handler = CombatToggled;

            if (handler != null) {
                handler();
            }
        }

        private void OnSelectScale(int scale) {
            Action<int> handler = ScaleSelected;

            if (handler != null) {
                handler(scale);
            }
        }

        private void OnSelectSleepTimeout(double seconds) {
            Action<double> handler = SleepTimeoutSelected;

            if (handler != null) {
                handler(seconds);
            }
        }

        private void OnTogglePause(object sender, EventArgs e) {
            Action handler = PauseToggled;

            if (handler != null) {
                handler();
            }
        }

        private void OnToggleStartup(object sender, EventArgs e) {
            Action handler = StartupToggled;

            if (handler != null) {
                handler();
            }
        }

        private void OnExit(object sender, EventArgs e) {
            Action handler = ExitRequested;

            if (handler != null) {
                handler();
            }
        }

        public void Dispose() {
            if (_trayIcon != null) {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            if (_trayIconHandle != IntPtr.Zero) {
                NativeMethods.DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
            }
        }

    }

}