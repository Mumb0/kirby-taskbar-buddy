using System.Drawing;
using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class SleepStateAndPhysicsTests
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
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
                Assert.NotNull(frame);
            }
            Assert.Equal(SleepState.FallingAsleep, state);
            Assert.Equal(0.0, animTime);

            // Step through FallingAsleep until it transitions to Sleeping.
            for (int tick = 0; tick < 250 && state == SleepState.FallingAsleep; tick++)
            {
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
                Assert.NotNull(frame);
            }
            Assert.Equal(SleepState.Sleeping, state);
            Assert.Equal(0.0, animTime);

            // Sleeping loops indefinitely across SleepLoopSeq frames [2, 3, 4, 5, 4, 3].
            for (int tick = 0; tick < 300; tick++)
            {
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
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
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
                Assert.NotNull(frame);
            }
            Assert.Equal(SleepState.PostWakeStanding, state);
            Assert.Equal(0.0, animTime);

            // Step through PostWakeStanding until Kirby returns to Awake (returning null so Awake renders immediately).
            SpriteTile lastFrame = null;
            for (int tick = 0; tick < 100 && state == SleepState.PostWakeStanding; tick++)
            {
                lastFrame = KirbyTaskbarBuddyContext.AdvanceSleepState(ref state, ref animTime, 1.0, form, sleepFrames);
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
            KirbyTaskbarBuddyContext.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.Awake, state);
            Assert.Equal(0.0, animTime);

            // 2. User activity during FallingAsleep triggers startled WakingUp.
            state = SleepState.FallingAsleep;
            animTime = 3.0;
            KirbyTaskbarBuddyContext.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.WakingUp, state);
            Assert.Equal(0.0, animTime);

            // 3. User activity during Sleeping triggers startled WakingUp.
            state = SleepState.Sleeping;
            animTime = 12.0;
            KirbyTaskbarBuddyContext.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.WakingUp, state);
            Assert.Equal(0.0, animTime);

            // 4. Activity while already WakingUp or PostWakeStanding does not restart the wake animation.
            state = SleepState.WakingUp;
            animTime = 1.8;
            KirbyTaskbarBuddyContext.WakeFromSleep(ref state, ref animTime);
            Assert.Equal(SleepState.WakingUp, state);
            Assert.Equal(1.8, animTime);
        }

        [Fact]
        public void ClampDeltaTime_PreventsTeleportOnZeroNegativeOrLargeSpikes()
        {
            Assert.Equal(0.01667, KirbyTaskbarBuddyContext.ClampDeltaTime(0.0));
            Assert.Equal(0.01667, KirbyTaskbarBuddyContext.ClampDeltaTime(-0.5));
            Assert.Equal(0.01667, KirbyTaskbarBuddyContext.ClampDeltaTime(5.0));   // System resume spike
            Assert.Equal(0.016, KirbyTaskbarBuddyContext.ClampDeltaTime(0.016));   // Normal 60Hz tick
            Assert.Equal(0.25, KirbyTaskbarBuddyContext.ClampDeltaTime(0.25));     // Upper boundary
        }

        [Fact]
        public void StepJumpPhysics_RisesReachesApexAndLandsCleanlyOnGround()
        {
            bool isJumping = true;
            double kirbyX = 100.0;
            double jumpY = 0.0;
            double jumpVelY = KirbyTaskbarBuddyContext.JUMP_INIT_VEL;
            double maxJumpY = 0.0;

            int ticks = 0;
            while (isJumping && ticks < 200)
            {
                KirbyTaskbarBuddyContext.StepJumpPhysics(ref isJumping, ref kirbyX, ref jumpY, ref jumpVelY, 1.0);
                if (jumpY > maxJumpY) maxJumpY = jumpY;
                ticks++;
            }

            Assert.False(isJumping);
            Assert.Equal(0.0, jumpY);
            Assert.Equal(0.0, jumpVelY);
            Assert.True(maxJumpY > 45.0, "Jump apex should exceed 45 unscaled pixels");
            Assert.Equal(100.0 + (KirbyTaskbarBuddyContext.WALK_SPEED * 2) * ticks, kirbyX, 5);

            // At 1x scale, Kirby travels half the horizontal screen distance per tick as at 2x scale.
            bool isJumping1x = true;
            double kirbyX1x = 100.0;
            double jumpY1x = 0.0;
            double jumpVelY1x = KirbyTaskbarBuddyContext.JUMP_INIT_VEL;
            for (int i = 0; i < ticks; i++)
            {
                KirbyTaskbarBuddyContext.StepJumpPhysics(ref isJumping1x, ref kirbyX1x, ref jumpY1x, ref jumpVelY1x, 1.0, scale: 1);
            }
            Assert.False(isJumping1x);
            Assert.Equal(100.0 + (KirbyTaskbarBuddyContext.WALK_SPEED * 1) * ticks, kirbyX1x, 5);
        }

        [Fact]
        public void GetJumpFrameIndex_MapsVelocityThresholdsToAllFiveSomersaultPhases()
        {
            Assert.Equal(0, KirbyTaskbarBuddyContext.GetJumpFrameIndex(4.2));   // Rising launch (> 1.2)
            Assert.Equal(0, KirbyTaskbarBuddyContext.GetJumpFrameIndex(1.21));
            Assert.Equal(1, KirbyTaskbarBuddyContext.GetJumpFrameIndex(1.2));   // Apex somersault 1 (0.0 < v <= 1.2)
            Assert.Equal(1, KirbyTaskbarBuddyContext.GetJumpFrameIndex(0.01));
            Assert.Equal(2, KirbyTaskbarBuddyContext.GetJumpFrameIndex(0.0));   // Apex somersault 2 (-1.2 < v <= 0.0)
            Assert.Equal(2, KirbyTaskbarBuddyContext.GetJumpFrameIndex(-1.19));
            Assert.Equal(3, KirbyTaskbarBuddyContext.GetJumpFrameIndex(-1.2));  // Early fall recovery (-2.6 < v <= -1.2)
            Assert.Equal(3, KirbyTaskbarBuddyContext.GetJumpFrameIndex(-2.59));
            Assert.Equal(4, KirbyTaskbarBuddyContext.GetJumpFrameIndex(-2.6));  // Full fall descent (<= -2.6)
            Assert.Equal(4, KirbyTaskbarBuddyContext.GetJumpFrameIndex(-4.2));
        }

        [Fact]
        public void IsMouseNearKirby_DetectsProximityScaledByFactor_AndIgnoresWhenJumping()
        {
            int workAreaBottom = 1040;
            double kirbyX = 500.0;
            int scale = 2;
            // At scale=2, kirbyCenterY = 1040 - 32 = 1008; guard box is +/-48px horizontally and +/-56px vertically.
            Point nearPoint = new Point(548, 952);
            Point outsideX = new Point(549, 1008);
            Point outsideY = new Point(500, 951);

            Assert.True(KirbyTaskbarBuddyContext.IsMouseNearKirby(nearPoint, kirbyX, workAreaBottom, scale, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(outsideX, kirbyX, workAreaBottom, scale, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(outsideY, kirbyX, workAreaBottom, scale, isJumping: false));

            // At scale=1, kirbyCenterY = 1040 - 16 = 1024; guard box is +/-24px horizontally and +/-28px vertically.
            Assert.True(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(524, 996), kirbyX, workAreaBottom, scale: 1, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(525, 1024), kirbyX, workAreaBottom, scale: 1, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(500, 995), kirbyX, workAreaBottom, scale: 1, isJumping: false));

            // Never enters guard stance while jumping, even if cursor is directly over Kirby.
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(500, 1008), kirbyX, workAreaBottom, scale, isJumping: true));
        }

        [Fact]
        public void NormalizeScale_And_NormalizeSleepTimeout_AcceptValidValuesAndFallbackOnInvalid()
        {
            Assert.Equal(1, KirbyTaskbarBuddyContext.NormalizeScale(1));
            Assert.Equal(2, KirbyTaskbarBuddyContext.NormalizeScale(2));
            Assert.Equal(3, KirbyTaskbarBuddyContext.NormalizeScale(3));
            Assert.Equal(4, KirbyTaskbarBuddyContext.NormalizeScale(4));
            Assert.Equal(2, KirbyTaskbarBuddyContext.NormalizeScale(0));
            Assert.Equal(2, KirbyTaskbarBuddyContext.NormalizeScale(5));
            Assert.Equal(2, KirbyTaskbarBuddyContext.NormalizeScale(-1));

            foreach (double valid in KirbyTaskbarBuddyContext.SleepTimeoutValues)
            {
                Assert.Equal(valid, KirbyTaskbarBuddyContext.NormalizeSleepTimeout(valid));
            }
            Assert.Equal(300.0, KirbyTaskbarBuddyContext.NormalizeSleepTimeout(0.0));
            Assert.Equal(300.0, KirbyTaskbarBuddyContext.NormalizeSleepTimeout(45.0));
            Assert.Equal(300.0, KirbyTaskbarBuddyContext.NormalizeSleepTimeout(-99.0));
        }

        [Fact]
        public void NextEnemySpawnInterval_ReturnsDelayWithinConfiguredBounds_AndHandlesEdgeCases()
        {
            System.Random rng = new System.Random(12345);
            for (int i = 0; i < 100; i++)
            {
                double delay = KirbyTaskbarBuddyContext.NextEnemySpawnInterval(
                    rng,
                    KirbyTaskbarBuddyContext.ENEMY_SPAWN_MIN_SECONDS,
                    KirbyTaskbarBuddyContext.ENEMY_SPAWN_MAX_SECONDS);
                Assert.InRange(delay, KirbyTaskbarBuddyContext.ENEMY_SPAWN_MIN_SECONDS, KirbyTaskbarBuddyContext.ENEMY_SPAWN_MAX_SECONDS);
            }

            Assert.Equal(5.0, KirbyTaskbarBuddyContext.NextEnemySpawnInterval(null, 5.0, 600.0));
            Assert.Equal(10.0, KirbyTaskbarBuddyContext.NextEnemySpawnInterval(rng, 10.0, 5.0));
        }

        [Fact]
        public void StepEnemyPhysics_WalksRightToLeftScaledByFactor_AndSignalsDespawnAtLeftEdge()
        {
            SpriteTile[] walkTiles = CreateDummyTiles(6);
            EnemyDef enemy = new EnemyDef
            {
                Name = "waddle-dee",
                WalkSpeed = 0.55,
                Walk = new AnimClip
                {
                    Speed = 0.1,
                    RawFrames = walkTiles,
                    Frames = walkTiles
                }
            };

            double enemyX = 100.0;
            double animTime = 0.0;
            int scale = 2;
            int despawnLeftX = -80;

            // Single 60Hz tick at 2x scale moves enemy left by 0.55 * 2 = 1.1px and advances animTime by 0.1.
            bool active = KirbyTaskbarBuddyContext.StepEnemyPhysics(ref enemyX, ref animTime, enemy, 1.0, scale, despawnLeftX);
            Assert.True(active);
            Assert.Equal(98.9, enemyX, 5);
            Assert.Equal(0.1, animTime, 5);

            // Step until enemy walks past despawnLeftX (-80).
            int ticks = 0;
            while (active && ticks < 500)
            {
                active = KirbyTaskbarBuddyContext.StepEnemyPhysics(ref enemyX, ref animTime, enemy, 1.0, scale, despawnLeftX);
                ticks++;
            }

            Assert.False(active);
            Assert.True(enemyX < despawnLeftX);
            Assert.True(animTime > 15.0);

            // Null enemy or clip without frames immediately returns false.
            Assert.False(KirbyTaskbarBuddyContext.StepEnemyPhysics(ref enemyX, ref animTime, null, 1.0, scale, despawnLeftX));
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
