using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KirbyTaskbarBuddy
{
    /// <summary>
    /// Click-through, per-pixel alpha-blended Win32 layered window that renders Kirby on the desktop.
    /// Uses a single persistent GDI DIBSection buffer to avoid allocating Bitmaps or HBITMAPs on the render loop.
    /// </summary>
    public class KirbyOverlayWindow : Form
    {
        private IntPtr _memDc = IntPtr.Zero;
        private IntPtr _hDib = IntPtr.Zero;
        private IntPtr _oldBmp = IntPtr.Zero;
        private IntPtr _dibBits = IntPtr.Zero;
        private int _dibSize = 0;

        // Tracks the last sprite tile and scale written into _dibBits so we only
        // touch pixel memory when the animation frame actually changes (~6-15 Hz),
        // rather than on every 1-pixel movement tick (60 Hz).
        private SpriteTile _renderedTile = null;
        private int _renderedScale = 0;
        private Rectangle _dirtyRect = Rectangle.Empty;

        public KirbyOverlayWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(128, 128);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                            | NativeMethods.WS_EX_TRANSPARENT
                            | NativeMethods.WS_EX_TOPMOST
                            | NativeMethods.WS_EX_TOOLWINDOW
                            | NativeMethods.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        /// <summary>
        /// Releases the unmanaged GDI device context and DIBSection bitmap handle.
        /// </summary>
        private void FreeDib()
        {
            if (_memDc != IntPtr.Zero)
            {
                if (_oldBmp != IntPtr.Zero)
                {
                    NativeMethods.SelectObject(_memDc, _oldBmp);
                    _oldBmp = IntPtr.Zero;
                }
                if (_hDib != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(_hDib);
                    _hDib = IntPtr.Zero;
                }
                NativeMethods.DeleteDC(_memDc);
                _memDc = IntPtr.Zero;
            }
            _dibBits = IntPtr.Zero;
            _dibSize = 0;
            _renderedTile = null;
            _dirtyRect = Rectangle.Empty;
        }

        /// <summary>
        /// Lazily allocates (or resizes when the user switches between 2x/3x/4x scale) a single
        /// 32-bpp top-down GDI DIBSection whose raw pixel pointer (<see cref="_dibBits"/>) can be written directly.
        /// </summary>
        private void EnsureDib(int targetSize)
        {
            if (_memDc != IntPtr.Zero && _dibSize == targetSize && _dibBits != IntPtr.Zero) return;

            FreeDib();

            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            try
            {
                _memDc = NativeMethods.CreateCompatibleDC(screenDc);
                NativeMethods.BITMAPINFO bmi = new NativeMethods.BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = targetSize;
                // Negative height tells GDI to create a top-down DIB with origin (0,0) at top-left.
                bmi.bmiHeader.biHeight = -targetSize;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0; // BI_RGB

                _hDib = NativeMethods.CreateDIBSection(_memDc, ref bmi, 0, out _dibBits, IntPtr.Zero, 0);
                if (_hDib != IntPtr.Zero)
                {
                    _oldBmp = NativeMethods.SelectObject(_memDc, _hDib);
                    _dibSize = targetSize;
                }
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        /// <summary>
        /// Updates the layered window's position and (when the animation frame or scale changes)
        /// nearest-neighbor blits the <see cref="SpriteTile"/> into the persistent DIBSection buffer.
        /// </summary>
        public void UpdateSpriteFrame(SpriteTile tile, int scale, int screenX, int screenY)
        {
            if (IsDisposed || !IsHandleCreated || tile == null) return;

            int targetSize = NativeMethods.TILE_SIZE * scale;
            EnsureDib(targetSize);
            if (_memDc == IntPtr.Zero || _dibBits == IntPtr.Zero) return;

            // Only re-rasterize pixels into _dibBits when the displayed sprite tile or scale changes.
            if (tile != _renderedTile || scale != _renderedScale)
            {
                // Clear only the bounding box dirtied by the previous frame instead of zeroing the entire canvas.
                if (!_dirtyRect.IsEmpty)
                {
                    int clearRight = Math.Min(targetSize, _dirtyRect.Right);
                    int clearBottom = Math.Min(targetSize, _dirtyRect.Bottom);
                    for (int y = Math.Max(0, _dirtyRect.Top); y < clearBottom; y++)
                    {
                        int rowBase = (y * targetSize) << 2;
                        for (int x = Math.Max(0, _dirtyRect.Left); x < clearRight; x++)
                        {
                            Marshal.WriteInt32(_dibBits, rowBase + (x << 2), 0);
                        }
                    }
                }

                // Nearest-neighbor scale each non-transparent source pixel into a (scale x scale) block.
                int startX = tile.MinX * scale;
                int startY = tile.MinY * scale;
                int srcIdx = 0;
                for (int sy = 0; sy < tile.Height; sy++)
                {
                    int baseY = startY + sy * scale;
                    for (int sx = 0; sx < tile.Width; sx++)
                    {
                        int c = tile.PremulPixels[srcIdx++];
                        if (c != 0)
                        {
                            int baseX = startX + sx * scale;
                            for (int dy = 0; dy < scale; dy++)
                            {
                                int rowOffset = ((baseY + dy) * targetSize + baseX) << 2;
                                for (int dx = 0; dx < scale; dx++)
                                {
                                    Marshal.WriteInt32(_dibBits, rowOffset + (dx << 2), c);
                                }
                            }
                        }
                    }
                }

                _dirtyRect = new Rectangle(startX, startY, tile.Width * scale, tile.Height * scale);
                _renderedTile = tile;
                _renderedScale = scale;
            }

            NativeMethods.SIZE size = new NativeMethods.SIZE { cx = targetSize, cy = targetSize };
            NativeMethods.POINT pointSource = new NativeMethods.POINT { x = 0, y = 0 };
            NativeMethods.POINT topPos = new NativeMethods.POINT { x = screenX, y = screenY };
            NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            NativeMethods.UpdateLayeredWindow(
                Handle,
                IntPtr.Zero,
                ref topPos,
                ref size,
                _memDc,
                ref pointSource,
                0,
                ref blend,
                NativeMethods.ULW_ALPHA);
        }

        protected override void Dispose(bool disposing)
        {
            FreeDib();
            base.Dispose(disposing);
        }
    }
}
