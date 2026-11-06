using System.Collections.Generic;
using System.Drawing;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// States for Kirby's idle sleep state machine.
    /// Transitions: Awake -> Halting -> FallingAsleep -> Sleeping -> WakingUp -> PostWakeStanding -> Awake.
    /// </summary>
    public enum SleepState {

        Awake,
        Halting,
        FallingAsleep,
        Sleeping,
        WakingUp,
        PostWakeStanding

    }

    /// <summary>
    /// States for Normal Kirby's enemy inhale and swallow sequence.
    /// </summary>
    public enum SwallowState {

        None,
        Inhaling,
        Mouthful,
        Swallowing

    }

    /// <summary>
    /// Controls how <see cref="SpriteLoader"/> aligns extracted spritesheet frames onto the 80x80 logical canvas.
    /// </summary>
    public enum PivotMode {

        /// <summary>
        /// Aligns the lowest non-background pixel row to <c>PIVOT_Y</c> and centers horizontally using
        /// only the bottom 4 rows so tall hats or raised weapons do not shift the character's body.
        /// </summary>
        FeetBottom4Rows,

        /// <summary>
        /// Aligns the lowest non-background pixel row to <c>PIVOT_Y</c> and restricts the left-edge scan
        /// to the bottom 2 rows for defensive guard stances.
        /// </summary>
        GuardBottom2Rows,

        /// <summary>
        /// Aligns all frames to Frame 0's bottom edge and scans the top rows on somersault frames (indices 1 and 2).
        /// </summary>
        JumpArc,

        /// <summary>
        /// Composites Mirror Kirby's guarding body, outer reflective sphere rim, and scrolling rainbow gradient.
        /// </summary>
        MirrorGuard,

        /// <summary>
        /// Centers the non-background sprite bounds horizontally and vertically on <c>(PIVOT_X, PIVOT_Y)</c>
        /// (useful for projectiles or floating/flying characters).
        /// </summary>
        Center,

        /// <summary>
        /// Aligns an explicit pixel coordinate <c>(CustomPivotX, CustomPivotY)</c> within each frame to <c>(PIVOT_X, PIVOT_Y)</c>.
        /// When <c>CustomPivotY &lt; 0</c>, aligns the bottom row of the source rectangle (<c>Height - 1</c>) to <c>PIVOT_Y</c>.
        /// </summary>
        CustomPivot

    }

    /// <summary>
    /// State of a single black-and-white air particle during Normal Kirby's inhale animation,
    /// tracked in unscaled 1x pixel offsets (<see cref="Dx"/>, <see cref="Dy"/>) relative to Kirby's open mouth.
    /// </summary>
    public struct InhaleParticle {

        public double Dx;
        public double Dy;
        public bool BlackOnTop;

    }

    /// <summary>
    /// Self-contained definition and runtime tile storage for a single sprite animation clip
    /// (such as Walk, Guard, Jump, Stand, Sleep, or future Attack and Enemy/Helper animations).
    /// </summary>
    public sealed class AnimClip {

        public Rectangle[] SourceRects;
        public int[] Sequence;
        public double Speed;
        public bool Loop;
        public PivotMode Pivot;
        public int CustomPivotX;
        public int CustomPivotY;

        // Unique extracted tiles corresponding 1:1 with SourceRects:
        public SpriteTile[] RawFrames;

        // Playback sequence of tiles (expanded via Sequence if specified, reusing RawFrames references):
        public SpriteTile[] Frames;

        public bool HasFrames(int minCount = 1) {
            return Frames != null && Frames.Length >= minCount;
        }

    }

    /// <summary>
    /// Defines a Kirby copy ability, its trigger key, its source spritesheet filename,
    /// and its <see cref="AnimClip"/> definitions.
    /// </summary>
    public sealed class FormDef {

        public string Name;
        public string Key;
        public string FileName;

        public AnimClip Walk;
        public AnimClip Guard;
        public AnimClip Jump;
        public AnimClip Stand;
        public AnimClip Inhale;
        public AnimClip Mouthful;
        public AnimClip Swallow;
        public AnimClip SwallowAbility;

    }

    /// <summary>
    /// Defines an enemy type, its source spritesheet filename, movement speed,
    /// optional granted copy ability name (<see cref="CopyAbility"/>), and its <see cref="AnimClip"/> definitions.
    /// </summary>
    public sealed class EnemyDef {

        public string Name;
        public string FileName;
        public double WalkSpeed;
        public string CopyAbility;

        public AnimClip Walk;
        public AnimClip Inhaled;

    }

    /// <summary>
    /// Catalog of spritesheet coordinates, builder helpers, and frame sequences for all 19 copy abilities and sleep animations.
    /// </summary>
    public static class FormDefinitions {

        // Standard 6-frame ping-pong walk sequence expanding 4 source walk frames into [0, 1, 2, 3, 2, 1]:
        public static readonly int[] WalkPingPongSeq = new[] { 0, 1, 2, 3, 2, 1 };

        /// <summary>
        /// Source rectangles in <c>kirby-sleep.png</c> for nodding off, breathing in sleep, and startled wake-up.
        /// </summary>
        public static readonly Rectangle[] SleepCoords = new[]
        {
            R(4, 6, 30, 28), // 0: Falling asleep (eyes closed)
            R(36, 6, 30, 28), // 1: Falling asleep (eyes open)
            R(68, 12, 30, 22), // 2: Sleeping 1 (inhale peak)
            R(99, 12, 30, 22), // 3: Sleeping 2
            R(130, 13, 30, 21), // 4: Sleeping 3
            R(162, 14, 30, 20), // 5: Sleeping 4 (exhale low)
            R(194, 6, 30, 28), // 6: Waking up (eyes closed)
            R(226, 6, 30, 28) // 7: Waking up (eyes open)
        };

        // Frame index sequences driving the multi-stage sleep/wake transition animations:
        public static readonly int[]
            HaltStandSeq = new[] { 0, 0, 1, 0, 0, 1, 0 }; // Blink twice in current ability before nodding off

        public static readonly int[] FallAsleepSeq = new[] { 1, 1, 0, 1, 1, 0, 1, 1, 0 }; // Heavy eyelids in sleep cap
        public static readonly int[] SleepLoopSeq = new[] { 2, 3, 4, 5, 4, 3 }; // Continuous ping-pong breathing loop
        public static readonly int[] WakeUpSeq = new[] { 6, 7, 6, 7, 6, 7 }; // Startled wake-up flutter

        public static readonly int[]
            PostWakeStandSeq = new[] { 0, 1, 0 }; // Brief blink in current ability before walking

        // 0-based frame index sequence for swallowing a regular enemy without a copy ability (frames 1, 3, 5):
        public static readonly int[] SwallowNoAbilitySeq = new[] { 0, 2, 4 };

        // 0-based frame index sequence for swallowing an enemy that grants a copy ability
        // (1-based: 1 1 1 2 2 1 1 1 1 2 3 3 2 2 3 4 3 3 2 4 4 followed by 16 frames of 5):
        public static readonly int[] SwallowAbilitySeq = new[]
        {
            0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 2, 2, 1, 1, 2, 3, 2, 2, 1, 3, 3,
            4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4
        };

        /// <summary>
        /// Shorthand builder for a single spritesheet <see cref="Rectangle"/>.
        /// </summary>
        public static Rectangle R(int x, int y, int w, int h) {
            return new Rectangle(x, y, w, h);
        }

        /// <summary>
        /// Builds an array of spritesheet rectangles sharing the same <paramref name="y"/>, <paramref name="w"/>,
        /// and <paramref name="h"/> across arbitrary horizontal X coordinates.
        /// </summary>
        public static Rectangle[] Row(int y, int w, int h, params int[] xs) {
            Rectangle[] rects = new Rectangle[xs.Length];

            for (int i = 0; i < xs.Length; i++) {
                rects[i] = new Rectangle(xs[i], y, w, h);
            }

            return rects;
        }

        /// <summary>
        /// Builds an array of evenly-spaced spritesheet rectangles stepping by <paramref name="stepX"/> and <paramref name="stepY"/>.
        /// </summary>
        public static Rectangle[] Strip(int startX, int startY, int w, int h, int count, int stepX, int stepY = 0) {
            Rectangle[] rects = new Rectangle[count];

            for (int i = 0; i < count; i++) {
                rects[i] = new Rectangle(startX + i * stepX, startY + i * stepY, w, h);
            }

            return rects;
        }

        /// <summary>
        /// General-purpose builder for custom <see cref="AnimClip"/> definitions (attacks, projectiles, enemies, helpers).
        /// </summary>
        public static AnimClip Clip(
            Rectangle[] rects,
            double speed,
            bool loop,
            PivotMode pivot = PivotMode.FeetBottom4Rows,
            int[] sequence = null,
            int customPivotX = 0,
            int customPivotY = -1) {
            return new AnimClip
            {
                SourceRects = rects,
                Sequence = sequence,
                Speed = speed,
                Loop = loop,
                Pivot = pivot,
                CustomPivotX = customPivotX,
                CustomPivotY = customPivotY
            };
        }

        public static AnimClip WalkClip(params Rectangle[] rects) {
            return Clip(rects, 0.1, true, PivotMode.FeetBottom4Rows, WalkPingPongSeq);
        }

        public static AnimClip EnemyWalkClip(params Rectangle[] rects) {
            return Clip(rects, 0.08, true);
        }

        public static AnimClip GuardClip(params Rectangle[] rects) {
            return Clip(rects, 0.25, true, PivotMode.GuardBottom2Rows);
        }

        public static AnimClip MirrorGuardClip(params Rectangle[] rects) {
            return Clip(rects, 0.25, true, PivotMode.MirrorGuard);
        }

        public static AnimClip JumpClip(params Rectangle[] rects) {
            return Clip(rects, 0.0, false, PivotMode.JumpArc);
        }

        public static AnimClip StandClip(params Rectangle[] rects) {
            return Clip(rects, 0.05, false);
        }

        public static AnimClip VfxClip(double speed, bool loop, params Rectangle[] rects) {
            return Clip(rects, speed, loop, PivotMode.Center);
        }

        /// <summary>
        /// Registers all 19 Kirby copy abilities and their spritesheet animation clips.
        /// </summary>
        public static void PopulateForms(List<FormDef> forms, Dictionary<string, FormDef> keyToForm, out FormDef normalForm) {
            normalForm = null;

            AddForm(forms, keyToForm, ref normalForm, "normal", "0", "kirby-normal.png",
                WalkClip(Row(125, 24, 24, 32, 60, 87, 114)),
                GuardClip(Row(263, 26, 25, 79, 108)),
                JumpClip(R(66, 193, 23, 24), R(94, 193, 23, 23), R(122, 194, 21, 22), R(147, 193, 24, 23),
                    R(176, 193, 23, 24)),
                StandClip(Row(55, 24, 23, 113, 142)));

            if (normalForm != null) {
                Rectangle[] swallowRects = new[]
                {
                    R(185, 86, 29, 32),
                    R(219, 90, 32, 28),
                    R(256, 96, 39, 22),
                    R(300, 99, 32, 19),
                    R(337, 102, 23, 16)
                };

                normalForm.Inhale = Clip(new[] { R(22, 94, 21, 24), R(48, 86, 21, 32) }, 0.15, false);
                normalForm.Mouthful = Clip(new[] { R(111, 87, 32, 31) }, 0.08, false);
                normalForm.Swallow = Clip(swallowRects, 0.15, false, PivotMode.FeetBottom4Rows, SwallowNoAbilitySeq);
                normalForm.SwallowAbility = Clip(swallowRects, 1.0, false, PivotMode.FeetBottom4Rows, SwallowAbilitySeq);
            }

            AddForm(forms, keyToForm, ref normalForm, "ice", "l", "kirby-ice.png",
                WalkClip(Row(213, 24, 34, 6, 35, 63, 92)),
                GuardClip(Strip(42, 605, 34, 26, 3, 0, 37)),
                JumpClip(R(6, 372, 24, 33), R(35, 381, 26, 24), R(66, 381, 26, 27), R(97, 372, 25, 32), R(127, 365, 23, 40)),
                StandClip(Row(84, 24, 32, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "bomb", "b", "kirby-bomb.png",
                WalkClip(R(0, 101, 31, 29), R(32, 101, 32, 29), R(68, 101, 32, 29), R(98, 101, 32, 29)),
                GuardClip(Row(103, 32, 28, 304, 339)),
                JumpClip(R(6, 174, 27, 30), R(38, 180, 23, 23), R(66, 180, 21, 27), R(92, 178, 28, 25), R(125, 168, 23, 36)),
                StandClip(Row(34, 29, 28, 40, 74)));

            AddForm(forms, keyToForm, ref normalForm, "beam", "e", "kirby-beam.png",
                WalkClip(Row(101, 27, 37, 5, 35, 65, 96)),
                GuardClip(Row(348, 31, 27, 5, 39)),
                JumpClip(R(6, 193, 26, 30), R(37, 199, 27, 24), R(69, 199, 23, 29), R(97, 191, 28, 31), R(130, 184, 24, 39)),
                StandClip(Row(29, 26, 31, 37, 68)));

            AddForm(forms, keyToForm, ref normalForm, "cutter", "c", "kirby-cutter.png",
                WalkClip(Row(126, 29, 35, 4, 34, 66, 100)),
                GuardClip(Row(413, 28, 30, 5, 36)),
                JumpClip(R(6, 214, 23, 37), R(34, 227, 23, 28), R(62, 227, 23, 33), R(90, 221, 27, 29), R(122, 216, 24, 35)),
                StandClip(Row(34, 24, 34, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "fighter", "f", "kirby-fighter.png",
                WalkClip(Strip(6, 95, 26, 24, 4, 30)),
                GuardClip(Row(333, 29, 25, 5, 37)),
                JumpClip(R(6, 176, 25, 24), R(36, 171, 23, 28), R(64, 175, 21, 23), R(90, 176, 26, 23), R(121, 169, 23, 31)),
                StandClip(Row(27, 24, 23, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "fire", "d", "kirby-fire.png",
                WalkClip(Row(309, 24, 34, 6, 35, 63, 92)),
                GuardClip(Row(941, 28, 31, 5, 36)),
                JumpClip(R(6, 529, 24, 33), R(35, 538, 26, 24), R(66, 538, 26, 27), R(97, 529, 25, 32), R(127, 522, 23, 40)),
                StandClip(Row(134, 24, 32, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "hammer", "h", "kirby-hammer.png",
                WalkClip(Strip(5, 164, 44, 49, 4, 47)),
                GuardClip(Row(426, 29, 34, 5, 37)),
                JumpClip(R(8, 240, 42, 43), R(82, 258, 31, 42), R(132, 259, 27, 45), R(174, 239, 44, 42),
                    R(236, 234, 37, 47)),
                StandClip(Row(100, 36, 40, 53, 94)));

            AddForm(forms, keyToForm, ref normalForm, "jet", "j", "kirby-jet.png",
                WalkClip(Row(114, 28, 29, 6, 39, 70, 101)),
                GuardClip(Row(283, 30, 28, 5, 38)),
                JumpClip(R(6, 195, 26, 29), R(37, 200, 24, 23), R(66, 200, 26, 24), R(97, 197, 27, 26), R(129, 193, 25, 31)),
                StandClip(Row(32, 29, 28, 40, 74)));

            // Mirror Kirby's guard uses a 15-frame scrolling rainbow palette animation
            // composited inside a reflective sphere (handled via PivotMode.MirrorGuard in SpriteLoader).
            AddForm(forms, keyToForm, ref normalForm, "mirror", "m", "kirby-mirror.png",
                WalkClip(Row(117, 27, 37, 5, 35, 65, 96)),
                MirrorGuardClip(Strip(220, 877, 32, 34, 15, 35)),
                JumpClip(R(6, 272, 26, 30), R(37, 278, 27, 24), R(69, 278, 23, 29), R(97, 270, 28, 31), R(130, 263, 24, 39)),
                StandClip(Row(44, 26, 31, 37, 68)));

            AddForm(forms, keyToForm, ref normalForm, "ninja", "n", "kirby-ninja.png",
                WalkClip(Row(45, 27, 32, 207, 235, 264, 296)),
                GuardClip(Row(234, 28, 26, 5, 36)),
                JumpClip(R(6, 136, 23, 33), R(34, 145, 23, 24), R(62, 145, 21, 31), R(90, 141, 24, 27), R(119, 130, 23, 39)),
                StandClip(Row(43, 24, 30, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "paint", "p", "kirby-paint.png",
                WalkClip(Row(113, 26, 28, 5, 34, 62, 92)),
                GuardClip(Row(196, 26, 25, 181, 210)),
                JumpClip(R(6, 195, 24, 28), R(35, 198, 23, 24), R(63, 199, 22, 25), R(90, 196, 24, 26), R(119, 195, 23, 28)),
                StandClip(Row(29, 24, 27, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "parasol", "r", "kirby-parasol.png",
                WalkClip(Strip(269, 92, 42, 49, 4, 45)),
                GuardClip(Row(116, 29, 25, 669, 701)),
                JumpClip(R(11, 167, 37, 44), R(77, 186, 30, 41), R(130, 187, 21, 45), R(165, 168, 42, 41),
                    R(229, 162, 30, 47)),
                StandClip(Row(91, 31, 46, 42, 78)));

            AddForm(forms, keyToForm, ref normalForm, "plasma", "a", "kirby-plasma.png",
                WalkClip(Row(357, 24, 34, 6, 35, 63, 92)),
                GuardClip(Row(1078, 28, 29, 5, 36)),
                JumpClip(R(6, 608, 24, 33), R(35, 617, 26, 24), R(66, 617, 26, 27), R(97, 608, 25, 32), R(127, 601, 23, 40)),
                StandClip(Row(159, 24, 32, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "stone", "s", "kirby-stone.png",
                WalkClip(Row(123, 27, 32, 5, 33, 62, 94)),
                GuardClip(Row(406, 28, 25, 5, 36)),
                JumpClip(R(6, 214, 23, 33), R(34, 223, 23, 24), R(62, 223, 21, 31), R(90, 219, 24, 27), R(119, 208, 23, 39)),
                StandClip(Row(29, 24, 30, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "suplex", "u", "kirby-suplex.png",
                WalkClip(Strip(6, 95, 26, 24, 4, 30)),
                GuardClip(Row(333, 29, 25, 5, 37)),
                JumpClip(R(6, 176, 25, 24), R(36, 171, 23, 28), R(64, 175, 21, 23), R(90, 176, 26, 23), R(121, 169, 23, 31)),
                StandClip(Row(27, 24, 23, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "sword", "w", "kirby-sword.png",
                WalkClip(Row(101, 32, 30, 3, 34, 67, 101)),
                GuardClip(Row(321, 32, 28, 5, 40)),
                JumpClip(R(6, 174, 27, 30), R(38, 180, 23, 23), R(66, 180, 21, 27), R(92, 178, 28, 25), R(125, 168, 23, 36)),
                StandClip(Row(34, 29, 28, 40, 74)));

            AddForm(forms, keyToForm, ref normalForm, "wheel", "v", "kirby-wheel.png",
                WalkClip(Row(121, 26, 28, 5, 34, 62, 92)),
                GuardClip(Row(204, 26, 25, 152, 181)),
                JumpClip(R(6, 203, 24, 28), R(35, 206, 23, 24), R(63, 207, 22, 25), R(90, 204, 24, 26), R(119, 203, 23, 28)),
                StandClip(Row(37, 24, 27, 35, 64)));

            AddForm(forms, keyToForm, ref normalForm, "wing", "g", "kirby-wing.png",
                WalkClip(Row(127, 31, 32, 4, 35, 66, 100)),
                GuardClip(Row(448, 30, 29, 5, 38)),
                JumpClip(R(6, 214, 26, 31), R(37, 220, 24, 23), R(66, 222, 25, 30), R(96, 217, 28, 27), R(129, 212, 23, 32)),
                StandClip(Row(37, 28, 32, 39, 72)));

            AddForm(forms, keyToForm, ref normalForm, "yoyo", "y", "kirby-yoyo.png",
                WalkClip(Row(121, 26, 28, 5, 34, 62, 92)),
                GuardClip(Row(204, 26, 25, 152, 181)),
                JumpClip(R(6, 203, 24, 28), R(35, 206, 23, 24), R(63, 207, 22, 25), R(90, 204, 24, 26), R(119, 203, 23, 28)),
                StandClip(Row(37, 24, 27, 35, 64)));
        }

        /// <summary>
        /// Registers enemy types and their spritesheet animation clips.
        /// </summary>
        public static void PopulateEnemies(List<EnemyDef> enemies) {
            if (enemies == null) {
                return;
            }

            enemies.Add(new EnemyDef
            {
                Name = "waddle-dee",
                FileName = "enemy-waddle-dee.png",
                WalkSpeed = 0.55,
                CopyAbility = null,
                Walk = EnemyWalkClip(R(5, 57, 24, 24), R(33, 57, 22, 24)),
                Inhaled = Clip(new[] { R(59, 57, 24, 24) }, 0.0, false)
            });

            enemies.Add(new EnemyDef
            {
                Name = "waddle-doo",
                FileName = "enemy-waddle-dee.png",
                WalkSpeed = 0.55,
                CopyAbility = "beam",
                Walk = EnemyWalkClip(R(5, 84, 24, 24), R(32, 84, 23, 24)),
                Inhaled = Clip(new[] { R(252, 84, 24, 24) }, 0.0, false)
            });
        }

        private static void AddForm(
            List<FormDef> forms,
            Dictionary<string, FormDef> keyToForm,
            ref FormDef normalForm,
            string name,
            string key,
            string file,
            AnimClip walk,
            AnimClip guard,
            AnimClip jump,
            AnimClip stand) {
            FormDef def = new FormDef
            {
                Name = name,
                Key = key,
                FileName = file,
                Walk = walk,
                Guard = guard,
                Jump = jump,
                Stand = stand
            };
            forms.Add(def);
            keyToForm[key] = def;

            if (name == "normal") {
                normalForm = def;
            }
        }

    }

}