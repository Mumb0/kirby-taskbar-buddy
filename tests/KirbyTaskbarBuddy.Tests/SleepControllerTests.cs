using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class SleepControllerTests
    {
        [Fact]
        public void SleepStateMachine_ProgressesFromHaltingToFallingAsleepToSleepingLoop()
        {
            FormDef form = CreateDummyFormWithStandFrames();
            SpriteTile[] sleepFrames = CreateDummyTiles(8);

            SleepState state = SleepState.Halting;
            double animTime = 0.0;

            // Step through Halting until it transitions to FallingAsleep.
            for (int tick = 0; tick < 200 && state == SleepState.Halting; tick++)
            {
                SpriteTile frame = SleepController.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
                Assert.NotNull(frame);
            }
            Assert.Equal(SleepState.FallingAsleep, state);
            Assert.Equal(0.0, animTime);

            // Step through FallingAsleep until it transitions to Sleeping.
            for (int tick = 0; tick < 250 && state == SleepState.FallingAsleep; tick++)
            {
                SpriteTile frame = SleepController.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
                Assert.NotNull(frame);
            }
            Assert.Equal(SleepState.Sleeping, state);
            Assert.Equal(0.0, animTime);

            // Sleeping loops indefinitely across SleepLoopSeq frames [2, 3, 4, 5, 4, 3].
            for (int tick = 0; tick < 300; tick++)
            {
                SpriteTile frame = SleepController.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
                Assert.Equal(SleepState.Sleeping, state);
                Assert.NotNull(frame);
            }
        }

        [Fact]
        public void SleepStateMachine_WakingUpTransitionsThroughPostWakeStandingToAwake()
        {
            FormDef form = CreateDummyFormWithStandFrames();
            SpriteTile[] sleepFrames = CreateDummyTiles(8);

            SleepState state = SleepState.WakingUp;
            double animTime = 0.0;

            // Step through WakingUp until it enters PostWakeStanding.
            for (int tick = 0; tick < 100 && state == SleepState.WakingUp; tick++)
            {
                SpriteTile frame = SleepController.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
                Assert.NotNull(frame);
            }
            Assert.Equal(SleepState.PostWakeStanding, state);
            Assert.Equal(0.0, animTime);

            // Step through PostWakeStanding until Kirby returns to Awake (returning null so Awake renders immediately).
            SpriteTile lastFrame = null;
            for (int tick = 0; tick < 100 && state == SleepState.PostWakeStanding; tick++)
            {
                lastFrame = SleepController.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
            }
            Assert.Equal(SleepState.Awake, state);
            Assert.Equal(0.0, animTime);
            Assert.Null(lastFrame);
        }

        [Fact]
        public void WakeFromSleep_InterruptsHaltingImmediatelyToAwake_AndTriggersWakingUpFromSleep()
        {
            // 1. User activity during Halting (before sleep cap appears) returns directly to Awake.
            SleepState state = SleepState.Halting;
            double animTime = 2.5;
            SleepController.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.Awake, state);
            Assert.Equal(0.0, animTime);

            // 2. User activity during FallingAsleep triggers startled WakingUp.
            state = SleepState.FallingAsleep;
            animTime = 3.0;
            SleepController.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.WakingUp, state);
            Assert.Equal(0.0, animTime);

            // 3. User activity during Sleeping triggers startled WakingUp.
            state = SleepState.Sleeping;
            animTime = 12.0;
            SleepController.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.WakingUp, state);
            Assert.Equal(0.0, animTime);

            // 4. Activity while already WakingUp or PostWakeStanding does not restart the wake animation.
            state = SleepState.WakingUp;
            animTime = 1.8;
            SleepController.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.WakingUp, state);
            Assert.Equal(1.8, animTime);
        }

        private static FormDef CreateDummyFormWithStandFrames()
        {
            SpriteTile[] standTiles = CreateDummyTiles(2);
            return new FormDef
            {
                Name = "normal",
                Key = "0",
                Stand = new AnimClip
                {
                    RawFrames = standTiles,
                    Frames = standTiles
                }
            };
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
