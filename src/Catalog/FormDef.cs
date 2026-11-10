namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Placement of a single particle sprite from <c>misc-effects.png</c> (<see cref="FormDefinitions.SwallowEffectRects"/>)
    /// relative to Kirby's floor pivot <c>(PIVOT_X, PIVOT_Y)</c> during the ability-swallow animation.
    /// </summary>
    public struct SwallowParticleLayer {

        public int SpriteIndex;
        public int OffsetX;
        public int OffsetY;

        public SwallowParticleLayer(int spriteIndex, int offsetX, int offsetY) {
            SpriteIndex = spriteIndex;
            OffsetX = offsetX;
            OffsetY = offsetY;
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
        public AnimClip Hit;

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

}
