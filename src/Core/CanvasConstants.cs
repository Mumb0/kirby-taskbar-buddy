namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Shared logical sprite canvas dimensions and floor-alignment pivot coordinates.
    /// </summary>
    public static class CanvasConstants {

        /// <summary>
        /// Logical unscaled canvas size (80x80 pixels) used to align all sprites.
        /// </summary>
        public const int TILE_SIZE = 80;

        /// <summary>
        /// Horizontal center of Kirby's feet within the 80x80 logical tile.
        /// </summary>
        public const int PIVOT_X = 40;

        /// <summary>
        /// Vertical floor line (Y=54) where Kirby's feet rest within the 80x80 logical tile.
        /// Leaves 54px above for tall hats/weapons (Parasol, Hammer) and 25px below for Mirror's guard bubble.
        /// </summary>
        public const int PIVOT_Y = 54;

        /// <summary>
        /// With INHALE_VFX_OFFSET_X = 36, Kirby's center is at X = 40 - 36 = 4 and his open mouth edge is at X = 14, Y = 39.
        /// </summary>
        public const int INHALE_MOUTH_X = 14;

        /// <summary>
        /// Vertical mouth center in unscaled tile coordinates during Normal Kirby's inhale animation.
        /// </summary>
        public const int INHALE_MOUTH_Y = PIVOT_Y - 15;

    }

}
