using System;
using System.Collections.Generic;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Encapsulates combat contact detection and knockback recoil state for both Kirby and enemies.
    /// </summary>
    public struct HitRecoilState {

        public const double ENEMY_CONTACT_DISTANCE = 16.0;
        public const int HIT_PUSHBACK_FRAMES = 8;
        public const double HIT_PUSHBACK_SPEED = 1.5;
        public const int ENEMY_HIT_FRAMES = 24;

        public bool IsHit;
        public double AnimTime;

        public void Reset() {
            IsHit = false;
            AnimTime = 0.0;
        }

        public SpriteTile AdvanceKirby(ref double kirbyX, double delta60, FormDef normalForm, int scale = 2) {
            return AdvanceHitState(ref IsHit, ref AnimTime, ref kirbyX, delta60, normalForm, scale);
        }

        public bool StepEnemy(ref double enemyX, EnemyDef enemyDef, double delta60, int scale = 2) {
            return StepHitEnemyPhysics(ref enemyX, ref AnimTime, enemyDef, delta60, scale);
        }

        /// <summary>
        /// Checks whether Kirby is in contact with an active enemy and should take damage:
        /// Kirby must be awake, on the ground, not guarding, not currently inhaling/swallowing or already hit,
        /// and within <see cref="ENEMY_CONTACT_DISTANCE"/> of the enemy.
        /// </summary>
        public static bool CanTakeEnemyHit(
            SleepState sleepState,
            bool isJumping,
            SwallowState swallowState,
            bool isHit,
            EnemyDef activeEnemy,
            double kirbyX,
            double enemyX,
            int scale,
            bool isGuarding = false,
            bool isEnemyHit = false) {
            if (sleepState != SleepState.Awake || isJumping || isGuarding || swallowState != SwallowState.None ||
                isHit || isEnemyHit || activeEnemy == null) {
                return false;
            }

            return Math.Abs(enemyX - kirbyX) <= ENEMY_CONTACT_DISTANCE * scale;
        }

        /// <summary>
        /// Checks whether Kirby has walked into <paramref name="activeEnemy"/> and, on contact, immediately
        /// strips any copy ability back to Normal form and starts the hit animation on both Kirby and the enemy.
        /// </summary>
        public static bool TryTriggerEnemyHit(
            IList<FormDef> forms,
            ref FormDef currentForm,
            SleepState sleepState,
            bool isJumping,
            SwallowState swallowState,
            ref bool isHit,
            ref double hitAnimTime,
            ref EnemyDef activeEnemy,
            ref bool isEnemyHit,
            ref double enemyHitAnimTime,
            double kirbyX,
            double enemyX,
            int scale,
            bool isGuarding = false) {
            if (!CanTakeEnemyHit(sleepState, isJumping, swallowState, isHit, activeEnemy, kirbyX, enemyX, scale,
                    isGuarding, isEnemyHit)) {
                return false;
            }

            currentForm = FormDefinitions.ResolveNormalForm(forms, currentForm);
            isHit = true;
            hitAnimTime = 0.0;
            isEnemyHit = true;
            enemyHitAnimTime = 0.0;
            return true;
        }

        /// <summary>
        /// Advances Normal Kirby's enemy-hit animation (<see cref="FormDef.Hit"/>) by <paramref name="delta60"/> ticks,
        /// applying backward recoil to <paramref name="kirbyX"/> during the first <see cref="HIT_PUSHBACK_FRAMES"/> frames,
        /// and returns the <see cref="SpriteTile"/> to render, or <c>null</c> once the hit sequence completes.
        /// </summary>
        public static SpriteTile AdvanceHitState(
            ref bool isHit,
            ref double hitAnimTime,
            ref double kirbyX,
            double delta60,
            FormDef normalForm,
            int scale = 2) {
            if (!isHit) {
                return null;
            }

            if (normalForm == null || normalForm.Hit == null || !normalForm.Hit.HasFrames()) {
                isHit = false;
                hitAnimTime = 0.0;
                return null;
            }

            int idx = (int)Math.Floor(hitAnimTime);

            if (idx < 0 || idx >= normalForm.Hit.Frames.Length) {
                isHit = false;
                hitAnimTime = 0.0;
                return null;
            }

            double nextAnimTime = hitAnimTime + normalForm.Hit.Speed * delta60;

            if (hitAnimTime < HIT_PUSHBACK_FRAMES && nextAnimTime > hitAnimTime) {
                double pushbackTicks = Math.Min(nextAnimTime, (double)HIT_PUSHBACK_FRAMES) - hitAnimTime;
                kirbyX -= HIT_PUSHBACK_SPEED * scale * pushbackTicks;
            }

            SpriteTile frame = normalForm.Hit.Frames[idx];
            hitAnimTime = nextAnimTime;
            return frame;
        }

        /// <summary>
        /// Advances a hit enemy's 24-frame knockback sequence: pushes the enemy backward (to the right)
        /// for the first <see cref="HIT_PUSHBACK_FRAMES"/> frames by the same distance as Kirby, holds the hit pose
        /// (<see cref="EnemyDef.Inhaled"/>) until <see cref="ENEMY_HIT_FRAMES"/> (24 frames), and then returns
        /// <c>false</c> so the enemy disappears.
        /// </summary>
        public static bool StepHitEnemyPhysics(
            ref double enemyX,
            ref double enemyHitAnimTime,
            EnemyDef enemyDef,
            double delta60,
            int scale = 2) {
            if (enemyDef == null) {
                return false;
            }

            int idx = (int)Math.Floor(enemyHitAnimTime);

            if (idx < 0 || idx >= ENEMY_HIT_FRAMES) {
                return false;
            }

            double nextAnimTime = enemyHitAnimTime + delta60;

            if (enemyHitAnimTime < HIT_PUSHBACK_FRAMES && nextAnimTime > enemyHitAnimTime) {
                double pushbackTicks = Math.Min(nextAnimTime, (double)HIT_PUSHBACK_FRAMES) - enemyHitAnimTime;
                enemyX += HIT_PUSHBACK_SPEED * scale * pushbackTicks;
            }

            enemyHitAnimTime = nextAnimTime;
            return true;
        }

    }

}