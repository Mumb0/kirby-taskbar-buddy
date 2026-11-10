using System;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Manages Kirby's multi-stage idle sleep and wake-up animation state machine
    /// (<see cref="SleepState.Halting"/> -> <see cref="SleepState.FallingAsleep"/> -> <see cref="SleepState.Sleeping"/>
    /// -> <see cref="SleepState.WakingUp"/> -> <see cref="SleepState.PostWakeStanding"/> -> <see cref="SleepState.Awake"/>).
    /// </summary>
    public sealed class SleepController {

        private readonly SpriteTile[] _sleepFrames;
        public SleepState State;
        public double AnimTime;

        public bool IsAwake {
            get { return State == SleepState.Awake; }
        }

        public bool HasSleepFrames {
            get { return _sleepFrames != null && _sleepFrames.Length >= 8; }
        }

        public SleepController(SpriteTile[] sleepFrames) {
            _sleepFrames = sleepFrames;
            State = SleepState.Awake;
            AnimTime = 0.0;
        }

        public void BeginSleep(FormDef currentForm) {
            if (!IsAwake || !HasSleepFrames) {
                return;
            }

            bool hasStandFrames = currentForm != null && currentForm.Stand != null &&
                                  currentForm.Stand.HasFrames(2);
            State = hasStandFrames ? SleepState.Halting : SleepState.FallingAsleep;
            AnimTime = 0.0;
        }

        public void Wake() {
            WakeFromSleep(ref State, ref AnimTime);
        }

        public SpriteTile Advance(double delta60, FormDef currentForm) {
            return AdvanceSleepState(ref State, ref AnimTime, delta60, currentForm, _sleepFrames);
        }

        /// <summary>
        /// Transitions Kirby out of sleep when user activity occurs or when the sleep timer is disabled.
        /// Interrupting <see cref="SleepState.Halting"/> returns directly to <see cref="SleepState.Awake"/>,
        /// whereas interrupting <see cref="SleepState.FallingAsleep"/> or <see cref="SleepState.Sleeping"/>
        /// triggers the startled <see cref="SleepState.WakingUp"/> animation.
        /// </summary>
        public static void WakeFromSleep(ref SleepState sleepState, ref double sleepAnimTime) {
            if (sleepState == SleepState.Halting) {
                sleepState = SleepState.Awake;
                sleepAnimTime = 0.0;
            }
            else if (sleepState == SleepState.FallingAsleep || sleepState == SleepState.Sleeping) {
                sleepState = SleepState.WakingUp;
                sleepAnimTime = 0.0;
            }
        }

        /// <summary>
        /// Advances the multi-stage sleep state machine by <paramref name="delta60"/> (60Hz-normalized ticks)
        /// and returns the <see cref="SpriteTile"/> to render, or <c>null</c> once Kirby returns to <see cref="SleepState.Awake"/>.
        /// </summary>
        public static SpriteTile AdvanceSleepState(
            ref SleepState sleepState,
            ref double sleepAnimTime,
            double delta60,
            FormDef currentForm,
            SpriteTile[] sleepFrames) {
            if (sleepState == SleepState.Awake || sleepFrames == null || sleepFrames.Length < 8) {
                return null;
            }

            if (sleepState == SleepState.Halting) {
                sleepAnimTime += 0.05 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);

                if (seqIdx >= FormDefinitions.HaltStandSeq.Length || currentForm == null || currentForm.Stand == null ||
                    !currentForm.Stand.HasFrames(2)) {
                    sleepState = SleepState.FallingAsleep;
                    sleepAnimTime = 0.0;
                    return sleepFrames[FormDefinitions.FallAsleepSeq[0]];
                }

                return currentForm.Stand.Frames[FormDefinitions.HaltStandSeq[seqIdx]];
            }

            if (sleepState == SleepState.FallingAsleep) {
                sleepAnimTime += 0.05 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);

                if (seqIdx >= FormDefinitions.FallAsleepSeq.Length) {
                    sleepState = SleepState.Sleeping;
                    sleepAnimTime = 0.0;
                    return sleepFrames[FormDefinitions.SleepLoopSeq[0]];
                }

                return sleepFrames[FormDefinitions.FallAsleepSeq[seqIdx]];
            }

            if (sleepState == SleepState.Sleeping) {
                sleepAnimTime += 0.045 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime) % FormDefinitions.SleepLoopSeq.Length;

                if (seqIdx < 0) {
                    seqIdx += FormDefinitions.SleepLoopSeq.Length;
                }

                return sleepFrames[FormDefinitions.SleepLoopSeq[seqIdx]];
            }

            if (sleepState == SleepState.WakingUp) {
                sleepAnimTime += 0.09 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);

                if (seqIdx >= FormDefinitions.WakeUpSeq.Length) {
                    if (currentForm != null && currentForm.Stand != null && currentForm.Stand.HasFrames(2)) {
                        sleepState = SleepState.PostWakeStanding;
                        sleepAnimTime = 0.0;
                        return currentForm.Stand.Frames[FormDefinitions.PostWakeStandSeq[0]];
                    }

                    sleepState = SleepState.Awake;
                    sleepAnimTime = 0.0;
                    return null;
                }

                return sleepFrames[FormDefinitions.WakeUpSeq[seqIdx]];
            }

            if (sleepState == SleepState.PostWakeStanding) {
                sleepAnimTime += 0.06 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);

                if (seqIdx >= FormDefinitions.PostWakeStandSeq.Length || currentForm == null || currentForm.Stand == null ||
                    !currentForm.Stand.HasFrames(2)) {
                    sleepState = SleepState.Awake;
                    sleepAnimTime = 0.0;
                    return null;
                }

                return currentForm.Stand.Frames[FormDefinitions.PostWakeStandSeq[seqIdx]];
            }

            return null;
        }

    }

}