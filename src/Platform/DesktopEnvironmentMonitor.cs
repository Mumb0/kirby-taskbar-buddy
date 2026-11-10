using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Periodically queries the primary monitor's <see cref="Screen.WorkingArea"/> and checks whether
    /// a fullscreen user application is covering the taskbar, reusing a single <see cref="StringBuilder"/>
    /// so the 60 Hz tick loop remains allocation-free.
    /// </summary>
    public sealed class DesktopEnvironmentMonitor {

        public const int POLL_INTERVAL_TICKS = 15;

        private readonly StringBuilder _classNameSb = new StringBuilder(64);
        private int _tickCounter;
        private Rectangle _cachedWorkArea;
        private bool _isFullscreenCached;
        private IntPtr _lastForegroundWindow = IntPtr.Zero;

        public Rectangle CachedWorkArea {
            get { return _cachedWorkArea; }
        }

        public bool IsFullscreenCached {
            get { return _isFullscreenCached; }
        }

        public DesktopEnvironmentMonitor() {
            Screen primaryScreen = Screen.PrimaryScreen;
            _cachedWorkArea = primaryScreen != null ? primaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080);
        }

        /// <summary>
        /// Advances the throttling counter and, every <see cref="POLL_INTERVAL_TICKS"/> ticks (~240ms),
        /// refreshes <see cref="CachedWorkArea"/>, <see cref="IsFullscreenCached"/>, and reports whether
        /// the foreground window handle changed since the previous check.
        /// </summary>
        public bool StepTick(OverlayStage stage) {
            _tickCounter++;

            if (_tickCounter < POLL_INTERVAL_TICKS) {
                return false;
            }

            _tickCounter = 0;
            Screen primaryScreen = Screen.PrimaryScreen;

            if (primaryScreen != null) {
                _cachedWorkArea = primaryScreen.WorkingArea;
            }

            IntPtr fg = NativeMethods.GetForegroundWindow();
            _isFullscreenCached = IsFullscreenAppActive(fg, _cachedWorkArea, stage);

            if (fg != _lastForegroundWindow) {
                _lastForegroundWindow = fg;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="className"/> belongs to the Windows desktop shell, taskbar,
        /// Start menu, Alt+Tab switcher, or XAML system island rather than a fullscreen user application.
        /// </summary>
        public static bool IsShellOrOverlayWindowClass(string className) {
            if (string.IsNullOrEmpty(className)) {
                return false;
            }

            return className == "Progman"
                   || className == "WorkerW"
                   || className == "Shell_TrayWnd"
                   || className == "Shell_SecondaryTrayWnd"
                   || className == "Windows.UI.Core.CoreWindow"
                   || className == "XamlExplorerHostIslandWindow"
                   || className == "MultitaskingViewFrame"
                   || className == "ForegroundStaging";
        }

        /// <summary>
        /// Returns <c>true</c> if the window extended style bits (<c>GWL_EXSTYLE</c>) indicate a non-activating,
        /// click-through, or floating tool overlay rather than a standard fullscreen application window.
        /// </summary>
        public static bool IsNonFullscreenOverlayExStyle(int exStyle) {
            const int ignoreMask = NativeMethods.WS_EX_TRANSPARENT
                                   | NativeMethods.WS_EX_NOACTIVATE
                                   | NativeMethods.WS_EX_TOOLWINDOW;
            return (exStyle & ignoreMask) != 0;
        }

        /// <summary>
        /// Checks whether the foreground window covers the entire primary monitor (including the taskbar area)
        /// while ignoring non-visible/minimized windows, transparent/tool overlays, and Windows shell classes.
        /// </summary>
        public bool IsFullscreenAppActive(IntPtr fg, Rectangle work, OverlayStage stage) {
            if (fg == IntPtr.Zero) {
                return false;
            }

            if (stage != null && stage.OwnsWindowHandle(fg)) {
                return false;
            }

            if (!NativeMethods.IsWindowVisible(fg) || NativeMethods.IsIconic(fg)) {
                return false;
            }

            int exStyle = NativeMethods.GetWindowLong(fg, NativeMethods.GWL_EXSTYLE);

            if (IsNonFullscreenOverlayExStyle(exStyle)) {
                return false;
            }

            _classNameSb.Length = 0;

            if (NativeMethods.GetClassName(fg, _classNameSb, _classNameSb.Capacity) > 0) {
                string cls = _classNameSb.ToString();

                if (IsShellOrOverlayWindowClass(cls)) {
                    return false;
                }
            }

            NativeMethods.RECT rect;

            if (NativeMethods.GetWindowRect(fg, out rect)) {
                Screen primaryScreen = Screen.PrimaryScreen;

                if (primaryScreen == null) {
                    return false;
                }

                Rectangle screen = primaryScreen.Bounds;

                if (rect.Left <= screen.Left && rect.Top <= screen.Top &&
                    rect.Right >= screen.Right && rect.Bottom >= screen.Bottom &&
                    rect.Bottom > work.Bottom) {
                    return true;
                }
            }

            return false;
        }

    }

}