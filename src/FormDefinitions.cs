using System.Collections.Generic;
using System.Drawing;

namespace KirbyTaskbarBuddy
{
    /// <summary>
    /// States for Kirby's idle sleep state machine.
    /// Transitions: Awake -> Halting -> FallingAsleep -> Sleeping -> WakingUp -> PostWakeStanding -> Awake.
    /// </summary>
    public enum SleepState
    {
        Awake,
        Halting,
        FallingAsleep,
        Sleeping,
        WakingUp,
        PostWakeStanding
    }

    /// <summary>
    /// Defines a Kirby copy ability, its trigger key, its source spritesheet rectangles,
    /// and its extracted runtime <see cref="SpriteTile"/> animation arrays.
    /// </summary>
    public sealed class FormDef
    {
        public string Name;
        public string Key;
        public string FileName;

        // Source rectangles within the ability's PNG spritesheet:
        public Rectangle[] Coords;       // 4 core walk frames (expanded into a 6-frame ping-pong loop)
        public Rectangle[] GuardCoords;  // Cursor-proximity defensive guard frames
        public Rectangle[] JumpCoords;   // 5 jump arc frames (launch, somersault 1, somersault 2, early fall, descent)
        public Rectangle[] StandCoords;  // 2 idle standing/blinking frames (eyes open, eyes closed)

        // Extracted, floor-aligned, premultiplied-ARGB tiles ready for rendering:
        public SpriteTile[] Frames;
        public SpriteTile[] GuardFrames;
        public SpriteTile[] JumpFrames;
        public SpriteTile[] StandFrames;
    }

    /// <summary>
    /// Catalog of spritesheet coordinates and frame index sequences for all 19 copy abilities and sleep animations.
    /// </summary>
    public static class FormDefinitions
    {
        /// <summary>
        /// Source rectangles in <c>kirby-sleep.png</c> for nodding off, breathing in sleep, and startled wake-up.
        /// </summary>
        public static readonly Rectangle[] SleepCoords = new Rectangle[]
        {
            new Rectangle(4, 6, 30, 28),   // 0: Falling asleep (eyes closed)
            new Rectangle(36, 6, 30, 28),  // 1: Falling asleep (eyes open)
            new Rectangle(68, 12, 30, 22), // 2: Sleeping 1 (inhale peak)
            new Rectangle(99, 12, 30, 22), // 3: Sleeping 2
            new Rectangle(130, 13, 30, 21),// 4: Sleeping 3
            new Rectangle(162, 14, 30, 20),// 5: Sleeping 4 (exhale low)
            new Rectangle(194, 6, 30, 28), // 6: Waking up (eyes closed)
            new Rectangle(226, 6, 30, 28)  // 7: Waking up (eyes open)
        };

        // Frame index sequences driving the multi-stage sleep/wake transition animations:
        public static readonly int[] HaltStandSeq = new int[] { 0, 0, 1, 0, 0, 1, 0 };          // Blink twice in current ability before nodding off
        public static readonly int[] FallAsleepSeq = new int[] { 1, 1, 0, 1, 1, 0, 1, 1, 0 };   // Heavy eyelids in sleep cap
        public static readonly int[] SleepLoopSeq = new int[] { 2, 3, 4, 5, 4, 3 };             // Continuous ping-pong breathing loop
        public static readonly int[] WakeUpSeq = new int[] { 6, 7, 6, 7, 6, 7 };                // Startled wake-up flutter
        public static readonly int[] PostWakeStandSeq = new int[] { 0, 1, 0 };                  // Brief blink in current ability before walking

        /// <summary>
        /// Registers all 19 Kirby copy abilities and their spritesheet frame coordinates.
        /// </summary>
        public static void PopulateForms(List<FormDef> forms, Dictionary<string, FormDef> keyToForm, out FormDef normalForm)
        {
            normalForm = null;

            AddForm(forms, keyToForm, ref normalForm, "normal", "0", "kirby-normal.png", new[] {
                new Rectangle(32, 125, 24, 24), new Rectangle(60, 125, 24, 24),
                new Rectangle(87, 125, 24, 24), new Rectangle(114, 125, 24, 24)
            }, new[] {
                new Rectangle(79, 263, 26, 25), new Rectangle(108, 263, 26, 25)
            }, new[] {
                new Rectangle(66, 193, 23, 24), new Rectangle(94, 193, 23, 23),
                new Rectangle(122, 194, 21, 22), new Rectangle(147, 193, 24, 23),
                new Rectangle(176, 193, 23, 24)
            }, new[] {
                new Rectangle(113, 55, 24, 23), new Rectangle(142, 55, 24, 23)
            });

            AddForm(forms, keyToForm, ref normalForm, "ice", "l", "kirby-ice.png", new[] {
                new Rectangle(6, 213, 24, 34), new Rectangle(35, 213, 24, 34),
                new Rectangle(63, 213, 24, 34), new Rectangle(92, 213, 24, 34)
            }, new[] {
                new Rectangle(42, 605, 34, 26), new Rectangle(42, 642, 34, 26),
                new Rectangle(42, 679, 34, 26)
            }, new[] {
                new Rectangle(6, 372, 24, 33), new Rectangle(35, 381, 26, 24),
                new Rectangle(66, 381, 26, 27), new Rectangle(97, 372, 25, 32),
                new Rectangle(127, 365, 23, 40)
            }, new[] {
                new Rectangle(35, 84, 24, 32), new Rectangle(64, 84, 24, 32)
            });

            AddForm(forms, keyToForm, ref normalForm, "bomb", "b", "kirby-bomb.png", new[] {
                new Rectangle(0, 101, 31, 29), new Rectangle(32, 101, 32, 29),
                new Rectangle(68, 101, 32, 29), new Rectangle(98, 101, 32, 29)
            }, new[] {
                new Rectangle(304, 103, 32, 28), new Rectangle(339, 103, 32, 28)
            }, new[] {
                new Rectangle(6, 174, 27, 30), new Rectangle(38, 180, 23, 23),
                new Rectangle(66, 180, 21, 27), new Rectangle(92, 178, 28, 25),
                new Rectangle(125, 168, 23, 36)
            }, new[] {
                new Rectangle(40, 34, 29, 28), new Rectangle(74, 34, 29, 28)
            });

            AddForm(forms, keyToForm, ref normalForm, "beam", "e", "kirby-beam.png", new[] {
                new Rectangle(5, 101, 27, 37), new Rectangle(35, 101, 27, 37),
                new Rectangle(65, 101, 27, 37), new Rectangle(96, 101, 27, 37)
            }, new[] {
                new Rectangle(5, 348, 31, 27), new Rectangle(39, 348, 31, 27)
            }, new[] {
                new Rectangle(6, 193, 26, 30), new Rectangle(37, 199, 27, 24),
                new Rectangle(69, 199, 23, 29), new Rectangle(97, 191, 28, 31),
                new Rectangle(130, 184, 24, 39)
            }, new[] {
                new Rectangle(37, 29, 26, 31), new Rectangle(68, 29, 26, 31)
            });

            AddForm(forms, keyToForm, ref normalForm, "cutter", "c", "kirby-cutter.png", new[] {
                new Rectangle(4, 126, 29, 35), new Rectangle(34, 126, 29, 35),
                new Rectangle(66, 126, 29, 35), new Rectangle(100, 126, 29, 35)
            }, new[] {
                new Rectangle(5, 413, 28, 30), new Rectangle(36, 413, 28, 30)
            }, new[] {
                new Rectangle(6, 214, 23, 37), new Rectangle(34, 227, 23, 28),
                new Rectangle(62, 227, 23, 33), new Rectangle(90, 221, 27, 29),
                new Rectangle(122, 216, 24, 35)
            }, new[] {
                new Rectangle(35, 34, 24, 34), new Rectangle(64, 34, 24, 34)
            });

            AddForm(forms, keyToForm, ref normalForm, "fighter", "f", "kirby-fighter.png", new[] {
                new Rectangle(6, 95, 26, 24), new Rectangle(36, 95, 26, 24),
                new Rectangle(66, 95, 26, 24), new Rectangle(96, 95, 26, 24)
            }, new[] {
                new Rectangle(5, 333, 29, 25), new Rectangle(37, 333, 29, 25)
            }, new[] {
                new Rectangle(6, 176, 25, 24), new Rectangle(36, 171, 23, 28),
                new Rectangle(64, 175, 21, 23), new Rectangle(90, 176, 26, 23),
                new Rectangle(121, 169, 23, 31)
            }, new[] {
                new Rectangle(35, 27, 24, 23), new Rectangle(64, 27, 24, 23)
            });

            AddForm(forms, keyToForm, ref normalForm, "fire", "d", "kirby-fire.png", new[] {
                new Rectangle(6, 309, 24, 34), new Rectangle(35, 309, 24, 34),
                new Rectangle(63, 309, 24, 34), new Rectangle(92, 309, 24, 34)
            }, new[] {
                new Rectangle(5, 941, 28, 31), new Rectangle(36, 941, 28, 31)
            }, new[] {
                new Rectangle(6, 529, 24, 33), new Rectangle(35, 538, 26, 24),
                new Rectangle(66, 538, 26, 27), new Rectangle(97, 529, 25, 32),
                new Rectangle(127, 522, 23, 40)
            }, new[] {
                new Rectangle(35, 134, 24, 32), new Rectangle(64, 134, 24, 32)
            });

            AddForm(forms, keyToForm, ref normalForm, "hammer", "h", "kirby-hammer.png", new[] {
                new Rectangle(5, 164, 44, 49), new Rectangle(52, 164, 44, 49),
                new Rectangle(99, 164, 44, 49), new Rectangle(146, 164, 44, 49)
            }, new[] {
                new Rectangle(5, 426, 29, 34), new Rectangle(37, 426, 29, 34)
            }, new[] {
                new Rectangle(8, 240, 42, 43), new Rectangle(82, 258, 31, 42),
                new Rectangle(132, 259, 27, 45), new Rectangle(174, 239, 44, 42),
                new Rectangle(236, 234, 37, 47)
            }, new[] {
                new Rectangle(53, 100, 36, 40), new Rectangle(94, 100, 36, 40)
            });

            AddForm(forms, keyToForm, ref normalForm, "jet", "j", "kirby-jet.png", new[] {
                new Rectangle(6, 114, 28, 29), new Rectangle(39, 114, 28, 29),
                new Rectangle(70, 114, 28, 29), new Rectangle(101, 114, 28, 29)
            }, new[] {
                new Rectangle(5, 283, 30, 28), new Rectangle(38, 283, 30, 28)
            }, new[] {
                new Rectangle(6, 195, 26, 29), new Rectangle(37, 200, 24, 23),
                new Rectangle(66, 200, 26, 24), new Rectangle(97, 197, 27, 26),
                new Rectangle(129, 193, 25, 31)
            }, new[] {
                new Rectangle(40, 32, 29, 28), new Rectangle(74, 32, 29, 28)
            });

            // Mirror Kirby's guard uses a 15-frame scrolling rainbow palette animation
            // composited inside a reflective sphere (handled specially in SpriteLoader.ExtractMirrorGuardTile).
            AddForm(forms, keyToForm, ref normalForm, "mirror", "m", "kirby-mirror.png", new[] {
                new Rectangle(5, 117, 27, 37), new Rectangle(35, 117, 27, 37),
                new Rectangle(65, 117, 27, 37), new Rectangle(96, 117, 27, 37)
            }, new[] {
                new Rectangle(220, 877, 32, 34), new Rectangle(255, 877, 32, 34),
                new Rectangle(290, 877, 32, 34), new Rectangle(325, 877, 32, 34),
                new Rectangle(360, 877, 32, 34), new Rectangle(395, 877, 32, 34),
                new Rectangle(430, 877, 32, 34), new Rectangle(465, 877, 32, 34),
                new Rectangle(500, 877, 32, 34), new Rectangle(535, 877, 32, 34),
                new Rectangle(570, 877, 32, 34), new Rectangle(605, 877, 32, 34),
                new Rectangle(640, 877, 32, 34), new Rectangle(675, 877, 32, 34),
                new Rectangle(710, 877, 32, 34)
            }, new[] {
                new Rectangle(6, 272, 26, 30), new Rectangle(37, 278, 27, 24),
                new Rectangle(69, 278, 23, 29), new Rectangle(97, 270, 28, 31),
                new Rectangle(130, 263, 24, 39)
            }, new[] {
                new Rectangle(37, 44, 26, 31), new Rectangle(68, 44, 26, 31)
            });

            AddForm(forms, keyToForm, ref normalForm, "ninja", "n", "kirby-ninja.png", new[] {
                new Rectangle(207, 45, 27, 32), new Rectangle(235, 45, 27, 32),
                new Rectangle(264, 45, 27, 32), new Rectangle(296, 45, 27, 32)
            }, new[] {
                new Rectangle(5, 234, 28, 26), new Rectangle(36, 234, 28, 26)
            }, new[] {
                new Rectangle(6, 136, 23, 33), new Rectangle(34, 145, 23, 24),
                new Rectangle(62, 145, 21, 31), new Rectangle(90, 141, 24, 27),
                new Rectangle(119, 130, 23, 39)
            }, new[] {
                new Rectangle(35, 43, 24, 30), new Rectangle(64, 43, 24, 30)
            });

            AddForm(forms, keyToForm, ref normalForm, "paint", "p", "kirby-paint.png", new[] {
                new Rectangle(5, 113, 26, 28), new Rectangle(34, 113, 26, 28),
                new Rectangle(62, 113, 26, 28), new Rectangle(92, 113, 26, 28)
            }, new[] {
                new Rectangle(181, 196, 26, 25), new Rectangle(210, 196, 26, 25)
            }, new[] {
                new Rectangle(6, 195, 24, 28), new Rectangle(35, 198, 23, 24),
                new Rectangle(63, 199, 22, 25), new Rectangle(90, 196, 24, 26),
                new Rectangle(119, 195, 23, 28)
            }, new[] {
                new Rectangle(35, 29, 24, 27), new Rectangle(64, 29, 24, 27)
            });

            AddForm(forms, keyToForm, ref normalForm, "parasol", "r", "kirby-parasol.png", new[] {
                new Rectangle(269, 92, 42, 49), new Rectangle(314, 92, 42, 49),
                new Rectangle(359, 92, 42, 49), new Rectangle(404, 92, 42, 49)
            }, new[] {
                new Rectangle(669, 116, 29, 25), new Rectangle(701, 116, 29, 25)
            }, new[] {
                new Rectangle(11, 167, 37, 44), new Rectangle(77, 186, 30, 41),
                new Rectangle(130, 187, 21, 45), new Rectangle(165, 168, 42, 41),
                new Rectangle(229, 162, 30, 47)
            }, new[] {
                new Rectangle(42, 91, 31, 46), new Rectangle(78, 91, 31, 46)
            });

            AddForm(forms, keyToForm, ref normalForm, "plasma", "a", "kirby-plasma.png", new[] {
                new Rectangle(6, 357, 24, 34), new Rectangle(35, 357, 24, 34),
                new Rectangle(63, 357, 24, 34), new Rectangle(92, 357, 24, 34)
            }, new[] {
                new Rectangle(5, 1078, 28, 29), new Rectangle(36, 1078, 28, 29)
            }, new[] {
                new Rectangle(6, 608, 24, 33), new Rectangle(35, 617, 26, 24),
                new Rectangle(66, 617, 26, 27), new Rectangle(97, 608, 25, 32),
                new Rectangle(127, 601, 23, 40)
            }, new[] {
                new Rectangle(35, 159, 24, 32), new Rectangle(64, 159, 24, 32)
            });

            AddForm(forms, keyToForm, ref normalForm, "stone", "s", "kirby-stone.png", new[] {
                new Rectangle(5, 123, 27, 32), new Rectangle(33, 123, 27, 32),
                new Rectangle(62, 123, 27, 32), new Rectangle(94, 123, 27, 32)
            }, new[] {
                new Rectangle(5, 406, 28, 25), new Rectangle(36, 406, 28, 25)
            }, new[] {
                new Rectangle(6, 214, 23, 33), new Rectangle(34, 223, 23, 24),
                new Rectangle(62, 223, 21, 31), new Rectangle(90, 219, 24, 27),
                new Rectangle(119, 208, 23, 39)
            }, new[] {
                new Rectangle(35, 29, 24, 30), new Rectangle(64, 29, 24, 30)
            });

            AddForm(forms, keyToForm, ref normalForm, "suplex", "u", "kirby-suplex.png", new[] {
                new Rectangle(6, 95, 26, 24), new Rectangle(36, 95, 26, 24),
                new Rectangle(66, 95, 26, 24), new Rectangle(96, 95, 26, 24)
            }, new[] {
                new Rectangle(5, 333, 29, 25), new Rectangle(37, 333, 29, 25)
            }, new[] {
                new Rectangle(6, 176, 25, 24), new Rectangle(36, 171, 23, 28),
                new Rectangle(64, 175, 21, 23), new Rectangle(90, 176, 26, 23),
                new Rectangle(121, 169, 23, 31)
            }, new[] {
                new Rectangle(35, 27, 24, 23), new Rectangle(64, 27, 24, 23)
            });

            AddForm(forms, keyToForm, ref normalForm, "sword", "w", "kirby-sword.png", new[] {
                new Rectangle(3, 101, 32, 30), new Rectangle(34, 101, 32, 30),
                new Rectangle(67, 101, 32, 30), new Rectangle(101, 101, 32, 30)
            }, new[] {
                new Rectangle(5, 321, 32, 28), new Rectangle(40, 321, 32, 28)
            }, new[] {
                new Rectangle(6, 174, 27, 30), new Rectangle(38, 180, 23, 23),
                new Rectangle(66, 180, 21, 27), new Rectangle(92, 178, 28, 25),
                new Rectangle(125, 168, 23, 36)
            }, new[] {
                new Rectangle(40, 34, 29, 28), new Rectangle(74, 34, 29, 28)
            });

            AddForm(forms, keyToForm, ref normalForm, "wheel", "v", "kirby-wheel.png", new[] {
                new Rectangle(5, 121, 26, 28), new Rectangle(34, 121, 26, 28),
                new Rectangle(62, 121, 26, 28), new Rectangle(92, 121, 26, 28)
            }, new[] {
                new Rectangle(152, 204, 26, 25), new Rectangle(181, 204, 26, 25)
            }, new[] {
                new Rectangle(6, 203, 24, 28), new Rectangle(35, 206, 23, 24),
                new Rectangle(63, 207, 22, 25), new Rectangle(90, 204, 24, 26),
                new Rectangle(119, 203, 23, 28)
            }, new[] {
                new Rectangle(35, 37, 24, 27), new Rectangle(64, 37, 24, 27)
            });

            AddForm(forms, keyToForm, ref normalForm, "wing", "g", "kirby-wing.png", new[] {
                new Rectangle(4, 127, 31, 32), new Rectangle(35, 127, 31, 32),
                new Rectangle(66, 127, 31, 32), new Rectangle(100, 127, 31, 32)
            }, new[] {
                new Rectangle(5, 448, 30, 29), new Rectangle(38, 448, 30, 29)
            }, new[] {
                new Rectangle(6, 214, 26, 31), new Rectangle(37, 220, 24, 23),
                new Rectangle(66, 222, 25, 30), new Rectangle(96, 217, 28, 27),
                new Rectangle(129, 212, 23, 32)
            }, new[] {
                new Rectangle(39, 37, 28, 32), new Rectangle(72, 37, 28, 32)
            });

            AddForm(forms, keyToForm, ref normalForm, "yoyo", "y", "kirby-yoyo.png", new[] {
                new Rectangle(5, 121, 26, 28), new Rectangle(34, 121, 26, 28),
                new Rectangle(62, 121, 26, 28), new Rectangle(92, 121, 26, 28)
            }, new[] {
                new Rectangle(152, 204, 26, 25), new Rectangle(181, 204, 26, 25)
            }, new[] {
                new Rectangle(6, 203, 24, 28), new Rectangle(35, 206, 23, 24),
                new Rectangle(63, 207, 22, 25), new Rectangle(90, 204, 24, 26),
                new Rectangle(119, 203, 23, 28)
            }, new[] {
                new Rectangle(35, 37, 24, 27), new Rectangle(64, 37, 24, 27)
            });
        }

        private static void AddForm(
            List<FormDef> forms,
            Dictionary<string, FormDef> keyToForm,
            ref FormDef normalForm,
            string name,
            string key,
            string file,
            Rectangle[] coords,
            Rectangle[] guardCoords,
            Rectangle[] jumpCoords,
            Rectangle[] standCoords)
        {
            FormDef def = new FormDef
            {
                Name = name,
                Key = key,
                FileName = file,
                Coords = coords,
                GuardCoords = guardCoords,
                JumpCoords = jumpCoords,
                StandCoords = standCoords
            };
            forms.Add(def);
            keyToForm[key] = def;
            if (name == "normal") normalForm = def;
        }
    }
}
