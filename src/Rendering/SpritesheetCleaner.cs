using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Offline spritesheet preparation utility (<c>--clean-sprites</c>) that strips opaque background and
    /// divider-grid colors from imported PNG spritesheets into true <c>alpha = 0</c> transparency.
    /// </summary>
    public static class SpritesheetCleaner {

        /// <summary>
        /// Determines whether a pixel in a newly imported (uncleaned) spritesheet belongs to the sheet's
        /// background or divider grid. Checks <c>alpha == 0</c>, the sheet's top-left pixel (<paramref name="bg0"/>),
        /// an optional sub-tile corner (<paramref name="bgCorner"/>), and the standard Spriters Resource sheet colors.
        /// </summary>
        internal static bool IsRawSheetBgColor(int c, int bg0, int bgCorner) {
            if (((c >> 24) & 0xFF) == 0) {
                return true;
            }

            int rgb = c & 0x00FFFFFF;

            if (((bg0 >> 24) & 0xFF) != 0 && rgb == (bg0 & 0x00FFFFFF)) {
                return true;
            }

            if (((bgCorner >> 24) & 0xFF) != 0 && rgb == (bgCorner & 0x00FFFFFF)) {
                return true;
            }

            if (rgb == 0x008080FF) {
                return true; // (128, 128, 255) outer sheet periwinkle background
            }

            if (rgb == 0x00709AD1) {
                return true; // (112, 154, 209) 1px blue divider grid
            }

            if (rgb == 0x009696FF) {
                return true; // (150, 150, 255) inner lavender sprite-box fill
            }

            if (rgb == 0x00004080) {
                return true; // (0, 64, 128) dark navy sprite-box fill / border
            }

            return false;
        }

        /// <summary>
        /// Scans all <c>*.png</c> files in <paramref name="spritesDirectory"/> and strips opaque background and
        /// grid colors into true <c>alpha = 0</c> transparency in-place. Already-transparent PNGs are skipped.
        /// Returns the number of PNG files that were modified.
        /// </summary>
        public static int StripSpritesheetBackgrounds(string spritesDirectory) {
            if (string.IsNullOrEmpty(spritesDirectory) || !Directory.Exists(spritesDirectory)) {
                return 0;
            }

            int modifiedCount = 0;
            string[] files = Directory.GetFiles(spritesDirectory, "*.png");

            for (int i = 0; i < files.Length; i++) {
                if (StripSingleSpritesheet(files[i])) {
                    modifiedCount++;
                }
            }

            return modifiedCount;
        }

        /// <summary>
        /// Strips opaque background and divider-grid pixels from a single spritesheet PNG file into <c>alpha = 0</c>.
        /// Idempotent: returns <c>false</c> without rewriting the file if all background pixels are already transparent.
        /// </summary>
        public static bool StripSingleSpritesheet(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return false;
            }

            byte[] fileBytes = File.ReadAllBytes(filePath);

            using (MemoryStream ms = new MemoryStream(fileBytes))
            using (Bitmap srcBmp = new Bitmap(ms))
            using (Bitmap editBmp = new Bitmap(srcBmp.Width, srcBmp.Height, PixelFormat.Format32bppArgb)) {
                int w = srcBmp.Width;
                int h = srcBmp.Height;
                Rectangle lockRect = new Rectangle(0, 0, w, h);

                BitmapData srcData = srcBmp.LockBits(lockRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData dstData = editBmp.LockBits(lockRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                bool modified = false;

                try {
                    int bg0 = Marshal.ReadInt32(srcData.Scan0, 0);

                    for (int y = 0; y < h; y++) {
                        int srcRow = y * srcData.Stride;
                        int dstRow = y * dstData.Stride;

                        for (int x = 0; x < w; x++) {
                            int c = Marshal.ReadInt32(srcData.Scan0, srcRow + (x << 2));

                            if (IsRawSheetBgColor(c, bg0, bg0)) {
                                if (c != 0) {
                                    modified = true;
                                }

                                Marshal.WriteInt32(dstData.Scan0, dstRow + (x << 2), 0);
                            }
                            else {
                                Marshal.WriteInt32(dstData.Scan0, dstRow + (x << 2), c);
                            }
                        }
                    }
                }
                finally {
                    srcBmp.UnlockBits(srcData);
                    editBmp.UnlockBits(dstData);
                }

                if (modified) {
                    editBmp.Save(filePath, ImageFormat.Png);
                }

                return modified;
            }
        }

    }

}