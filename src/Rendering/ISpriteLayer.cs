using System;
using System.Drawing;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Abstraction over a transparent desktop sprite overlay window (<see cref="KirbyOverlayWindow"/>)
    /// allowing actors and scenes to render or clear sprites without direct Win32 HWND dependencies.
    /// </summary>
    public interface ISpriteLayer : IDisposable {

        /// <summary>
        /// Indicates whether the overlay is currently parked off-screen with zero opacity.
        /// </summary>
        bool IsCleared { get; }

        /// <summary>
        /// Updates the layered window's screen position and nearest-neighbor blits <paramref name="tile"/>
        /// at <paramref name="scale"/> whenever the tile, scale, or coordinates change.
        /// </summary>
        void UpdateSpriteFrame(SpriteTile tile, int scale, int screenX, int screenY);

        /// <summary>
        /// Aligns <paramref name="tile"/> to the bottom of <paramref name="workArea"/> at horizontal center
        /// <paramref name="worldX"/> and vertical jump/float offset <paramref name="worldYOffset"/> (in unscaled 1x pixels),
        /// or clears the overlay if <paramref name="tile"/> is <c>null</c>.
        /// </summary>
        void RenderAtTaskbar(SpriteTile tile, int scale, Rectangle workArea, double worldX, double worldYOffset = 0.0);

        /// <summary>
        /// Parks the overlay off-screen with zero constant alpha without toggling <c>WS_VISIBLE</c>.
        /// </summary>
        void ClearOverlay(int scale);

    }

}