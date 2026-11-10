using System;
using System.Collections.Generic;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Manages Normal Kirby's inhale range check, mouthful hold, swallow animation,
    /// and copy-ability transformation upon swallowing an enemy.
    /// </summary>
    public sealed class SwallowController {

        public const double INHALE_RANGE = 80.0;
        public const double INHALE_PULL_SPEED = 2.0;
        public const double INHALE_TOUCH_DISTANCE = 8.0;

        public SwallowState State;
        public double AnimTime;
        public EnemyDef SwallowedEnemy;

        public SwallowController() {
            State = SwallowState.None;
            AnimTime = 0.0;
            SwallowedEnemy = null;
        }

        public void Reset() {
            State = SwallowState.None;
            AnimTime = 0.0;
            SwallowedEnemy = null;
        }

        public void BeginInhale() {
            State = SwallowState.Inhaling;
            AnimTime = 0.0;
        }

        public void BeginMouthful(EnemyDef swallowedEnemy) {
            SwallowedEnemy = swallowedEnemy;
            State = SwallowState.Mouthful;
            AnimTime = 0.0;
        }

        public SpriteTile Advance(double delta60, ref FormDef currentForm, IList<FormDef> forms) {
            return AdvanceSwallowState(ref State, ref AnimTime, delta60, ref currentForm, forms, ref SwallowedEnemy);
        }

        /// <summary>
        /// Checks whether Kirby can begin inhaling an approaching enemy: Kirby must be awake, on the ground,
        /// not guarding, in Normal form with an <see cref="FormDef.Inhale"/> clip, and the enemy must be in front
        /// of Kirby within <see cref="INHALE_RANGE"/>.
        /// </summary>
        public static bool CanStartInhale(
            FormDef currentForm,
            SleepState sleepState,
            bool isJumping,
            SwallowState swallowState,
            EnemyDef activeEnemy,
            double kirbyX,
            double enemyX,
            int scale,
            bool isGuarding = false,
            bool isEnemyHit = false) {
            if (sleepState != SleepState.Awake || isJumping || isGuarding || isEnemyHit ||
                swallowState != SwallowState.None) {
                return false;
            }

            if (currentForm == null || currentForm.Name != "normal" || currentForm.Inhale == null ||
                !currentForm.Inhale.HasFrames()) {
                return false;
            }

            if (activeEnemy == null) {
                return false;
            }

            double dist = enemyX - kirbyX;
            return dist >= 0.0 && dist <= INHALE_RANGE * scale;
        }

        /// <summary>
        /// Advances Normal Kirby's post-inhale <see cref="SwallowState.Mouthful"/> and <see cref="SwallowState.Swallowing"/>
        /// phases by <paramref name="delta60"/> ticks and returns the <see cref="SpriteTile"/> to render,
        /// or <c>null</c> once the swallow sequence finishes and returns to <see cref="SwallowState.None"/>.
        /// </summary>
        public static SpriteTile AdvanceSwallowState(
            ref SwallowState swallowState,
            ref double swallowAnimTime,
            double delta60,
            FormDef currentForm) {
            EnemyDef dummyEnemy = null;
            return AdvanceSwallowState(ref swallowState, ref swallowAnimTime, delta60, ref currentForm, null,
                ref dummyEnemy);
        }

        /// <summary>
        /// Advances Normal Kirby's post-inhale <see cref="SwallowState.Mouthful"/> and <see cref="SwallowState.Swallowing"/>
        /// phases by <paramref name="delta60"/> ticks, transforms <paramref name="currentForm"/> into the copy ability
        /// granted by <paramref name="swallowedEnemy"/> once swallowing completes, and returns the <see cref="SpriteTile"/>
        /// to render (or <c>null</c> once Kirby returns to <see cref="SwallowState.None"/>).
        /// </summary>
        public static SpriteTile AdvanceSwallowState(
            ref SwallowState swallowState,
            ref double swallowAnimTime,
            double delta60,
            ref FormDef currentForm,
            IList<FormDef> forms,
            ref EnemyDef swallowedEnemy) {
            if (currentForm == null) {
                swallowedEnemy = null;
                swallowState = SwallowState.None;
                swallowAnimTime = 0.0;
                return null;
            }

            bool hasAbility = swallowedEnemy != null && !string.IsNullOrEmpty(swallowedEnemy.CopyAbility);
            AnimClip swallowClip = hasAbility && currentForm.SwallowAbility != null &&
                                   currentForm.SwallowAbility.HasFrames()
                ? currentForm.SwallowAbility
                : currentForm.Swallow;

            if (swallowState == SwallowState.Mouthful) {
                if (currentForm.Mouthful == null || !currentForm.Mouthful.HasFrames()) {
                    swallowState = SwallowState.Swallowing;
                    swallowAnimTime = 0.0;
                }
                else {
                    swallowAnimTime += currentForm.Mouthful.Speed * delta60;
                    int idx = (int)Math.Floor(swallowAnimTime);

                    if (idx < currentForm.Mouthful.Frames.Length) {
                        return currentForm.Mouthful.Frames[Math.Max(0, idx)];
                    }

                    swallowState = SwallowState.Swallowing;
                    swallowAnimTime = 0.0;

                    if (swallowClip != null && swallowClip.HasFrames()) {
                        return swallowClip.Frames[0];
                    }
                }
            }

            if (swallowState == SwallowState.Swallowing) {
                if (swallowClip == null || !swallowClip.HasFrames()) {
                    currentForm = FormDefinitions.ResolveSwallowedForm(forms, currentForm, swallowedEnemy);
                    swallowedEnemy = null;
                    swallowState = SwallowState.None;
                    swallowAnimTime = 0.0;
                    return null;
                }

                swallowAnimTime += swallowClip.Speed * delta60;
                int idx = (int)Math.Floor(swallowAnimTime);

                if (idx < swallowClip.Frames.Length) {
                    return swallowClip.Frames[Math.Max(0, idx)];
                }

                currentForm = FormDefinitions.ResolveSwallowedForm(forms, currentForm, swallowedEnemy);
                swallowedEnemy = null;
                swallowState = SwallowState.None;
                swallowAnimTime = 0.0;
                return null;
            }

            return null;
        }

    }

}