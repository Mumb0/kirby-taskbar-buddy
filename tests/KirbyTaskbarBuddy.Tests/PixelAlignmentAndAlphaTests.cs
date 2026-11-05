using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class PixelAlignmentAndAlphaTests
    {
        [Fact]
        public void MakePremulArgb_SatisfiesWin32PremultipliedAlphaContract()
        {
            Assert.Equal(0, SpriteLoader.MakePremulArgb(0, 255, 128, 64));
            Assert.Equal(0, SpriteLoader.MakePremulArgb(-10, 255, 128, 64));

            int fullOpaque = SpriteLoader.MakePremulArgb(255, 240, 120, 60);
            Assert.Equal(255, (fullOpaque >> 24) & 0xFF);
            Assert.Equal(240, (fullOpaque >> 16) & 0xFF);
            Assert.Equal(120, (fullOpaque >> 8) & 0xFF);
            Assert.Equal(60, fullOpaque & 0xFF);

            int halfAlpha = SpriteLoader.MakePremulArgb(128, 200, 100, 50);
            int a = (halfAlpha >> 24) & 0xFF;
            int r = (halfAlpha >> 16) & 0xFF;
            int g = (halfAlpha >> 8) & 0xFF;
            int b = halfAlpha & 0xFF;
            Assert.Equal(128, a);
            Assert.InRange(r, 99, 101);
            Assert.InRange(g, 49, 51);
            Assert.InRange(b, 24, 26);
            Assert.True(r <= a && g <= a && b <= a);
        }

        [Fact]
        public void AllExtractedTiles_HaveValidPremultipliedAlphaPixels()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            List<EnemyDef> enemies = new List<EnemyDef>();
            FormDefinitions.PopulateEnemies(enemies);

            SpriteTile[] sleepFrames;
            Icon trayIcon;
            IntPtr trayIconHandle;

            SpriteLoader.LoadAllSpritesheets(forms, enemies, out sleepFrames, out trayIcon, out trayIconHandle);
            try
            {
                foreach (FormDef form in forms)
                {
                    AssertClipPremultipliedAlpha(form.Name, "Walk", form.Walk);
                    AssertClipPremultipliedAlpha(form.Name, "Guard", form.Guard);
                    AssertClipPremultipliedAlpha(form.Name, "Jump", form.Jump);
                    AssertClipPremultipliedAlpha(form.Name, "Stand", form.Stand);

                    if (form.Inhale != null)
                    {
                        AssertClipPremultipliedAlpha(form.Name, "Inhale", form.Inhale);
                    }
                    if (form.Mouthful != null)
                    {
                        AssertClipPremultipliedAlpha(form.Name, "Mouthful", form.Mouthful);
                    }
                    if (form.Swallow != null)
                    {
                        AssertClipPremultipliedAlpha(form.Name, "Swallow", form.Swallow);
                    }
                }

                foreach (EnemyDef enemy in enemies)
                {
                    AssertClipPremultipliedAlpha(enemy.Name, "Walk", enemy.Walk);
                    if (enemy.Inhaled != null)
                    {
                        AssertClipPremultipliedAlpha(enemy.Name, "Inhaled", enemy.Inhaled);
                    }
                }

                for (int i = 0; i < sleepFrames.Length; i++)
                {
                    AssertTilePremultipliedAlpha("sleep[" + i + "]", sleepFrames[i]);
                }
            }
            finally
            {
                if (trayIcon != null) trayIcon.Dispose();
                if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
            }
        }

        [Fact]
        public void WalkStandAndGuardTiles_AlignFeetToPivotY_AndStraddlePivotX()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            List<EnemyDef> enemies = new List<EnemyDef>();
            FormDefinitions.PopulateEnemies(enemies);

            SpriteTile[] sleepFrames;
            Icon trayIcon;
            IntPtr trayIconHandle;

            SpriteLoader.LoadAllSpritesheets(forms, enemies, out sleepFrames, out trayIcon, out trayIconHandle);
            try
            {
                foreach (FormDef form in forms)
                {
                    AssertFeetAlignedToFloor(form.Name, "Walk", form.Walk.RawFrames);
                    AssertFeetAlignedToFloor(form.Name, "Stand", form.Stand.RawFrames);

                    if (form.Inhale != null)
                    {
                        AssertFeetAlignedToFloor(form.Name, "Inhale", form.Inhale.RawFrames);
                    }
                    if (form.Mouthful != null)
                    {
                        AssertFeetAlignedToFloor(form.Name, "Mouthful", form.Mouthful.RawFrames);
                    }
                    if (form.Swallow != null)
                    {
                        AssertFeetAlignedToFloor(form.Name, "Swallow", form.Swallow.RawFrames);
                    }

                    if (form.Guard.Pivot == PivotMode.MirrorGuard)
                    {
                        // Mirror Kirby's reflective guard sphere extends below his feet (PIVOT_Y = 54).
                        foreach (SpriteTile tile in form.Guard.RawFrames)
                        {
                            int bottomY = tile.MinY + tile.Height - 1;
                            Assert.True(bottomY > NativeMethods.PIVOT_Y, "Mirror guard sphere should extend below PIVOT_Y");
                            Assert.True(tile.MinX < NativeMethods.PIVOT_X && (tile.MinX + tile.Width - 1) > NativeMethods.PIVOT_X);
                        }
                    }
                    else
                    {
                        AssertFeetAlignedToFloor(form.Name, "Guard", form.Guard.RawFrames);
                    }
                }

                foreach (EnemyDef enemy in enemies)
                {
                    AssertFeetAlignedToFloor(enemy.Name, "Walk", enemy.Walk.RawFrames);
                    if (enemy.Inhaled != null)
                    {
                        AssertFeetAlignedToFloor(enemy.Name, "Inhaled", enemy.Inhaled.RawFrames);
                    }
                }

                for (int i = 0; i < sleepFrames.Length; i++)
                {
                    SpriteTile sleepTile = sleepFrames[i];
                    int bottomY = sleepTile.MinY + sleepTile.Height - 1;
                    Assert.Equal(NativeMethods.PIVOT_Y, bottomY);
                    Assert.True(sleepTile.MinX < NativeMethods.PIVOT_X && (sleepTile.MinX + sleepTile.Width - 1) > NativeMethods.PIVOT_X);
                }
            }
            finally
            {
                if (trayIcon != null) trayIcon.Dispose();
                if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
            }
        }

        [Fact]
        public void LoadClip_FeetBottom4Rows_CentersOnFeetAndIgnoresWideHatAbove()
        {
            using (Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                // Wide asymmetrical hat/weapon near the top: X = 2..28 at Y = 5
                for (int x = 2; x <= 28; x++)
                {
                    bmp.SetPixel(x, 5, Color.FromArgb(255, 255, 200, 0));
                }

                // Narrow feet at the bottom: X = 4..8 at Y = 20 (center of feet = X=6, bottom row = Y=20)
                for (int x = 4; x <= 8; x++)
                {
                    bmp.SetPixel(x, 20, Color.FromArgb(255, 255, 100, 150));
                }

                using (FastSheet sheet = new FastSheet(null, bmp, 32, 32))
                {
                    int[] scratch = new int[NativeMethods.TILE_SIZE * NativeMethods.TILE_SIZE];
                    AnimClip clip = FormDefinitions.Clip(
                        new Rectangle[] { new Rectangle(0, 0, 32, 32) },
                        0.1,
                        false,
                        PivotMode.FeetBottom4Rows);

                    SpriteLoader.LoadClip(sheet, clip, scratch);

                    SpriteTile tile = clip.Frames[0];
                    // Feet at Y=20 must land on PIVOT_Y (54), so top of hat (Y=5) lands at 54 - (20 - 5) = 39.
                    Assert.Equal(NativeMethods.PIVOT_Y, tile.MinY + tile.Height - 1);
                    Assert.Equal(39, tile.MinY);

                    // Feet center (X=6) must land on PIVOT_X (40), so hat left edge (X=2) lands at 40 - (6 - 2) = 36.
                    Assert.Equal(36, tile.MinX);
                    Assert.Equal(27, tile.Width); // X=2..28 inclusive -> 27px wide
                }
            }
        }

        [Fact]
        public void LoadClip_CenterAndCustomPivot_AlignToExpectedCoordinates()
        {
            using (Bitmap bmp = new Bitmap(20, 20, PixelFormat.Format32bppArgb))
            {
                // Leave background transparent (alpha = 0) and draw a 5x5 block at X = 2..6, Y = 4..8 (center = 4, 6).
                for (int y = 4; y <= 8; y++)
                {
                    for (int x = 2; x <= 6; x++)
                    {
                        bmp.SetPixel(x, y, Color.FromArgb(255, 255, 50, 50));
                    }
                }

                using (FastSheet sheet = new FastSheet(null, bmp, 20, 20))
                {
                    int[] scratch = new int[NativeMethods.TILE_SIZE * NativeMethods.TILE_SIZE];

                    // 1. PivotMode.Center places bounding-box center (4, 6) at (PIVOT_X=40, PIVOT_Y=54) -> MinX=38, MinY=52.
                    AnimClip centerClip = FormDefinitions.Clip(
                        new Rectangle[] { new Rectangle(0, 0, 20, 20) },
                        0.1,
                        false,
                        PivotMode.Center);
                    SpriteLoader.LoadClip(sheet, centerClip, scratch);
                    Assert.Equal(NativeMethods.PIVOT_X - 2, centerClip.Frames[0].MinX);
                    Assert.Equal(NativeMethods.PIVOT_Y - 2, centerClip.Frames[0].MinY);
                    Assert.Equal(5, centerClip.Frames[0].Width);
                    Assert.Equal(5, centerClip.Frames[0].Height);

                    // 2. PivotMode.CustomPivot with explicit (pivotX=2, pivotY=4) places top-left of block at (PIVOT_X, PIVOT_Y).
                    AnimClip customClip = FormDefinitions.Clip(
                        new Rectangle[] { new Rectangle(0, 0, 20, 20) },
                        0.1,
                        false,
                        PivotMode.CustomPivot,
                        null,
                        customPivotX: 2,
                        customPivotY: 4);
                    SpriteLoader.LoadClip(sheet, customClip, scratch);
                    Assert.Equal(NativeMethods.PIVOT_X, customClip.Frames[0].MinX);
                    Assert.Equal(NativeMethods.PIVOT_Y, customClip.Frames[0].MinY);
                }
            }
        }

        [Fact]
        public void IsTransparent_And_IsRawSheetBgColor_DetectExpectedColors()
        {
            int bg0 = unchecked((int)0xFF112233);
            int bgCorner = unchecked((int)0xFF445566);

            Assert.True(SpriteLoader.IsTransparent(0x00FFFFFF));
            Assert.False(SpriteLoader.IsTransparent(unchecked((int)0xFFFF80A0)));

            Assert.True(SpriteLoader.IsRawSheetBgColor(0x00FFFFFF, bg0, bgCorner)); // Alpha = 0
            Assert.True(SpriteLoader.IsRawSheetBgColor(bg0, bg0, bgCorner));        // Sheet top-left color
            Assert.True(SpriteLoader.IsRawSheetBgColor(bgCorner, bg0, bgCorner));   // Sub-rect corner color
            Assert.True(SpriteLoader.IsRawSheetBgColor(unchecked((int)0xFF8080FF), bg0, bgCorner)); // Outer sheet periwinkle
            Assert.True(SpriteLoader.IsRawSheetBgColor(unchecked((int)0xFF709AD1), bg0, bgCorner)); // Blue divider grid
            Assert.True(SpriteLoader.IsRawSheetBgColor(unchecked((int)0xFF9696FF), bg0, bgCorner)); // Lavender fill
            Assert.False(SpriteLoader.IsRawSheetBgColor(unchecked((int)0xFFFF80A0), bg0, bgCorner)); // Kirby pink
        }

        [Fact]
        public void StripSingleSpritesheet_StripsOpaqueBackgroundAndGrid_AndIsIdempotent()
        {
            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kirby_test_sheet_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                using (Bitmap raw = new Bitmap(8, 8, PixelFormat.Format32bppArgb))
                {
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            raw.SetPixel(x, y, Color.FromArgb(255, 128, 128, 255)); // 0x8080FF outer bg
                        }
                    }
                    raw.SetPixel(1, 1, Color.FromArgb(255, 112, 154, 209)); // 0x709AD1 grid line
                    raw.SetPixel(2, 2, Color.FromArgb(255, 150, 150, 255)); // 0x9696FF inner box fill
                    raw.SetPixel(4, 4, Color.FromArgb(255, 255, 128, 160)); // Kirby pink pixel
                    raw.Save(tempFile, ImageFormat.Png);
                }

                // First pass strips the background and grid colors and returns true.
                Assert.True(SpriteLoader.StripSingleSpritesheet(tempFile));

                using (Bitmap cleaned = new Bitmap(tempFile))
                {
                    Assert.Equal(0, cleaned.GetPixel(0, 0).A);
                    Assert.Equal(0, cleaned.GetPixel(1, 1).A);
                    Assert.Equal(0, cleaned.GetPixel(2, 2).A);
                    Color kirbyPixel = cleaned.GetPixel(4, 4);
                    Assert.Equal(255, kirbyPixel.A);
                    Assert.Equal(255, kirbyPixel.R);
                    Assert.Equal(128, kirbyPixel.G);
                    Assert.Equal(160, kirbyPixel.B);
                }

                // Second pass on an already-cleaned sheet is a no-op and returns false.
                Assert.False(SpriteLoader.StripSingleSpritesheet(tempFile));
            }
            finally
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
            }
        }

        [Fact]
        public void BuildSpriteTile_CropsTightNonZeroBoundingBox_AndHandlesEmptyCanvas()
        {
            int[] scratch = new int[NativeMethods.TILE_SIZE * NativeMethods.TILE_SIZE];

            // Empty canvas returns a safe 1x1 transparent tile.
            SpriteTile emptyTile = SpriteLoader.BuildSpriteTile(scratch);
            Assert.Equal(1, emptyTile.Width);
            Assert.Equal(1, emptyTile.Height);
            Assert.Equal(0, emptyTile.PremulPixels[0]);

            // Two opaque pixels at (10, 15) and (12, 18) -> 3x4 bounding box starting at (10, 15).
            int color = unchecked((int)0xFFFF00FF);
            scratch[15 * NativeMethods.TILE_SIZE + 10] = color;
            scratch[18 * NativeMethods.TILE_SIZE + 12] = color;

            SpriteTile cropped = SpriteLoader.BuildSpriteTile(scratch);
            Assert.Equal(10, cropped.MinX);
            Assert.Equal(15, cropped.MinY);
            Assert.Equal(3, cropped.Width);
            Assert.Equal(4, cropped.Height);
            Assert.Equal(12, cropped.PremulPixels.Length);
            Assert.Equal(color, cropped.PremulPixels[0]);  // (10, 15)
            Assert.Equal(color, cropped.PremulPixels[11]); // (12, 18)
        }

        private static void AssertFeetAlignedToFloor(string formName, string clipName, SpriteTile[] tiles)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                SpriteTile tile = tiles[i];
                int bottomY = tile.MinY + tile.Height - 1;
                Assert.True(
                    bottomY == NativeMethods.PIVOT_Y,
                    string.Format("{0}.{1}[{2}] bottomY expected {3}, got {4}", formName, clipName, i, NativeMethods.PIVOT_Y, bottomY));
                Assert.True(
                    tile.MinX <= NativeMethods.PIVOT_X && (tile.MinX + tile.Width - 1) >= NativeMethods.PIVOT_X,
                    string.Format("{0}.{1}[{2}] horizontal span [{3}..{4}] does not cover PIVOT_X ({5})",
                        formName, clipName, i, tile.MinX, tile.MinX + tile.Width - 1, NativeMethods.PIVOT_X));
            }
        }

        private static void AssertClipPremultipliedAlpha(string formName, string clipName, AnimClip clip)
        {
            for (int i = 0; i < clip.RawFrames.Length; i++)
            {
                AssertTilePremultipliedAlpha(string.Format("{0}.{1}[{2}]", formName, clipName, i), clip.RawFrames[i]);
            }
        }

        private static void AssertTilePremultipliedAlpha(string label, SpriteTile tile)
        {
            for (int i = 0; i < tile.PremulPixels.Length; i++)
            {
                int c = tile.PremulPixels[i];
                int a = (c >> 24) & 0xFF;
                int r = (c >> 16) & 0xFF;
                int g = (c >> 8) & 0xFF;
                int b = c & 0xFF;

                if (a == 0)
                {
                    Assert.True(c == 0, label + " has non-zero RGB in a transparent (alpha=0) pixel");
                }
                else
                {
                    Assert.True(
                        r <= a && g <= a && b <= a,
                        string.Format("{0} pixel {1} violates premultiplied alpha: A={2}, R={3}, G={4}, B={5}", label, i, a, r, g, b));
                }
            }
        }
    }
}
