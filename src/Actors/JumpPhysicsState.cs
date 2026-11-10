using System;
using System.Drawing;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Encapsulates Kirby's jump trajectory state, horizontal walk speed, somersault frame mapping,
    /// cursor guard proximity check, and frame delta-time clamping.
    /// </summary>
    public struct JumpPhysicsState {

        public const double WALK_SPEED = 0.75;
        public const double JUMP_INIT_VEL = 4.2;
        public const double JUMP_GRAVITY = 0.18;

        public bool IsJumping;
        public double JumpY;
        public double JumpVelY;

        public void StartJump() {
            if (!IsJumping) {
                IsJumping = true;
                JumpY = 0.0;
                JumpVelY = JUMP_INIT_VEL;
            }
        }

        public void Step(ref double kirbyX, double delta60, int scale = 2) {
            StepJumpPhysics(ref IsJumping, ref kirbyX, ref JumpY, ref JumpVelY, delta60, scale);
        }

        public int GetFrameIndex() {
            return GetJumpFrameIndex(JumpVelY);
        }

        /// <summary>
        /// Clamps frame delta time so system suspend/resume or debugger pauses do not teleport Kirby across the screen.
        /// </summary>
        public static double ClampDeltaTime(double dtSeconds) {
            if (dtSeconds <= 0 || dtSeconds > 0.25) {
                return 0.01667;
            }

            return dtSeconds;
        }

        /// <summary>
        /// Checks whether the mouse pointer is touching Kirby's 24x24 sprite body on the taskbar
        /// (Kirby only guards when on the ground, never mid-jump).
        /// </summary>
        public static bool IsMouseNearKirby(Point mouse, double kirbyX, int workAreaBottom, int scale, bool isJumping) {
            if (isJumping) {
                return false;
            }

            double kirbyCenterY = workAreaBottom - 12 * scale;
            double distX = Math.Abs(mouse.X - kirbyX);
            double distY = Math.Abs(mouse.Y - kirbyCenterY);
            return distX <= 12 * scale && distY <= 12 * scale;
        }

        /// <summary>
        /// Advances Kirby's horizontal and vertical jump trajectory by <paramref name="delta60"/> ticks
        /// (scaling horizontal travel by <paramref name="scale"/>) and lands cleanly at <c>jumpY = 0</c> when touching the ground.
        /// </summary>
        public static void StepJumpPhysics(
            ref bool isJumping,
            ref double kirbyX,
            ref double jumpY,
            ref double jumpVelY,
            double delta60,
            int scale = 2) {
            kirbyX += WALK_SPEED * scale * delta60;
            jumpY += jumpVelY * delta60;
            jumpVelY -= JUMP_GRAVITY * delta60;

            if (jumpY <= 0.0) {
                jumpY = 0.0;
                jumpVelY = 0.0;
                isJumping = false;
            }
        }

        /// <summary>
        /// Maps vertical jump velocity (<paramref name="jumpVelY"/>) to the 5 phases of Kirby's jump and mid-air somersault.
        /// </summary>
        public static int GetJumpFrameIndex(double jumpVelY) {
            if (jumpVelY > 1.2) {
                return 0; // Rising launch
            }

            if (jumpVelY > 0.0) {
                return 1; // Apex somersault 1
            }

            if (jumpVelY > -1.2) {
                return 2; // Apex somersault 2
            }

            if (jumpVelY > -2.6) {
                return 3; // Early fall recovery
            }

            return 4; // Full fall descent
        }

    }

}