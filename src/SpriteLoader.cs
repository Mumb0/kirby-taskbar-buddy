using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KirbyTaskbarBuddy
{
    /// <summary>
    /// Compact, tightly-cropped representation of a single sprite frame aligned within the 80x80 logical canvas.
    /// Stores only the non-transparent bounding box (<see cref="MinX"/>, <see cref="MinY"/>, <see cref="Width"/>, <see cref="Height"/>)
    /// and 32-bit premultiplied-ARGB pixels ready for direct blitting into a GDI DIBSection.
    /// </summary>
    public sealed class SpriteTile
    {
        public int MinX;
        public int MinY;
        public int Width;
        public int Height;
        public int[] PremulPixels;
    }

    /// <summary>
    /// Wraps a GDI+ <see cref="Bitmap"/> locked via <see cref="Bitmap.LockBits"/> for fast, zero-allocation
    /// unmanaged pixel reads. Locks only the required upper-left region (<c>lockWidth x lockHeight</c>)
    /// so GDI+ does not allocate multi-megabyte conversion buffers for unused lower sections of large sheets.
    /// </summary>
    internal sealed class FastSheet : IDisposable
    {
        private Stream _stream;
        private Bitmap _bmp;
        private BitmapData _data;
        private IntPtr _scan0;
        private int _stride;
        public readonly int Width;
        public readonly int Height;

        public FastSheet(Stream stream, Bitmap bmp, int lockWidth, int lockHeight)
        {
            _stream = stream;
            _bmp = bmp;
            Width = Math.Min(bmp.Width, Math.Max(1, lockWidth));
            Height = Math.Min(bmp.Height, Math.Max(1, lockHeight));
            _data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            _scan0 = _data.Scan0;
            _stride = _data.Stride;
        }

        public int GetPixel(int x, int y)
        {
            return Marshal.ReadInt32(_scan0, y * _stride + (x << 2));
        }

        public void Dispose()
        {
            if (_bmp != null)
            {
                if (_data != null)
                {
                    try { _bmp.UnlockBits(_data); } catch { }
                    _data = null;
                }
                _bmp.Dispose();
                _bmp = null;
            }
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }
    }

    /// <summary>
    /// Extracts, background-keys, floor-aligns, and composites Kirby sprite frames from embedded PNG spritesheets.
    /// </summary>
    public static class SpriteLoader
    {
        /// <summary>
        /// Decodes all ability and sleep spritesheets once at startup into compact <see cref="SpriteTile"/> arrays,
        /// and extracts the 24x24 system tray icon from <c>kirby-normal.png</c>.
        /// </summary>
        public static void LoadAllSpritesheets(
            List<FormDef> forms,
            out SpriteTile[] sleepFrames,
            out Icon trayIcon,
            out IntPtr trayIconHandle)
        {
            sleepFrames = null;
            trayIcon = null;
            trayIconHandle = IntPtr.Zero;

            // Single reusable 80x80 scratch canvas shared across all frame extractions at startup.
            int[] scratch = new int[NativeMethods.TILE_SIZE * NativeMethods.TILE_SIZE];

            foreach (FormDef form in forms)
            {
                int reqW = 64, reqH = 160;
                ExpandBounds(form.Walk, ref reqW, ref reqH);
                ExpandBounds(form.Guard, ref reqW, ref reqH);
                ExpandBounds(form.Jump, ref reqW, ref reqH);
                ExpandBounds(form.Stand, ref reqW, ref reqH);

                using (FastSheet rawSheet = OpenSpriteSheet(form.FileName, reqW, reqH))
                {
                    if (rawSheet == null) continue;
                    int bg0 = rawSheet.GetPixel(0, 0);

                    if (form.Name == "normal" && trayIcon == null)
                    {
                        ExtractTrayIconFromNormalSheet(rawSheet, bg0, out trayIcon, out trayIconHandle);
                    }

                    LoadClip(rawSheet, form.Walk, bg0, scratch, true);
                    LoadClip(rawSheet, form.Guard, bg0, scratch, false);
                    LoadClip(rawSheet, form.Jump, bg0, scratch, false);
                    LoadClip(rawSheet, form.Stand, bg0, scratch, false);
                }
            }

            int sleepW = 1, sleepH = 1;
            ExpandBounds(FormDefinitions.SleepCoords, ref sleepW, ref sleepH);
            using (FastSheet sleepSheet = OpenSpriteSheet("kirby-sleep.png", sleepW, sleepH))
            {
                if (sleepSheet != null)
                {
                    int bg0 = sleepSheet.GetPixel(0, 0);
                    sleepFrames = new SpriteTile[FormDefinitions.SleepCoords.Length];
                    for (int i = 0; i < FormDefinitions.SleepCoords.Length; i++)
                    {
                        Rectangle sr = FormDefinitions.SleepCoords[i];
                        int sCorner = sleepSheet.GetPixel(sr.X, sr.Y);
                        sleepFrames[i] = ExtractCustomPivotTile(sleepSheet, sr, bg0, sCorner, 18, -1, scratch);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts all unique <see cref="AnimClip.SourceRects"/> into <see cref="AnimClip.RawFrames"/> according to
        /// <see cref="AnimClip.Pivot"/>, and builds the playback <see cref="AnimClip.Frames"/> array (reusing tile
        /// references when an index <see cref="AnimClip.Sequence"/> such as a ping-pong loop is defined).
        /// </summary>
        internal static void LoadClip(FastSheet rawSheet, AnimClip clip, int bg0, int[] scratch, bool useFirstRectCorner = false)
        {
            if (rawSheet == null || clip == null || clip.SourceRects == null || clip.SourceRects.Length == 0) return;

            Rectangle[] rects = clip.SourceRects;
            if (clip.Pivot == PivotMode.JumpArc)
            {
                clip.RawFrames = ExtractJumpTiles(rawSheet, rects, bg0, scratch);
            }
            else
            {
                clip.RawFrames = new SpriteTile[rects.Length];
                int firstCorner = rawSheet.GetPixel(rects[0].X, rects[0].Y);
                for (int i = 0; i < rects.Length; i++)
                {
                    Rectangle r = rects[i];
                    int bgCorner = useFirstRectCorner ? firstCorner : rawSheet.GetPixel(r.X, r.Y);
                    switch (clip.Pivot)
                    {
                        case PivotMode.MirrorGuard:
                            clip.RawFrames[i] = ExtractMirrorGuardTile(rawSheet, r, bg0, scratch);
                            break;
                        case PivotMode.GuardBottom2Rows:
                            clip.RawFrames[i] = ExtractAlignedTile(rawSheet, r, bg0, bgCorner, scratch, true);
                            break;
                        case PivotMode.Center:
                            clip.RawFrames[i] = ExtractCenteredTile(rawSheet, r, bg0, bgCorner, scratch);
                            break;
                        case PivotMode.CustomPivot:
                            clip.RawFrames[i] = ExtractCustomPivotTile(rawSheet, r, bg0, bgCorner, clip.CustomPivotX, clip.CustomPivotY, scratch);
                            break;
                        case PivotMode.FeetBottom4Rows:
                        default:
                            clip.RawFrames[i] = ExtractAlignedTile(rawSheet, r, bg0, bgCorner, scratch, false);
                            break;
                    }
                }
            }

            if (clip.Sequence != null && clip.Sequence.Length > 0 && clip.RawFrames != null)
            {
                clip.Frames = new SpriteTile[clip.Sequence.Length];
                for (int i = 0; i < clip.Sequence.Length; i++)
                {
                    int idx = clip.Sequence[i];
                    if (idx >= 0 && idx < clip.RawFrames.Length)
                    {
                        clip.Frames[i] = clip.RawFrames[idx];
                    }
                }
            }
            else
            {
                clip.Frames = clip.RawFrames;
            }
        }

        private static void ExpandBounds(AnimClip clip, ref int maxW, ref int maxH)
        {
            if (clip != null) ExpandBounds(clip.SourceRects, ref maxW, ref maxH);
        }

        private static void ExpandBounds(Rectangle[] rects, ref int maxW, ref int maxH)
        {
            if (rects == null) return;
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].Right > maxW) maxW = rects[i].Right;
                if (rects[i].Bottom > maxH) maxH = rects[i].Bottom;
            }
        }

        private static FastSheet OpenSpriteSheet(string fileName, int reqW, int reqH)
        {
            try
            {
                Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fileName);
                if (resStream != null)
                {
                    return new FastSheet(resStream, new Bitmap(resStream), reqW, reqH);
                }
            }
            catch { }

            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(exeDir, "sprites", fileName);
            if (!File.Exists(path)) path = Path.Combine(exeDir, fileName);
            if (File.Exists(path)) return new FastSheet(null, new Bitmap(path), reqW, reqH);
            return null;
        }

        /// <summary>
        /// Packs an (A, R, G, B) color into 32-bit premultiplied ARGB format required by Win32 UpdateLayeredWindow.
        /// </summary>
        internal static int MakePremulArgb(int a, int r, int g, int b)
        {
            if (a <= 0) return 0;
            if (a >= 255) return (255 << 24) | (r << 16) | (g << 8) | b;
            int pr = (r * a + 127) / 255;
            int pg = (g * a + 127) / 255;
            int pb = (b * a + 127) / 255;
            return (a << 24) | (pr << 16) | (pg << 8) | pb;
        }

        /// <summary>
        /// Determines whether a spritesheet pixel belongs to the sheet's background / grid lines.
        /// Checks alpha=0, the sheet's top-left pixel (<paramref name="bg0"/>), the sub-tile's top-left
        /// corner pixel (<paramref name="bgCorner"/>), and the two standard Spriters Resource grid colors.
        /// </summary>
        internal static bool IsBgColor(int c, int bg0, int bgCorner)
        {
            if (((c >> 24) & 0xFF) == 0) return true;
            int rgb = c & 0x00FFFFFF;
            if (rgb == (bg0 & 0x00FFFFFF)) return true;
            if (rgb == (bgCorner & 0x00FFFFFF)) return true;
            if (rgb == 0x00709AD1) return true; // (112, 154, 209) blue divider grid
            if (rgb == 0x009696FF) return true; // (150, 150, 255) lavender background
            return false;
        }

        /// <summary>
        /// Crops the non-transparent bounding box out of the 80x80 scratch buffer into a compact <see cref="SpriteTile"/>.
        /// </summary>
        internal static SpriteTile BuildSpriteTile(int[] scratchTile)
        {
            int tileSize = NativeMethods.TILE_SIZE;
            int minX = tileSize, maxX = -1, minY = tileSize, maxY = -1;
            for (int y = 0; y < tileSize; y++)
            {
                int row = y * tileSize;
                for (int x = 0; x < tileSize; x++)
                {
                    if (scratchTile[row + x] != 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return new SpriteTile { MinX = 0, MinY = 0, Width = 1, Height = 1, PremulPixels = new int[1] };
            }

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            int[] pixels = new int[w * h];
            for (int sy = 0; sy < h; sy++)
            {
                Array.Copy(scratchTile, (minY + sy) * tileSize + minX, pixels, sy * w, w);
            }
            return new SpriteTile { MinX = minX, MinY = minY, Width = w, Height = h, PremulPixels = pixels };
        }

        /// <summary>
        /// Extracts a walk, stand, or guard frame and automatically aligns Kirby's feet to
        /// <c>(PIVOT_X, PIVOT_Y)</c> so abilities with tall hats or wide weapons don't shift his body position.
        /// </summary>
        private static SpriteTile ExtractAlignedTile(FastSheet rawSheet, Rectangle r, int bg0, int bgCorner, int[] scratch, bool isGuard = false)
        {
            // Pass 1: Find the lowest non-background row (maxY) corresponding to the bottom of Kirby's feet.
            int maxY = r.Height - 1;
            for (int dy = 0; dy < r.Height; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bgCorner)) maxY = dy;
                }
            }

            // Pass 2: Scan only the bottom 4 rows (maxY - 3 .. maxY) to find the horizontal span of Kirby's feet.
            // This ignores wide hats/parasols/hammers higher up on the sprite that would otherwise skew horizontal centering.
            int feetMinX = r.Width, feetMaxX = 0;
            int startY = Math.Max(0, maxY - 3);
            for (int dy = startY; dy <= maxY; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bgCorner))
                    {
                        if ((!isGuard || dy >= maxY - 1) && dx < feetMinX) feetMinX = dx;
                        if (dx > feetMaxX) feetMaxX = dx;
                    }
                }
            }

            int bodyPivotX = (feetMinX + feetMaxX) / 2;
            int destX = NativeMethods.PIVOT_X - bodyPivotX;
            int destY = NativeMethods.PIVOT_Y - maxY;

            Array.Clear(scratch, 0, scratch.Length);
            for (int dy = 0; dy < r.Height; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bgCorner))
                    {
                        int tx = destX + dx;
                        int ty = destY + dy;
                        if (tx >= 0 && tx < NativeMethods.TILE_SIZE && ty >= 0 && ty < NativeMethods.TILE_SIZE)
                        {
                            scratch[ty * NativeMethods.TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                        }
                    }
                }
            }
            return BuildSpriteTile(scratch);
        }

        /// <summary>
        /// Synthesizes Mirror Kirby's defensive barrier by compositing three separate spritesheet elements:
        /// 1. Guarding Mirror Kirby at his floor-aligned pivot (<c>37, 880, 31, 27</c>),
        /// 2. The outer reflective bubble rim (<c>2, 877, 32, 34</c>) at 96% opacity, and
        /// 3. One of 15 scrolling rainbow gradient tiles (<paramref name="rainRect"/>) blended at 45% over Kirby and 68% over the interior.
        /// </summary>
        private static SpriteTile ExtractMirrorGuardTile(FastSheet rawSheet, Rectangle rainRect, int bg0, int[] scratch)
        {
            Rectangle kirbyRect = new Rectangle(37, 880, 31, 27);
            Rectangle bubbleRect = new Rectangle(2, 877, 32, 34);
            int kirbyCorner = rawSheet.GetPixel(kirbyRect.X, kirbyRect.Y);
            int rainCorner = rawSheet.GetPixel(rainRect.X, rainRect.Y);
            int bubbleCorner = rawSheet.GetPixel(bubbleRect.X, bubbleRect.Y);

            // Place Guard Mirror Kirby at his exact floor-aligned position (bodyPivotX=17, maxY=25)
            // and center the 30x32 bubble symmetrically around him (extending 3px below his feet).
            int kirbyDestX = NativeMethods.PIVOT_X - 17;
            int kirbyDestY = NativeMethods.PIVOT_Y - 25;
            int sphereDestX = kirbyDestX;
            int sphereDestY = kirbyDestY - 4;

            Array.Clear(scratch, 0, scratch.Length);

            for (int dy = 0; dy < rainRect.Height; dy++)
            {
                for (int dx = 0; dx < rainRect.Width; dx++)
                {
                    int rc = rawSheet.GetPixel(rainRect.X + dx, rainRect.Y + dy);
                    if (IsBgColor(rc, bg0, rainCorner)) continue;

                    int tx = sphereDestX + dx;
                    int ty = sphereDestY + dy;
                    if (tx < 0 || tx >= NativeMethods.TILE_SIZE || ty < 0 || ty >= NativeMethods.TILE_SIZE) continue;

                    int kdx = tx - kirbyDestX;
                    int kdy = ty - kirbyDestY;
                    bool hasKirby = false;
                    int kc = 0;
                    if (kdx >= 0 && kdx < kirbyRect.Width && kdy >= 0 && kdy < kirbyRect.Height)
                    {
                        kc = rawSheet.GetPixel(kirbyRect.X + kdx, kirbyRect.Y + kdy);
                        if (!IsBgColor(kc, bg0, kirbyCorner)) hasKirby = true;
                    }

                    int bc = rawSheet.GetPixel(bubbleRect.X + dx, bubbleRect.Y + dy);
                    bool hasBubbleRim = !IsBgColor(bc, bg0, bubbleCorner);
                    if (hasBubbleRim)
                    {
                        int br = (bc >> 16) & 0xFF;
                        int bg = (bc >> 8) & 0xFF;
                        int bb = bc & 0xFF;
                        // Clean up orange/magenta compression artifacts on the bubble rim into crisp sky-blue.
                        if (br > 180 && bg < 200)
                        {
                            br = 80; bg = 180; bb = 248;
                        }
                        scratch[ty * NativeMethods.TILE_SIZE + tx] = MakePremulArgb(245, br, bg, bb);
                    }
                    else
                    {
                        int rr = (rc >> 16) & 0xFF;
                        int rg = (rc >> 8) & 0xFF;
                        int rb = rc & 0xFF;
                        // Smooth out the 3 pure-white stripe bands between Orange and Yellow into warm golden-amber.
                        if (rr >= 248 && rg >= 248 && rb >= 248)
                        {
                            rr = 248; rg = 200; rb = 64;
                        }

                        if (hasKirby)
                        {
                            int kr = (kc >> 16) & 0xFF;
                            int kg = (kc >> 8) & 0xFF;
                            int kb = kc & 0xFF;
                            int nr = (int)(kr * 0.55 + rr * 0.45);
                            int ng = (int)(kg * 0.55 + rg * 0.45);
                            int nb = (int)(kb * 0.55 + rb * 0.45);
                            scratch[ty * NativeMethods.TILE_SIZE + tx] = MakePremulArgb(255, nr, ng, nb);
                        }
                        else
                        {
                            scratch[ty * NativeMethods.TILE_SIZE + tx] = MakePremulArgb(175, rr, rg, rb);
                        }
                    }
                }
            }

            return BuildSpriteTile(scratch);
        }

        /// <summary>
        /// Extracts the 5-frame jump sequence. Uses frame 0's bottom edge (<c>refBottomY</c>) as a common
        /// vertical baseline so raised hats/weapons stay aligned across the somersault arc.
        /// </summary>
        private static SpriteTile[] ExtractJumpTiles(FastSheet rawSheet, Rectangle[] jumpCoords, int bg0, int[] scratch)
        {
            if (jumpCoords == null || jumpCoords.Length == 0) return null;
            SpriteTile[] tiles = new SpriteTile[jumpCoords.Length];
            int refBottomY = jumpCoords[0].Bottom - 1;

            for (int i = 0; i < jumpCoords.Length; i++)
            {
                Rectangle r = jumpCoords[i];
                int minX = r.Width, maxX = 0;
                // Frames 1 and 2 are upside-down mid-air somersault poses, so Kirby's feet are at the top of the crop.
                bool isFlip = (i == 1 || i == 2);
                int yStart = isFlip ? 0 : Math.Max(0, r.Height - 4);
                int yEnd = isFlip ? Math.Min(r.Height - 1, 10) : r.Height - 1;

                for (int dy = yStart; dy <= yEnd; dy++)
                {
                    for (int dx = 0; dx < r.Width; dx++)
                    {
                        int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                        if (!IsBgColor(c, bg0, bg0))
                        {
                            if (dx < minX) minX = dx;
                            if (dx > maxX) maxX = dx;
                        }
                    }
                }

                int bodyPivotX = (minX <= maxX) ? (minX + maxX) / 2 : r.Width / 2;
                int destX = NativeMethods.PIVOT_X - bodyPivotX;
                int destY = NativeMethods.PIVOT_Y - (refBottomY - r.Y);

                Array.Clear(scratch, 0, scratch.Length);
                for (int dy = 0; dy < r.Height; dy++)
                {
                    for (int dx = 0; dx < r.Width; dx++)
                    {
                        int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                        if (!IsBgColor(c, bg0, bg0))
                        {
                            int tx = destX + dx;
                            int ty = destY + dy;
                            if (tx >= 0 && tx < NativeMethods.TILE_SIZE && ty >= 0 && ty < NativeMethods.TILE_SIZE)
                            {
                                scratch[ty * NativeMethods.TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                            }
                        }
                    }
                }
                tiles[i] = BuildSpriteTile(scratch);
            }
            return tiles;
        }

        /// <summary>
        /// Extracts a sprite frame and centers its non-background bounding box on <c>(PIVOT_X, PIVOT_Y)</c>
        /// (used for projectiles or floating/flying characters with <see cref="PivotMode.Center"/>).
        /// </summary>
        private static SpriteTile ExtractCenteredTile(FastSheet rawSheet, Rectangle r, int bg0, int bgCorner, int[] scratch)
        {
            int minX = r.Width, maxX = -1, minY = r.Height, maxY = -1;
            for (int dy = 0; dy < r.Height; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bgCorner))
                    {
                        if (dx < minX) minX = dx;
                        if (dx > maxX) maxX = dx;
                        if (dy < minY) minY = dy;
                        if (dy > maxY) maxY = dy;
                    }
                }
            }

            int centerX = (minX <= maxX) ? (minX + maxX) / 2 : r.Width / 2;
            int centerY = (minY <= maxY) ? (minY + maxY) / 2 : r.Height / 2;
            return ExtractCustomPivotTile(rawSheet, r, bg0, bgCorner, centerX, centerY, scratch);
        }

        /// <summary>
        /// Extracts a sprite frame aligned to an explicit <c>(pivotX, pivotY)</c> coordinate within the source rectangle.
        /// When <paramref name="pivotY"/> is negative, aligns the bottom edge of the rectangle (<c>r.Height - 1</c>) to <c>PIVOT_Y</c>.
        /// </summary>
        private static SpriteTile ExtractCustomPivotTile(FastSheet rawSheet, Rectangle r, int bg0, int bgCorner, int pivotX, int pivotY, int[] scratch)
        {
            int actualPivotY = (pivotY < 0) ? (r.Height - 1) : pivotY;
            int destX = NativeMethods.PIVOT_X - pivotX;
            int destY = NativeMethods.PIVOT_Y - actualPivotY;

            Array.Clear(scratch, 0, scratch.Length);
            for (int dy = 0; dy < r.Height; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bgCorner))
                    {
                        int tx = destX + dx;
                        int ty = destY + dy;
                        if (tx >= 0 && tx < NativeMethods.TILE_SIZE && ty >= 0 && ty < NativeMethods.TILE_SIZE)
                        {
                            scratch[ty * NativeMethods.TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                        }
                    }
                }
            }
            return BuildSpriteTile(scratch);
        }

        /// <summary>
        /// Crops the 24x24 Normal Kirby walk frame into a GDI HICON for the Windows system tray.
        /// Returns the raw <paramref name="iconHandle"/> so it can be freed via <c>DestroyIcon</c> on exit.
        /// </summary>
        private static void ExtractTrayIconFromNormalSheet(FastSheet normalSheet, int bg0, out Icon icon, out IntPtr iconHandle)
        {
            icon = null;
            iconHandle = IntPtr.Zero;
            try
            {
                using (Bitmap crop = new Bitmap(24, 24, PixelFormat.Format32bppArgb))
                {
                    int bgRgb = bg0 & 0x00FFFFFF;
                    for (int y = 0; y < 24; y++)
                    {
                        for (int x = 0; x < 24; x++)
                        {
                            int c = normalSheet.GetPixel(32 + x, 125 + y);
                            if ((c & 0x00FFFFFF) == bgRgb)
                                crop.SetPixel(x, y, Color.Transparent);
                            else
                                crop.SetPixel(x, y, Color.FromArgb(255, (c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF));
                        }
                    }
                    iconHandle = crop.GetHicon();
                    icon = Icon.FromHandle(iconHandle);
                }
            }
            catch { }
        }
    }
}
