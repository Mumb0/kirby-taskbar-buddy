using System;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Coordinates the three Win32 layered overlay windows ordered back-to-front:
    /// <see cref="EnemyLayer"/> -> <see cref="KirbyLayer"/> -> <see cref="VfxLayer"/>.
    /// </summary>
    public sealed class OverlayStage : IDisposable {

        private readonly KirbyOverlayWindow _enemyOverlay;
        private readonly KirbyOverlayWindow _kirbyOverlay;
        private readonly KirbyOverlayWindow _vfxOverlay;

        public ISpriteLayer EnemyLayer {
            get { return _enemyOverlay; }
        }

        public ISpriteLayer KirbyLayer {
            get { return _kirbyOverlay; }
        }

        public ISpriteLayer VfxLayer {
            get { return _vfxOverlay; }
        }

        /// <summary>
        /// Creates, initializes, clears (<c>SourceConstantAlpha = 0</c> off-screen), and shows all three
        /// layered windows in back-to-front Z-order so Windows/DWM never maps an uninitialized HWND.
        /// </summary>
        public OverlayStage(int initialScale = 2) {
            _enemyOverlay = CreateLayer(initialScale);
            _kirbyOverlay = CreateLayer(initialScale);
            _vfxOverlay = CreateLayer(initialScale);
        }

        private static KirbyOverlayWindow CreateLayer(int initialScale) {
            KirbyOverlayWindow window = new KirbyOverlayWindow();
            IntPtr unusedHandle = window.Handle;
            window.ClearOverlay(initialScale);
            window.Show();
            return window;
        }

        /// <summary>
        /// Parks all three overlay windows off-screen with zero opacity.
        /// </summary>
        public void ClearAll(int scale) {
            if (_kirbyOverlay != null && !_kirbyOverlay.IsCleared) {
                _kirbyOverlay.ClearOverlay(scale);
            }

            if (_enemyOverlay != null && !_enemyOverlay.IsCleared) {
                _enemyOverlay.ClearOverlay(scale);
            }

            if (_vfxOverlay != null && !_vfxOverlay.IsCleared) {
                _vfxOverlay.ClearOverlay(scale);
            }
        }

        /// <summary>
        /// Re-asserts <c>HWND_TOPMOST</c> when the foreground window changes so clicking the taskbar
        /// does not cover Kirby or active enemies, ordered back-to-front:
        /// <see cref="EnemyLayer"/> -> <see cref="KirbyLayer"/> -> <see cref="VfxLayer"/>.
        /// </summary>
        public void RefreshTopmostOrder() {
            if (_kirbyOverlay == null || !_kirbyOverlay.IsHandleCreated) {
                return;
            }

            IntPtr hwndTopMost = new IntPtr(-1);

            if (_enemyOverlay != null && _enemyOverlay.IsHandleCreated) {
                NativeMethods.SetWindowPos(_enemyOverlay.Handle, hwndTopMost, 0, 0, 0, 0, NativeMethods.TOPMOST_FLAGS);
            }

            NativeMethods.SetWindowPos(_kirbyOverlay.Handle, hwndTopMost, 0, 0, 0, 0, NativeMethods.TOPMOST_FLAGS);

            if (_vfxOverlay != null && _vfxOverlay.IsHandleCreated) {
                NativeMethods.SetWindowPos(_vfxOverlay.Handle, hwndTopMost, 0, 0, 0, 0, NativeMethods.TOPMOST_FLAGS);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="hwnd"/> belongs to any of the stage's overlay windows.
        /// </summary>
        public bool OwnsWindowHandle(IntPtr hwnd) {
            if (hwnd == IntPtr.Zero) {
                return false;
            }

            if (_kirbyOverlay != null && _kirbyOverlay.IsHandleCreated && hwnd == _kirbyOverlay.Handle) {
                return true;
            }

            if (_enemyOverlay != null && _enemyOverlay.IsHandleCreated && hwnd == _enemyOverlay.Handle) {
                return true;
            }

            if (_vfxOverlay != null && _vfxOverlay.IsHandleCreated && hwnd == _vfxOverlay.Handle) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Marshals <paramref name="action"/> onto the UI thread owning the overlay windows.
        /// </summary>
        public void BeginInvokeOnUiThread(Action action) {
            if (action != null && _kirbyOverlay != null && _kirbyOverlay.IsHandleCreated) {
                _kirbyOverlay.BeginInvoke(action);
            }
        }

        public void Dispose() {
            if (_kirbyOverlay != null) {
                _kirbyOverlay.Close();
            }

            if (_enemyOverlay != null) {
                _enemyOverlay.Close();
            }

            if (_vfxOverlay != null) {
                _vfxOverlay.Close();
            }
        }

    }

}