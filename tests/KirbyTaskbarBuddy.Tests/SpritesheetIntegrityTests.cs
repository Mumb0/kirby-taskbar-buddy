using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class SpritesheetIntegrityTests
    {
        [Fact]
        public void AllSpritesheets_ExistAsEmbeddedResources_AndContainAllSourceRectangles()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            List<EnemyDef> enemies = new List<EnemyDef>();
            FormDefinitions.PopulateEnemies(enemies);

            Assembly appAssembly = typeof(FormDefinitions).Assembly;

            foreach (FormDef form in forms)
            {
                using (Stream stream = appAssembly.GetManifestResourceStream(form.FileName))
                {
                    Assert.True(stream != null, "Missing embedded resource: " + form.FileName);
                    using (Bitmap bmp = new Bitmap(stream))
                    {
                        AssertRectsWithinBitmap(form.Name, "Walk", form.Walk.SourceRects, bmp);
                        AssertRectsWithinBitmap(form.Name, "Guard", form.Guard.SourceRects, bmp);
                        AssertRectsWithinBitmap(form.Name, "Jump", form.Jump.SourceRects, bmp);
                        AssertRectsWithinBitmap(form.Name, "Stand", form.Stand.SourceRects, bmp);

                        if (form.Inhale != null)
                        {
                            AssertRectsWithinBitmap(form.Name, "Inhale", form.Inhale.SourceRects, bmp);
                        }
                        if (form.Mouthful != null)
                        {
                            AssertRectsWithinBitmap(form.Name, "Mouthful", form.Mouthful.SourceRects, bmp);
                        }
                        if (form.Swallow != null)
                        {
                            AssertRectsWithinBitmap(form.Name, "Swallow", form.Swallow.SourceRects, bmp);
                        }
                        if (form.SwallowAbility != null)
                        {
                            AssertRectsWithinBitmap(form.Name, "SwallowAbility", form.SwallowAbility.SourceRects, bmp);
                        }

                        if (form.Guard.Pivot == PivotMode.MirrorGuard)
                        {
                            // Mirror Guard also composites Kirby's body (37, 880, 31, 27) and bubble rim (2, 877, 32, 34).
                            AssertRectsWithinBitmap(
                                form.Name,
                                "MirrorGuardSpecial",
                                new Rectangle[] { new Rectangle(37, 880, 31, 27), new Rectangle(2, 877, 32, 34) },
                                bmp);
                        }
                    }
                }
            }

            foreach (EnemyDef enemy in enemies)
            {
                using (Stream stream = appAssembly.GetManifestResourceStream(enemy.FileName))
                {
                    Assert.True(stream != null, "Missing embedded resource: " + enemy.FileName);
                    using (Bitmap bmp = new Bitmap(stream))
                    {
                        AssertRectsWithinBitmap(enemy.Name, "Walk", enemy.Walk.SourceRects, bmp);
                        if (enemy.Inhaled != null)
                        {
                            AssertRectsWithinBitmap(enemy.Name, "Inhaled", enemy.Inhaled.SourceRects, bmp);
                        }
                    }
                }
            }

            using (Stream sleepStream = appAssembly.GetManifestResourceStream("kirby-sleep.png"))
            {
                Assert.NotNull(sleepStream);
                using (Bitmap sleepBmp = new Bitmap(sleepStream))
                {
                    AssertRectsWithinBitmap("sleep", "SleepCoords", FormDefinitions.SleepCoords, sleepBmp);
                }
            }
        }

        [Fact]
        public void AllSpritesheets_HaveTransparentBackgrounds()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            List<EnemyDef> enemies = new List<EnemyDef>();
            FormDefinitions.PopulateEnemies(enemies);

            Assembly appAssembly = typeof(FormDefinitions).Assembly;

            foreach (FormDef form in forms)
            {
                using (Stream stream = appAssembly.GetManifestResourceStream(form.FileName))
                using (Bitmap bmp = new Bitmap(stream))
                {
                    Assert.True(
                        bmp.GetPixel(0, 0).A == 0,
                        string.Format("{0} top-left pixel (0,0) is not transparent; run clean-sprites.bat or SpriteLoader.StripSpritesheetBackgrounds", form.FileName));
                }
            }

            foreach (EnemyDef enemy in enemies)
            {
                using (Stream stream = appAssembly.GetManifestResourceStream(enemy.FileName))
                using (Bitmap bmp = new Bitmap(stream))
                {
                    Assert.True(
                        bmp.GetPixel(0, 0).A == 0,
                        string.Format("{0} top-left pixel (0,0) is not transparent; run clean-sprites.bat or SpriteLoader.StripSpritesheetBackgrounds", enemy.FileName));
                }
            }

            using (Stream sleepStream = appAssembly.GetManifestResourceStream("kirby-sleep.png"))
            using (Bitmap sleepBmp = new Bitmap(sleepStream))
            {
                Assert.Equal(0, sleepBmp.GetPixel(0, 0).A);
            }
        }

        [Fact]
        public void LoadAllSpritesheets_ExtractsNonEmptyValidTilesForAllAbilitiesAndSleep()
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
                    AssertLoadedClipValid(form.Name, "Walk", form.Walk, expectedMinFrames: 6);
                    AssertLoadedClipValid(form.Name, "Guard", form.Guard, expectedMinFrames: 2);
                    AssertLoadedClipValid(form.Name, "Jump", form.Jump, expectedMinFrames: 5);
                    AssertLoadedClipValid(form.Name, "Stand", form.Stand, expectedMinFrames: 2);

                    if (form.Inhale != null)
                    {
                        AssertLoadedClipValid(form.Name, "Inhale", form.Inhale, expectedMinFrames: 2);
                    }
                    if (form.Mouthful != null)
                    {
                        AssertLoadedClipValid(form.Name, "Mouthful", form.Mouthful, expectedMinFrames: 1);
                    }
                    if (form.Swallow != null)
                    {
                        AssertLoadedClipValid(form.Name, "Swallow", form.Swallow, expectedMinFrames: 3);
                    }
                    if (form.SwallowAbility != null)
                    {
                        AssertLoadedClipValid(form.Name, "SwallowAbility", form.SwallowAbility, expectedMinFrames: 37);
                    }
                }

                foreach (EnemyDef enemy in enemies)
                {
                    AssertLoadedClipValid(enemy.Name, "Walk", enemy.Walk, expectedMinFrames: 2);
                    if (enemy.Inhaled != null)
                    {
                        AssertLoadedClipValid(enemy.Name, "Inhaled", enemy.Inhaled, expectedMinFrames: 1);
                    }
                }

                Assert.NotNull(sleepFrames);
                Assert.Equal(FormDefinitions.SleepCoords.Length, sleepFrames.Length);
                for (int i = 0; i < sleepFrames.Length; i++)
                {
                    AssertTileValid(string.Format("sleep[{0}]", i), sleepFrames[i]);
                }
            }
            finally
            {
                if (trayIcon != null) trayIcon.Dispose();
                if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
            }
        }

        [Fact]
        public void LoadAllSpritesheets_ReusesRawFrameInstancesInPingPongSequences()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            SpriteTile[] sleepFrames;
            Icon trayIcon;
            IntPtr trayIconHandle;

            SpriteLoader.LoadAllSpritesheets(forms, out sleepFrames, out trayIcon, out trayIconHandle);
            try
            {
                foreach (FormDef form in forms)
                {
                    AnimClip walk = form.Walk;
                    Assert.Equal(FormDefinitions.WalkPingPongSeq.Length, walk.Frames.Length);
                    for (int i = 0; i < walk.Sequence.Length; i++)
                    {
                        int rawIdx = walk.Sequence[i];
                        Assert.Same(walk.RawFrames[rawIdx], walk.Frames[i]);
                    }
                }
            }
            finally
            {
                if (trayIcon != null) trayIcon.Dispose();
                if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
            }
        }

        [Fact]
        public void LoadAllSpritesheets_ExtractsValid24x24TrayIcon()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            SpriteTile[] sleepFrames;
            Icon trayIcon;
            IntPtr trayIconHandle;

            SpriteLoader.LoadAllSpritesheets(forms, out sleepFrames, out trayIcon, out trayIconHandle);
            try
            {
                Assert.NotNull(trayIcon);
                Assert.NotEqual(IntPtr.Zero, trayIconHandle);
                Assert.Equal(24, trayIcon.Width);
                Assert.Equal(24, trayIcon.Height);
            }
            finally
            {
                if (trayIcon != null) trayIcon.Dispose();
                if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
            }
        }

        private static void AssertRectsWithinBitmap(string formName, string clipName, Rectangle[] rects, Bitmap bmp)
        {
            for (int i = 0; i < rects.Length; i++)
            {
                Rectangle r = rects[i];
                Assert.True(
                    r.Left >= 0 && r.Top >= 0 && r.Right <= bmp.Width && r.Bottom <= bmp.Height,
                    string.Format(
                        "{0}.{1}[{2}] rect {3} is outside bitmap bounds ({4}x{5})",
                        formName, clipName, i, r, bmp.Width, bmp.Height));
            }
        }

        private static void AssertLoadedClipValid(string formName, string clipName, AnimClip clip, int expectedMinFrames)
        {
            Assert.NotNull(clip.RawFrames);
            Assert.Equal(clip.SourceRects.Length, clip.RawFrames.Length);
            Assert.True(clip.HasFrames(expectedMinFrames), string.Format("{0}.{1} missing expected frames", formName, clipName));

            for (int i = 0; i < clip.RawFrames.Length; i++)
            {
                AssertTileValid(string.Format("{0}.{1}.RawFrames[{2}]", formName, clipName, i), clip.RawFrames[i]);
            }

            for (int i = 0; i < clip.Frames.Length; i++)
            {
                AssertTileValid(string.Format("{0}.{1}.Frames[{2}]", formName, clipName, i), clip.Frames[i]);
            }
        }

        private static void AssertTileValid(string label, SpriteTile tile)
        {
            Assert.NotNull(tile);
            // BuildSpriteTile returns a 1x1 zero-pixel tile if all pixels were stripped as background.
            Assert.True(tile.Width > 1 && tile.Height > 1, label + " produced a degenerate/empty SpriteTile");
            Assert.InRange(tile.MinX, 0, NativeMethods.TILE_SIZE - 1);
            Assert.InRange(tile.MinY, 0, NativeMethods.TILE_SIZE - 1);
            Assert.InRange(tile.MinX + tile.Width, 1, NativeMethods.TILE_SIZE);
            Assert.InRange(tile.MinY + tile.Height, 1, NativeMethods.TILE_SIZE);
            Assert.NotNull(tile.PremulPixels);
            Assert.Equal(tile.Width * tile.Height, tile.PremulPixels.Length);

            bool hasOpaquePixel = false;
            for (int i = 0; i < tile.PremulPixels.Length; i++)
            {
                if (tile.PremulPixels[i] != 0)
                {
                    hasOpaquePixel = true;
                    break;
                }
            }
            Assert.True(hasOpaquePixel, label + " contains no non-transparent pixels");
        }
    }
}
