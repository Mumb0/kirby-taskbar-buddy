using System.Drawing;
using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class JumpAndMovementPhysicsTests
    {
        [Fact]
        public void ClampDeltaTime_PreventsTeleportOnZeroNegativeOrLargeSpikes()
        {
            Assert.Equal(0.01667, JumpPhysicsState.ClampDeltaTime(0.0));
            Assert.Equal(0.01667, JumpPhysicsState.ClampDeltaTime(-0.5));
            Assert.Equal(0.01667, JumpPhysicsState.ClampDeltaTime(5.0));   // System resume spike
            Assert.Equal(0.016, JumpPhysicsState.ClampDeltaTime(0.016));   // Normal 60Hz tick
            Assert.Equal(0.25, JumpPhysicsState.ClampDeltaTime(0.25));     // Upper boundary
        }

        [Fact]
        public void StepJumpPhysics_RisesReachesApexAndLandsCleanlyOnGround()
        {
            bool isJumping = true;
            double kirbyX = 100.0;
            double jumpY = 0.0;
            double jumpVelY = JumpPhysicsState.JUMP_INIT_VEL;
            double maxJumpY = 0.0;

            int ticks = 0;
            while (isJumping && ticks < 200)
            {
                JumpPhysicsState.StepJumpPhysics(ref isJumping, ref kirbyX, ref jumpY, ref jumpVelY, 1.0);
                if (jumpY > maxJumpY) maxJumpY = jumpY;
                ticks++;
            }

            Assert.False(isJumping);
            Assert.Equal(0.0, jumpY);
            Assert.Equal(0.0, jumpVelY);
            Assert.True(maxJumpY > 45.0, "Jump apex should exceed 45 unscaled pixels");
            Assert.Equal(100.0 + (JumpPhysicsState.WALK_SPEED * 2) * ticks, kirbyX, 5);

            // At 1x scale, Kirby travels half the horizontal screen distance per tick as at 2x scale.
            bool isJumping1x = true;
            double kirbyX1x = 100.0;
            double jumpY1x = 0.0;
            double jumpVelY1x = JumpPhysicsState.JUMP_INIT_VEL;
            for (int i = 0; i < ticks; i++)
            {
                JumpPhysicsState.StepJumpPhysics(ref isJumping1x, ref kirbyX1x, ref jumpY1x, ref jumpVelY1x, 1.0, scale: 1);
            }
            Assert.False(isJumping1x);
            Assert.Equal(100.0 + (JumpPhysicsState.WALK_SPEED * 1) * ticks, kirbyX1x, 5);
        }

        [Fact]
        public void GetJumpFrameIndex_MapsVelocityThresholdsToAllFiveSomersaultPhases()
        {
            Assert.Equal(0, JumpPhysicsState.GetJumpFrameIndex(4.2));   // Rising launch (> 1.2)
            Assert.Equal(0, JumpPhysicsState.GetJumpFrameIndex(1.21));
            Assert.Equal(1, JumpPhysicsState.GetJumpFrameIndex(1.2));   // Apex somersault 1 (0.0 < v <= 1.2)
            Assert.Equal(1, JumpPhysicsState.GetJumpFrameIndex(0.01));
            Assert.Equal(2, JumpPhysicsState.GetJumpFrameIndex(0.0));   // Apex somersault 2 (-1.2 < v <= 0.0)
            Assert.Equal(2, JumpPhysicsState.GetJumpFrameIndex(-1.19));
            Assert.Equal(3, JumpPhysicsState.GetJumpFrameIndex(-1.2));  // Early fall recovery (-2.6 < v <= -1.2)
            Assert.Equal(3, JumpPhysicsState.GetJumpFrameIndex(-2.59));
            Assert.Equal(4, JumpPhysicsState.GetJumpFrameIndex(-2.6));  // Full fall descent (<= -2.6)
            Assert.Equal(4, JumpPhysicsState.GetJumpFrameIndex(-4.2));
        }

        [Fact]
        public void IsMouseNearKirby_DetectsProximityScaledByFactor_AndIgnoresWhenJumping()
        {
            int workAreaBottom = 1040;
            double kirbyX = 500.0;
            int scale = 2;
            // At scale=2, kirbyCenterY = 1040 - 24 = 1016; guard box is +/-24px horizontally and +/-24px vertically.
            Point nearPoint = new Point(524, 992);
            Point outsideX = new Point(525, 1016);
            Point outsideY = new Point(500, 991);

            Assert.True(JumpPhysicsState.IsMouseNearKirby(nearPoint, kirbyX, workAreaBottom, scale, isJumping: false));
            Assert.False(JumpPhysicsState.IsMouseNearKirby(outsideX, kirbyX, workAreaBottom, scale, isJumping: false));
            Assert.False(JumpPhysicsState.IsMouseNearKirby(outsideY, kirbyX, workAreaBottom, scale, isJumping: false));

            // At scale=1, kirbyCenterY = 1040 - 12 = 1028; guard box is +/-12px horizontally and +/-12px vertically.
            Assert.True(JumpPhysicsState.IsMouseNearKirby(new Point(512, 1016), kirbyX, workAreaBottom, scale: 1, isJumping: false));
            Assert.False(JumpPhysicsState.IsMouseNearKirby(new Point(513, 1028), kirbyX, workAreaBottom, scale: 1, isJumping: false));
            Assert.False(JumpPhysicsState.IsMouseNearKirby(new Point(500, 1015), kirbyX, workAreaBottom, scale: 1, isJumping: false));

            // Never enters guard stance while jumping, even if cursor is directly over Kirby.
            Assert.False(JumpPhysicsState.IsMouseNearKirby(new Point(500, 1016), kirbyX, workAreaBottom, scale, isJumping: true));
        }

        [Fact]
        public void GetFrame_LoopsWhenLoopEnabled_AndClampsToFinalFrameWhenLoopDisabled()
        {
            SpriteTile[] tiles = CreateDummyTiles(3);
            AnimClip loopClip = new AnimClip { Loop = true, RawFrames = tiles, Frames = tiles };
            AnimClip onceClip = new AnimClip { Loop = false, RawFrames = tiles, Frames = tiles };

            Assert.Same(tiles[0], loopClip.GetFrame(0.2));
            Assert.Same(tiles[1], loopClip.GetFrame(1.5));
            Assert.Same(tiles[2], loopClip.GetFrame(2.9));
            Assert.Same(tiles[0], loopClip.GetFrame(3.1));

            Assert.Same(tiles[0], onceClip.GetFrame(0.2));
            Assert.Same(tiles[1], onceClip.GetFrame(1.5));
            Assert.Same(tiles[2], onceClip.GetFrame(2.9));
            Assert.Same(tiles[2], onceClip.GetFrame(9.5));
            Assert.Null(AnimClip.GetFrame(null, 0.0));

            // GetVfxFrame loops when Loop is true, and returns null (despawning the VFX) once a non-looping clip completes.
            Assert.Same(tiles[0], loopClip.GetVfxFrame(3.1));
            Assert.Same(tiles[2], onceClip.GetVfxFrame(2.9));
            Assert.Null(onceClip.GetVfxFrame(3.0));
            Assert.Null(AnimClip.GetVfxFrame(null, 0.0));
        }

        private static SpriteTile[] CreateDummyTiles(int count)
        {
            SpriteTile[] tiles = new SpriteTile[count];
            for (int i = 0; i < count; i++)
            {
                tiles[i] = new SpriteTile
                {
                    MinX = 30,
                    MinY = 30,
                    Width = 20,
                    Height = 25,
                    PremulPixels = new int[20 * 25]
                };
            }
            return tiles;
        }
    }
}
