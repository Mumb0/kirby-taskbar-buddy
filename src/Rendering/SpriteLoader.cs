using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Extracts, floor-aligns, and composites Kirby sprite frames from transparent embedded PNG spritesheets.
    /// </summary>
    public static class SpriteLoader {

        /// <summary>
        /// Decodes all ability and sleep spritesheets once at startup into compact <see cref="SpriteTile"/> arrays,
        /// and extracts the 24x24 system tray icon from <c>kirby-normal.png</c>.
        /// </summary>
        public static void LoadAllSpritesheets(
            List<FormDef> forms,
            out SpriteTile[] sleepFrames,
            out Icon trayIcon,
            out IntPtr trayIconHandle) {
            LoadAllSpritesheets(forms, null, out sleepFrames, out trayIcon, out trayIconHandle);
        }

        /// <summary>
        /// Decodes all ability, enemy, and sleep spritesheets once at startup into compact <see cref="SpriteTile"/> arrays,
        /// and extracts the 24x24 system tray icon from <c>kirby-normal.png</c>.
        /// </summary>
        public static void LoadAllSpritesheets(
            List<FormDef> forms,
            List<EnemyDef> enemies,
            out SpriteTile[] sleepFrames,
            out Icon trayIcon,
            out IntPtr trayIconHandle) {
            sleepFrames = null;
            trayIcon = null;
            trayIconHandle = IntPtr.Zero;

            // Single reusable 80x80 scratch canvas shared across all frame extractions at startup.
            int[] scratch = new int[CanvasConstants.TILE_SIZE * CanvasConstants.TILE_SIZE];

            if (forms != null) {
                foreach (FormDef form in forms) {
                    int reqW = 64, reqH = 160;
                    ExpandBounds(form.Walk, ref reqW, ref reqH);
                    ExpandBounds(form.Guard, ref reqW, ref reqH);
                    ExpandBounds(form.Jump, ref reqW, ref reqH);
                    ExpandBounds(form.Stand, ref reqW, ref reqH);
                    ExpandBounds(form.Inhale, ref reqW, ref reqH);
                    ExpandBounds(form.Mouthful, ref reqW, ref reqH);
                    ExpandBounds(form.Swallow, ref reqW, ref reqH);
                    ExpandBounds(form.SwallowAbility, ref reqW, ref reqH);
                    ExpandBounds(form.Hit, ref reqW, ref reqH);

                    using (FastSheet rawSheet = OpenSpriteSheet(form.FileName, reqW, reqH)) {
                        if (rawSheet == null) {
                            continue;
                        }

                        if (form.Name == "normal" && trayIcon == null) {
                            ExtractTrayIconFromNormalSheet(rawSheet, out trayIcon, out trayIconHandle);
                        }

                        LoadClip(rawSheet, form.Walk, scratch);
                        LoadClip(rawSheet, form.Guard, scratch);
                        LoadClip(rawSheet, form.Jump, scratch);
                        LoadClip(rawSheet, form.Stand, scratch);
                        LoadClip(rawSheet, form.Inhale, scratch);
                        LoadClip(rawSheet, form.Mouthful, scratch);
                        LoadClip(rawSheet, form.Swallow, scratch);
                        LoadClip(rawSheet, form.SwallowAbility, scratch);
                        LoadClip(rawSheet, form.Hit, scratch);

                        if (form.SwallowAbility != null) {
                            CompositeSwallowAbilityParticles(form.SwallowAbility, scratch);
                        }
                    }
                }
            }

            if (enemies != null) {
                foreach (EnemyDef enemy in enemies) {
                    int reqW = 64, reqH = 96;
                    ExpandBounds(enemy.Walk, ref reqW, ref reqH);
                    ExpandBounds(enemy.Inhaled, ref reqW, ref reqH);

                    using (FastSheet rawSheet = OpenSpriteSheet(enemy.FileName, reqW, reqH)) {
                        if (rawSheet == null) {
                            continue;
                        }

                        LoadClip(rawSheet, enemy.Walk, scratch);
                        LoadClip(rawSheet, enemy.Inhaled, scratch);
                    }
                }
            }

            int sleepW = 1, sleepH = 1;
            ExpandBounds(FormDefinitions.SleepCoords, ref sleepW, ref sleepH);

            using (FastSheet sleepSheet = OpenSpriteSheet("kirby-sleep.png", sleepW, sleepH)) {
                if (sleepSheet != null) {
                    sleepFrames = new SpriteTile[FormDefinitions.SleepCoords.Length];

                    for (int i = 0; i < FormDefinitions.SleepCoords.Length; i++) {
                        Rectangle sr = FormDefinitions.SleepCoords[i];
                        sleepFrames[i] = ExtractCustomPivotTile(
                            sleepSheet,
                            sr,
                            FormDefinitions.SLEEP_PIVOT_X,
                            FormDefinitions.SLEEP_PIVOT_Y,
                            scratch);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts all unique <see cref="AnimClip.SourceRects"/> into <see cref="AnimClip.RawFrames"/> according to
        /// <see cref="AnimClip.Pivot"/>, and builds the playback <see cref="AnimClip.Frames"/> array (reusing tile
        /// references when an index <see cref="AnimClip.Sequence"/> such as a ping-pong loop is defined).
        /// </summary>
        internal static void LoadClip(FastSheet rawSheet, AnimClip clip, int[] scratch) {
            if (rawSheet == null || clip == null || clip.SourceRects == null || clip.SourceRects.Length == 0) {
                return;
            }

            Rectangle[] rects = clip.SourceRects;

            if (clip.Pivot == PivotMode.JumpArc) {
                clip.RawFrames = ExtractJumpTiles(rawSheet, rects, scratch);
            }
            else {
                clip.RawFrames = new SpriteTile[rects.Length];

                for (int i = 0; i < rects.Length; i++) {
                    Rectangle r = rects[i];

                    switch (clip.Pivot) {
                        case PivotMode.MirrorGuard:
                            clip.RawFrames[i] = ExtractMirrorGuardTile(rawSheet, r, scratch);
                            break;
                        case PivotMode.GuardBottom2Rows:
                            clip.RawFrames[i] = ExtractAlignedTile(rawSheet, r, scratch, true);
                            break;
                        case PivotMode.Center:
                            clip.RawFrames[i] = ExtractCenteredTile(rawSheet, r, scratch);
                            break;
                        case PivotMode.CustomPivot:
                            clip.RawFrames[i] =
                                ExtractCustomPivotTile(rawSheet, r, clip.CustomPivotX, clip.CustomPivotY, scratch);
                            break;
                        case PivotMode.FeetBottom4Rows:
                        default:
                            clip.RawFrames[i] = ExtractAlignedTile(rawSheet, r, scratch);
                            break;
                    }
                }
            }

            if (clip.Sequence != null && clip.Sequence.Length > 0 && clip.RawFrames != null) {
                clip.Frames = new SpriteTile[clip.Sequence.Length];

                for (int i = 0; i < clip.Sequence.Length; i++) {
                    int idx = clip.Sequence[i];

                    if (idx >= 0 && idx < clip.RawFrames.Length) {
                        clip.Frames[i] = clip.RawFrames[idx];
                    }
                }
            }
            else {
                clip.Frames = clip.RawFrames;
            }
        }

        /// <summary>
        /// Composites the particle effects from <c>misc-effects.png</c> (<see cref="FormDefinitions.SwallowAbilityParticles"/>)
        /// on top of Kirby's floor-aligned swallow frames for the ability-swallow sequence (<see cref="FormDef.SwallowAbility"/>).
        /// </summary>
        internal static void CompositeSwallowAbilityParticles(AnimClip clip, int[] scratch) {
            if (clip == null || clip.Frames == null || clip.RawFrames == null || scratch == null) {
                return;
            }

            int effW = 1, effH = 1;
            ExpandBounds(FormDefinitions.SwallowEffectRects, ref effW, ref effH);

            using (FastSheet effectsSheet = OpenSpriteSheet("misc-effects.png", effW, effH)) {
                if (effectsSheet == null) {
                    return;
                }

                SwallowParticleLayer[][] particleSeq = FormDefinitions.SwallowAbilityParticles;
                Rectangle[] effectRects = FormDefinitions.SwallowEffectRects;
                int count = Math.Min(clip.Frames.Length, particleSeq != null ? particleSeq.Length : 0);

                for (int i = 0; i < count; i++) {
                    SwallowParticleLayer[] layers = particleSeq[i];
                    SpriteTile baseTile = clip.Frames[i];

                    if (layers == null || layers.Length == 0 || baseTile == null || baseTile.PremulPixels == null) {
                        continue;
                    }

                    Array.Clear(scratch, 0, scratch.Length);

                    for (int sy = 0; sy < baseTile.Height; sy++) {
                        int ty = baseTile.MinY + sy;

                        if (ty < 0 || ty >= CanvasConstants.TILE_SIZE) {
                            continue;
                        }

                        int dstRow = ty * CanvasConstants.TILE_SIZE;
                        int srcRow = sy * baseTile.Width;

                        for (int sx = 0; sx < baseTile.Width; sx++) {
                            int tx = baseTile.MinX + sx;

                            if (tx >= 0 && tx < CanvasConstants.TILE_SIZE) {
                                int px = baseTile.PremulPixels[srcRow + sx];

                                if (px != 0) {
                                    scratch[dstRow + tx] = px;
                                }
                            }
                        }
                    }

                    for (int l = 0; l < layers.Length; l++) {
                        SwallowParticleLayer layer = layers[l];

                        if (layer.SpriteIndex < 0 || layer.SpriteIndex >= effectRects.Length) {
                            continue;
                        }

                        Rectangle er = effectRects[layer.SpriteIndex];
                        int destX = CanvasConstants.PIVOT_X + layer.OffsetX;
                        int destY = CanvasConstants.PIVOT_Y + layer.OffsetY;

                        for (int dy = 0; dy < er.Height; dy++) {
                            int ty = destY + dy;

                            if (ty < 0 || ty >= CanvasConstants.TILE_SIZE) {
                                continue;
                            }

                            int dstRow = ty * CanvasConstants.TILE_SIZE;

                            for (int dx = 0; dx < er.Width; dx++) {
                                int tx = destX + dx;

                                if (tx < 0 || tx >= CanvasConstants.TILE_SIZE) {
                                    continue;
                                }

                                int c = effectsSheet.GetPixel(er.X + dx, er.Y + dy);

                                if (!IsTransparent(c)) {
                                    scratch[dstRow + tx] = (255 << 24) | (c & 0x00FFFFFF);
                                }
                            }
                        }
                    }

                    clip.Frames[i] = BuildSpriteTile(scratch);
                }
            }
        }

        private static void ExpandBounds(AnimClip clip, ref int maxW, ref int maxH) {
            if (clip != null) {
                ExpandBounds(clip.SourceRects, ref maxW, ref maxH);
            }
        }

        private static void ExpandBounds(Rectangle[] rects, ref int maxW, ref int maxH) {
            if (rects == null) {
                return;
            }

            for (int i = 0; i < rects.Length; i++) {
                if (rects[i].Right > maxW) {
                    maxW = rects[i].Right;
                }

                if (rects[i].Bottom > maxH) {
                    maxH = rects[i].Bottom;
                }
            }
        }

        private static void DisposeFailedSheetResources(Bitmap bmp, Stream stream) {
            if (bmp != null) {
                bmp.Dispose();
            }

            if (stream != null) {
                stream.Dispose();
            }
        }

        private static FastSheet OpenSpriteSheet(string fileName, int reqW, int reqH) {
            if (string.IsNullOrEmpty(fileName)) {
                return null;
            }

            Stream resStream = null;
            Bitmap resBmp = null;

            try {
                resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fileName);

                if (resStream != null) {
                    resBmp = new Bitmap(resStream);
                    return new FastSheet(resStream, resBmp, reqW, reqH);
                }
            }
            catch (ArgumentException ex) {
                DisposeFailedSheetResources(resBmp, resStream);
                Diagnostics.LogWarning("SpriteLoader.OpenSpriteSheet(Resource: " + fileName + ")", ex);
            }
            catch (ExternalException ex) {
                DisposeFailedSheetResources(resBmp, resStream);
                Diagnostics.LogWarning("SpriteLoader.OpenSpriteSheet(Resource: " + fileName + ")", ex);
            }
            catch (IOException ex) {
                DisposeFailedSheetResources(resBmp, resStream);
                Diagnostics.LogWarning("SpriteLoader.OpenSpriteSheet(Resource: " + fileName + ")", ex);
            }

            string location = Assembly.GetExecutingAssembly().Location;
            string exeDir = string.IsNullOrEmpty(location)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(location);

            if (string.IsNullOrEmpty(exeDir)) {
                return null;
            }

            string path = Path.Combine(exeDir, "sprites", fileName);

            if (!File.Exists(path)) {
                path = Path.Combine(exeDir, fileName);
            }

            if (File.Exists(path)) {
                Bitmap fileBmp = null;

                try {
                    fileBmp = new Bitmap(path);
                    return new FastSheet(null, fileBmp, reqW, reqH);
                }
                catch (ArgumentException ex) {
                    DisposeFailedSheetResources(fileBmp, null);
                    Diagnostics.LogWarning("SpriteLoader.OpenSpriteSheet(File: " + path + ")", ex);
                }
                catch (ExternalException ex) {
                    DisposeFailedSheetResources(fileBmp, null);
                    Diagnostics.LogWarning("SpriteLoader.OpenSpriteSheet(File: " + path + ")", ex);
                }
                catch (IOException ex) {
                    DisposeFailedSheetResources(fileBmp, null);
                    Diagnostics.LogWarning("SpriteLoader.OpenSpriteSheet(File: " + path + ")", ex);
                }
                catch (UnauthorizedAccessException ex) {
                    DisposeFailedSheetResources(fileBmp, null);
                    Diagnostics.LogWarning("SpriteLoader.OpenSpriteSheet(File: " + path + ")", ex);
                }
            }

            return null;
        }

        /// <summary>
        /// Packs an (A, R, G, B) color into 32-bit premultiplied ARGB format required by Win32 UpdateLayeredWindow.
        /// </summary>
        internal static int MakePremulArgb(int a, int r, int g, int b) {
            if (a <= 0) {
                return 0;
            }

            if (a >= 255) {
                return (255 << 24) | (r << 16) | (g << 8) | b;
            }

            int pr = (r * a + 127) / 255;
            int pg = (g * a + 127) / 255;
            int pb = (b * a + 127) / 255;
            return (a << 24) | (pr << 16) | (pg << 8) | pb;
        }

        /// <summary>
        /// Returns <c>true</c> if the pixel is transparent (<c>alpha == 0</c>).
        /// </summary>
        internal static bool IsTransparent(int c) {
            return ((c >> 24) & 0xFF) == 0;
        }

        /// <summary>
        /// Crops the non-transparent bounding box out of the 80x80 scratch buffer into a compact <see cref="SpriteTile"/>.
        /// </summary>
        internal static SpriteTile BuildSpriteTile(int[] scratchTile) {
            int tileSize = CanvasConstants.TILE_SIZE;
            int minX = tileSize, maxX = -1, minY = tileSize, maxY = -1;

            for (int y = 0; y < tileSize; y++) {
                int row = y * tileSize;

                for (int x = 0; x < tileSize; x++) {
                    if (scratchTile[row + x] != 0) {
                        if (x < minX) {
                            minX = x;
                        }

                        if (x > maxX) {
                            maxX = x;
                        }

                        if (y < minY) {
                            minY = y;
                        }

                        if (y > maxY) {
                            maxY = y;
                        }
                    }
                }
            }

            if (maxX < minX || maxY < minY) {
                return new SpriteTile { MinX = 0, MinY = 0, Width = 1, Height = 1, PremulPixels = new int[1] };
            }

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            int[] pixels = new int[w * h];

            for (int sy = 0; sy < h; sy++) {
                Array.Copy(scratchTile, (minY + sy) * tileSize + minX, pixels, sy * w, w);
            }

            return new SpriteTile { MinX = minX, MinY = minY, Width = w, Height = h, PremulPixels = pixels };
        }

        /// <summary>
        /// Crops the non-transparent bounding box out of the 80x80 scratch buffer into a pre-allocated
        /// <paramref name="targetTile"/> without allocating a new pixel array on the 60 Hz render loop.
        /// </summary>
        internal static SpriteTile BuildSpriteTileInto(int[] scratchTile, SpriteTile targetTile) {
            if (targetTile == null) {
                return BuildSpriteTile(scratchTile);
            }

            int tileSize = CanvasConstants.TILE_SIZE;
            int minX = tileSize, maxX = -1, minY = tileSize, maxY = -1;

            for (int y = 0; y < tileSize; y++) {
                int row = y * tileSize;

                for (int x = 0; x < tileSize; x++) {
                    if (scratchTile[row + x] != 0) {
                        if (x < minX) {
                            minX = x;
                        }

                        if (x > maxX) {
                            maxX = x;
                        }

                        if (y < minY) {
                            minY = y;
                        }

                        if (y > maxY) {
                            maxY = y;
                        }
                    }
                }
            }

            if (targetTile.PremulPixels == null || targetTile.PremulPixels.Length < tileSize * tileSize) {
                targetTile.PremulPixels = new int[tileSize * tileSize];
            }

            if (maxX < minX || maxY < minY) {
                targetTile.MinX = 0;
                targetTile.MinY = 0;
                targetTile.Width = 1;
                targetTile.Height = 1;
                targetTile.PremulPixels[0] = 0;
                return targetTile;
            }

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;

            for (int sy = 0; sy < h; sy++) {
                Array.Copy(scratchTile, (minY + sy) * tileSize + minX, targetTile.PremulPixels, sy * w, w);
            }

            targetTile.MinX = minX;
            targetTile.MinY = minY;
            targetTile.Width = w;
            targetTile.Height = h;
            return targetTile;
        }

        /// <summary>
        /// Extracts a walk, stand, or guard frame and automatically aligns Kirby's feet to
        /// <c>(PIVOT_X, PIVOT_Y)</c> so abilities with tall hats or wide weapons don't shift his body position.
        /// </summary>
        private static SpriteTile ExtractAlignedTile(FastSheet rawSheet, Rectangle r, int[] scratch, bool isGuard = false) {
            // Pass 1: Find the lowest non-transparent row (maxY) corresponding to the bottom of Kirby's feet.
            int maxY = r.Height - 1;

            for (int dy = 0; dy < r.Height; dy++) {
                for (int dx = 0; dx < r.Width; dx++) {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);

                    if (!IsTransparent(c)) {
                        maxY = dy;
                    }
                }
            }

            // Pass 2: Scan only the bottom 4 rows (maxY - 3 .. maxY) to find the horizontal span of Kirby's feet.
            // This ignores wide hats/parasols/hammers higher up on the sprite that would otherwise skew horizontal centering.
            int feetMinX = r.Width, feetMaxX = 0;
            int startY = Math.Max(0, maxY - 3);

            for (int dy = startY; dy <= maxY; dy++) {
                for (int dx = 0; dx < r.Width; dx++) {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);

                    if (!IsTransparent(c)) {
                        if ((!isGuard || dy >= maxY - 1) && dx < feetMinX) {
                            feetMinX = dx;
                        }

                        if (dx > feetMaxX) {
                            feetMaxX = dx;
                        }
                    }
                }
            }

            int bodyPivotX = (feetMinX + feetMaxX) / 2;
            int destX = CanvasConstants.PIVOT_X - bodyPivotX;
            int destY = CanvasConstants.PIVOT_Y - maxY;

            Array.Clear(scratch, 0, scratch.Length);

            for (int dy = 0; dy < r.Height; dy++) {
                for (int dx = 0; dx < r.Width; dx++) {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);

                    if (!IsTransparent(c)) {
                        int tx = destX + dx;
                        int ty = destY + dy;

                        if (tx >= 0 && tx < CanvasConstants.TILE_SIZE && ty >= 0 && ty < CanvasConstants.TILE_SIZE) {
                            scratch[ty * CanvasConstants.TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                        }
                    }
                }
            }

            return BuildSpriteTile(scratch);
        }

        /// <summary>
        /// Synthesizes Mirror Kirby's defensive barrier by compositing three separate spritesheet elements:
        /// 1. Guarding Mirror Kirby at his floor-aligned pivot (<see cref="FormDefinitions.MirrorGuardBodyRect"/>),
        /// 2. The outer reflective bubble rim (<see cref="FormDefinitions.MirrorGuardBubbleRect"/>) at 96% opacity, and
        /// 3. One of 15 scrolling rainbow gradient tiles (<paramref name="rainRect"/>) blended at 45% over Kirby and 68% over the interior.
        /// </summary>
        private static SpriteTile ExtractMirrorGuardTile(FastSheet rawSheet, Rectangle rainRect, int[] scratch) {
            Rectangle kirbyRect = FormDefinitions.MirrorGuardBodyRect;
            Rectangle bubbleRect = FormDefinitions.MirrorGuardBubbleRect;

            // Place Guard Mirror Kirby at his exact floor-aligned position (bodyPivotX=17, maxY=25)
            // and center the 30x32 bubble symmetrically around him (extending 3px below his feet).
            int kirbyDestX = CanvasConstants.PIVOT_X - 17;
            int kirbyDestY = CanvasConstants.PIVOT_Y - 25;
            int sphereDestX = kirbyDestX;
            int sphereDestY = kirbyDestY - 4;

            Array.Clear(scratch, 0, scratch.Length);

            for (int dy = 0; dy < rainRect.Height; dy++) {
                for (int dx = 0; dx < rainRect.Width; dx++) {
                    int rc = rawSheet.GetPixel(rainRect.X + dx, rainRect.Y + dy);

                    if (IsTransparent(rc)) {
                        continue;
                    }

                    int tx = sphereDestX + dx;
                    int ty = sphereDestY + dy;

                    if (tx < 0 || tx >= CanvasConstants.TILE_SIZE || ty < 0 || ty >= CanvasConstants.TILE_SIZE) {
                        continue;
                    }

                    int kdx = tx - kirbyDestX;
                    int kdy = ty - kirbyDestY;
                    bool hasKirby = false;
                    int kc = 0;

                    if (kdx >= 0 && kdx < kirbyRect.Width && kdy >= 0 && kdy < kirbyRect.Height) {
                        kc = rawSheet.GetPixel(kirbyRect.X + kdx, kirbyRect.Y + kdy);

                        if (!IsTransparent(kc)) {
                            hasKirby = true;
                        }
                    }

                    int bc = rawSheet.GetPixel(bubbleRect.X + dx, bubbleRect.Y + dy);
                    bool hasBubbleRim = !IsTransparent(bc);

                    if (hasBubbleRim) {
                        int br = (bc >> 16) & 0xFF;
                        int bg = (bc >> 8) & 0xFF;
                        int bb = bc & 0xFF;

                        // Clean up orange/magenta compression artifacts on the bubble rim into crisp sky-blue.
                        if (br > 180 && bg < 200) {
                            br = 80;
                            bg = 180;
                            bb = 248;
                        }

                        scratch[ty * CanvasConstants.TILE_SIZE + tx] = MakePremulArgb(245, br, bg, bb);
                    }
                    else {
                        int rr = (rc >> 16) & 0xFF;
                        int rg = (rc >> 8) & 0xFF;
                        int rb = rc & 0xFF;

                        // Smooth out the 3 pure-white stripe bands between Orange and Yellow into warm golden-amber.
                        if (rr >= 248 && rg >= 248 && rb >= 248) {
                            rr = 248;
                            rg = 200;
                            rb = 64;
                        }

                        if (hasKirby) {
                            int kr = (kc >> 16) & 0xFF;
                            int kg = (kc >> 8) & 0xFF;
                            int kb = kc & 0xFF;
                            int nr = (int)(kr * 0.55 + rr * 0.45);
                            int ng = (int)(kg * 0.55 + rg * 0.45);
                            int nb = (int)(kb * 0.55 + rb * 0.45);
                            scratch[ty * CanvasConstants.TILE_SIZE + tx] = MakePremulArgb(255, nr, ng, nb);
                        }
                        else {
                            scratch[ty * CanvasConstants.TILE_SIZE + tx] = MakePremulArgb(175, rr, rg, rb);
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
        private static SpriteTile[] ExtractJumpTiles(FastSheet rawSheet, Rectangle[] jumpCoords, int[] scratch) {
            if (jumpCoords == null || jumpCoords.Length == 0) {
                return null;
            }

            SpriteTile[] tiles = new SpriteTile[jumpCoords.Length];
            int refBottomY = jumpCoords[0].Bottom - 1;

            for (int i = 0; i < jumpCoords.Length; i++) {
                Rectangle r = jumpCoords[i];
                int minX = r.Width, maxX = 0;
                // Frames 1 and 2 are upside-down mid-air somersault poses, so Kirby's feet are at the top of the crop.
                bool isFlip = i == 1 || i == 2;
                int yStart = isFlip ? 0 : Math.Max(0, r.Height - 4);
                int yEnd = isFlip ? Math.Min(r.Height - 1, 10) : r.Height - 1;

                for (int dy = yStart; dy <= yEnd; dy++) {
                    for (int dx = 0; dx < r.Width; dx++) {
                        int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);

                        if (!IsTransparent(c)) {
                            if (dx < minX) {
                                minX = dx;
                            }

                            if (dx > maxX) {
                                maxX = dx;
                            }
                        }
                    }
                }

                int bodyPivotX = minX <= maxX ? (minX + maxX) / 2 : r.Width / 2;
                int destX = CanvasConstants.PIVOT_X - bodyPivotX;
                int destY = CanvasConstants.PIVOT_Y - (refBottomY - r.Y);

                Array.Clear(scratch, 0, scratch.Length);

                for (int dy = 0; dy < r.Height; dy++) {
                    for (int dx = 0; dx < r.Width; dx++) {
                        int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);

                        if (!IsTransparent(c)) {
                            int tx = destX + dx;
                            int ty = destY + dy;

                            if (tx >= 0 && tx < CanvasConstants.TILE_SIZE && ty >= 0 && ty < CanvasConstants.TILE_SIZE) {
                                scratch[ty * CanvasConstants.TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                            }
                        }
                    }
                }

                tiles[i] = BuildSpriteTile(scratch);
            }

            return tiles;
        }

        /// <summary>
        /// Extracts a sprite frame and centers its non-transparent bounding box on <c>(PIVOT_X, PIVOT_Y)</c>
        /// (used for projectiles or floating/flying characters with <see cref="PivotMode.Center"/>).
        /// </summary>
        private static SpriteTile ExtractCenteredTile(FastSheet rawSheet, Rectangle r, int[] scratch) {
            int minX = r.Width, maxX = -1, minY = r.Height, maxY = -1;

            for (int dy = 0; dy < r.Height; dy++) {
                for (int dx = 0; dx < r.Width; dx++) {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);

                    if (!IsTransparent(c)) {
                        if (dx < minX) {
                            minX = dx;
                        }

                        if (dx > maxX) {
                            maxX = dx;
                        }

                        if (dy < minY) {
                            minY = dy;
                        }

                        if (dy > maxY) {
                            maxY = dy;
                        }
                    }
                }
            }

            int centerX = minX <= maxX ? (minX + maxX) / 2 : r.Width / 2;
            int centerY = minY <= maxY ? (minY + maxY) / 2 : r.Height / 2;
            return ExtractCustomPivotTile(rawSheet, r, centerX, centerY, scratch);
        }

        /// <summary>
        /// Extracts a sprite frame aligned to an explicit <c>(pivotX, pivotY)</c> coordinate within the source rectangle.
        /// When <paramref name="pivotY"/> is negative, aligns the bottom edge of the rectangle (<c>r.Height - 1</c>) to <c>PIVOT_Y</c>.
        /// </summary>
        private static SpriteTile ExtractCustomPivotTile(FastSheet rawSheet, Rectangle r, int pivotX, int pivotY,
            int[] scratch) {
            int actualPivotY = pivotY < 0 ? r.Height - 1 : pivotY;
            int destX = CanvasConstants.PIVOT_X - pivotX;
            int destY = CanvasConstants.PIVOT_Y - actualPivotY;

            Array.Clear(scratch, 0, scratch.Length);

            for (int dy = 0; dy < r.Height; dy++) {
                for (int dx = 0; dx < r.Width; dx++) {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);

                    if (!IsTransparent(c)) {
                        int tx = destX + dx;
                        int ty = destY + dy;

                        if (tx >= 0 && tx < CanvasConstants.TILE_SIZE && ty >= 0 && ty < CanvasConstants.TILE_SIZE) {
                            scratch[ty * CanvasConstants.TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                        }
                    }
                }
            }

            return BuildSpriteTile(scratch);
        }

        /// <summary>
        /// Crops the 24x24 Normal Kirby walk frame (<see cref="FormDefinitions.NormalTrayIconRect"/>) into a GDI HICON for the Windows system tray.
        /// Returns the raw <paramref name="iconHandle"/> so it can be freed via <c>DestroyIcon</c> on exit.
        /// </summary>
        private static void ExtractTrayIconFromNormalSheet(FastSheet normalSheet, out Icon icon, out IntPtr iconHandle) {
            icon = null;
            iconHandle = IntPtr.Zero;

            Rectangle iconRect = FormDefinitions.NormalTrayIconRect;

            if (normalSheet == null || normalSheet.Width < iconRect.Right || normalSheet.Height < iconRect.Bottom) {
                return;
            }

            try {
                using (Bitmap crop = new Bitmap(iconRect.Width, iconRect.Height, PixelFormat.Format32bppArgb)) {
                    for (int y = 0; y < iconRect.Height; y++) {
                        for (int x = 0; x < iconRect.Width; x++) {
                            int c = normalSheet.GetPixel(iconRect.X + x, iconRect.Y + y);

                            if (IsTransparent(c)) {
                                crop.SetPixel(x, y, Color.Transparent);
                            }
                            else {
                                crop.SetPixel(x, y, Color.FromArgb(255, (c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF));
                            }
                        }
                    }

                    iconHandle = crop.GetHicon();
                    icon = Icon.FromHandle(iconHandle);
                }
            }
            catch (ExternalException ex) {
                CleanupFailedTrayIcon(ref icon, ref iconHandle);
                Diagnostics.LogWarning("SpriteLoader.ExtractTrayIconFromNormalSheet", ex);
            }
            catch (ArgumentException ex) {
                CleanupFailedTrayIcon(ref icon, ref iconHandle);
                Diagnostics.LogWarning("SpriteLoader.ExtractTrayIconFromNormalSheet", ex);
            }
        }

        private static void CleanupFailedTrayIcon(ref Icon icon, ref IntPtr iconHandle) {
            icon = null;

            if (iconHandle == IntPtr.Zero) {
                return;
            }

            NativeMethods.DestroyIcon(iconHandle);
            iconHandle = IntPtr.Zero;
        }

    }

}