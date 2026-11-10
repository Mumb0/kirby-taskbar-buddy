namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Compact, tightly-cropped representation of a single sprite frame aligned within the 80x80 logical canvas.
    /// Stores only the non-transparent bounding box (<see cref="MinX"/>, <see cref="MinY"/>, <see cref="Width"/>, <see cref="Height"/>)
    /// and 32-bit premultiplied-ARGB pixels ready for direct blitting into a GDI DIBSection.
    /// </summary>
    public sealed class SpriteTile {

        public int MinX;
        public int MinY;
        public int Width;
        public int Height;
        public int[] PremulPixels;

    }

}
