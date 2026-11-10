using System;
using System.Drawing;

namespace KirbyTaskbarBuddy {

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

        /// <summary>
        /// Resolves the <see cref="SpriteTile"/> for this clip at <paramref name="animTime"/>,
        /// wrapping modulo frame count when <see cref="Loop"/> is <c>true</c> or clamping to the last frame when <c>false</c>.
        /// </summary>
        public SpriteTile GetFrame(double animTime) {
            if (!HasFrames()) {
                return null;
            }

            int frameCount = Frames.Length;
            int idx = (int)Math.Floor(animTime);

            if (Loop) {
                idx %= frameCount;

                if (idx < 0) {
                    idx += frameCount;
                }
            }
            else {
                if (idx < 0) {
                    idx = 0;
                }
                else if (idx >= frameCount) {
                    idx = frameCount - 1;
                }
            }

            return Frames[idx];
        }

        /// <summary>
        /// Resolves the <see cref="SpriteTile"/> for a visual effect clip at <paramref name="animTime"/>,
        /// wrapping when <see cref="Loop"/> is <c>true</c> or returning <c>null</c> (despawning the VFX)
        /// once <paramref name="animTime"/> completes all frames of a non-looping clip.
        /// </summary>
        public SpriteTile GetVfxFrame(double animTime) {
            if (!HasFrames()) {
                return null;
            }

            int frameCount = Frames.Length;
            int idx = (int)Math.Floor(animTime);

            if (Loop) {
                idx %= frameCount;

                if (idx < 0) {
                    idx += frameCount;
                }

                return Frames[idx];
            }

            if (idx < 0 || idx >= frameCount) {
                return null;
            }

            return Frames[idx];
        }

        /// <summary>
        /// Null-safe helper that resolves the <see cref="SpriteTile"/> for <paramref name="clip"/> at <paramref name="animTime"/>.
        /// </summary>
        public static SpriteTile GetFrame(AnimClip clip, double animTime) {
            return clip != null ? clip.GetFrame(animTime) : null;
        }

        /// <summary>
        /// Null-safe helper that resolves the VFX <see cref="SpriteTile"/> for <paramref name="clip"/> at <paramref name="animTime"/>.
        /// </summary>
        public static SpriteTile GetVfxFrame(AnimClip clip, double animTime) {
            return clip != null ? clip.GetVfxFrame(animTime) : null;
        }

    }

}
