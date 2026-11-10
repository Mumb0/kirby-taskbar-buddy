using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Wraps a GDI+ <see cref="Bitmap"/> locked via <see cref="Bitmap.LockBits"/> for fast, zero-allocation
    /// unmanaged pixel reads. Locks only the required upper-left region (<c>lockWidth x lockHeight</c>)
    /// so GDI+ does not allocate multi-megabyte conversion buffers for unused lower sections of large sheets.
    /// </summary>
    internal sealed class FastSheet : IDisposable {

        private Stream _stream;
        private Bitmap _bmp;
        private BitmapData _data;
        private readonly IntPtr _scan0;
        private readonly int _stride;
        public readonly int Width;
        public readonly int Height;

        public FastSheet(Stream stream, Bitmap bmp, int lockWidth, int lockHeight) {
            _stream = stream;
            _bmp = bmp;
            Width = Math.Min(bmp.Width, Math.Max(1, lockWidth));
            Height = Math.Min(bmp.Height, Math.Max(1, lockHeight));
            _data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            _scan0 = _data.Scan0;
            _stride = _data.Stride;
        }

        public int GetPixel(int x, int y) {
            return Marshal.ReadInt32(_scan0, y * _stride + (x << 2));
        }

        public void Dispose() {
            if (_bmp != null) {
                if (_data != null) {
                    try {
                        _bmp.UnlockBits(_data);
                    }
                    catch (ExternalException ex) {
                        Diagnostics.LogWarning("FastSheet.Dispose", ex);
                    }
                    catch (ArgumentException ex) {
                        Diagnostics.LogWarning("FastSheet.Dispose", ex);
                    }

                    _data = null;
                }

                _bmp.Dispose();
                _bmp = null;
            }

            if (_stream != null) {
                _stream.Dispose();
                _stream = null;
            }
        }

    }

}