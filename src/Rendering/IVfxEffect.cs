namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Represents a visual effect (such as Kirby's inhale wind particles or a sprite-based attack/star effect)
    /// rendered on the foreground VFX overlay layer.
    /// </summary>
    public interface IVfxEffect {

        /// <summary>
        /// Horizontal offset in unscaled 1x pixels relative to the anchor X coordinate.
        /// </summary>
        double OffsetX { get; }

        /// <summary>
        /// Vertical offset in unscaled 1x pixels above the taskbar floor line.
        /// </summary>
        double OffsetY { get; }

        /// <summary>
        /// Advances the effect by <paramref name="delta60"/> (60Hz-normalized ticks).
        /// Returns <c>true</c> while the effect remains active, or <c>false</c> when it has finished.
        /// </summary>
        bool Step(double delta60);

        /// <summary>
        /// Returns the current <see cref="SpriteTile"/> frame to display, or <c>null</c> if inactive.
        /// </summary>
        SpriteTile Render();

    }

    /// <summary>
    /// Visual effect backed by a sprite <see cref="AnimClip"/> (looping or one-shot).
    /// </summary>
    public sealed class SpriteClipVfxEffect : IVfxEffect {

        private readonly AnimClip _clip;
        private readonly double _offsetX;
        private readonly double _offsetY;
        private double _animTime;

        public double OffsetX {
            get { return _offsetX; }
        }

        public double OffsetY {
            get { return _offsetY; }
        }

        public SpriteClipVfxEffect(AnimClip clip, double offsetX = 0.0, double offsetY = 0.0) {
            _clip = clip;
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

        public bool Step(double delta60) {
            if (_clip == null || !_clip.HasFrames()) {
                return false;
            }

            _animTime += _clip.Speed * delta60;
            return _clip.GetVfxFrame(_animTime) != null;
        }

        public SpriteTile Render() {
            return AnimClip.GetVfxFrame(_clip, _animTime);
        }

    }

}
