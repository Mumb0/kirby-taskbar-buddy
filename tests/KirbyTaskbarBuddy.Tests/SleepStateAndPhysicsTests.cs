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
            // At scale=2, kirbyCenterY = 1040 - 24 = 1016; guard box is +/-24px horizontally and +/-24px vertically.
            Point nearPoint = new Point(524, 992);
            Point outsideX = new Point(525, 1016);
            Point outsideY = new Point(500, 991);

            Assert.True(KirbyTaskbarBuddyContext.IsMouseNearKirby(nearPoint, kirbyX, workAreaBottom, scale, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(outsideX, kirbyX, workAreaBottom, scale, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(outsideY, kirbyX, workAreaBottom, scale, isJumping: false));

            // At scale=1, kirbyCenterY = 1040 - 12 = 1028; guard box is +/-12px horizontally and +/-12px vertically.
            Assert.True(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(512, 1016), kirbyX, workAreaBottom, scale: 1, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(513, 1028), kirbyX, workAreaBottom, scale: 1, isJumping: false));
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(500, 1015), kirbyX, workAreaBottom, scale: 1, isJumping: false));

            // Never enters guard stance while jumping, even if cursor is directly over Kirby.
            Assert.False(KirbyTaskbarBuddyContext.IsMouseNearKirby(new Point(500, 1016), kirbyX, workAreaBottom, scale, isJumping: true));
        }

        [Fact]
        public void NormalizeScale_And_NormalizeSleepTimeout_AcceptValidValuesAndFallbackOnInvalid()
        {
            Assert.Equal(1, AppSettings.NormalizeScale(1));
            Assert.Equal(2, AppSettings.NormalizeScale(2));
            Assert.Equal(3, AppSettings.NormalizeScale(3));
            Assert.Equal(4, AppSettings.NormalizeScale(4));
            Assert.Equal(2, AppSettings.NormalizeScale(0));
            Assert.Equal(2, AppSettings.NormalizeScale(5));
            Assert.Equal(2, AppSettings.NormalizeScale(-1));

            foreach (double valid in AppSettings.SleepTimeoutValues)
            {
                Assert.Equal(valid, AppSettings.NormalizeSleepTimeout(valid));
            }
            Assert.Equal(300.0, AppSettings.NormalizeSleepTimeout(0.0));
            Assert.Equal(300.0, AppSettings.NormalizeSleepTimeout(45.0));
            Assert.Equal(300.0, AppSettings.NormalizeSleepTimeout(-99.0));
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

        [Fact]
        public void StepInhaledEnemyPhysics_PullsEnemyRapidlyTowardKirby_AndSignalsTouchAtThreshold()
        {
            SpriteTile[] inhaledTiles = CreateDummyTiles(1);
            EnemyDef enemy = new EnemyDef
            {
                Name = "waddle-dee",
                WalkSpeed = 0.55,
                Inhaled = new AnimClip
                {
                    Speed = 0.0,
                    RawFrames = inhaledTiles,
                    Frames = inhaledTiles
                }
            };

            double kirbyX = 100.0;
            double enemyX = 240.0;
            double animTime = 0.0;
            int scale = 2;

            // Single 60Hz tick at 2x scale pulls enemy left by INHALE_PULL_SPEED * 2 = 6.0px.
            bool stillFlying = KirbyTaskbarBuddyContext.StepInhaledEnemyPhysics(ref enemyX, ref animTime, enemy, kirbyX, 1.0, scale);
            Assert.True(stillFlying);
            Assert.Equal(240.0 - KirbyTaskbarBuddyContext.INHALE_PULL_SPEED * scale, enemyX, 5);

            // Step until enemy reaches Kirby's touch threshold (kirbyX + INHALE_TOUCH_DISTANCE * scale = 116.0).
            int ticks = 0;
            while (stillFlying && ticks < 100)
            {
                stillFlying = KirbyTaskbarBuddyContext.StepInhaledEnemyPhysics(ref enemyX, ref animTime, enemy, kirbyX, 1.0, scale);
                ticks++;
            }

            Assert.False(stillFlying);
            Assert.True(enemyX <= kirbyX + KirbyTaskbarBuddyContext.INHALE_TOUCH_DISTANCE * scale);
            Assert.False(KirbyTaskbarBuddyContext.StepInhaledEnemyPhysics(ref enemyX, ref animTime, null, kirbyX, 1.0, scale));
        }

        [Fact]
        public void CanStartInhale_RequiresNormalFormAwakeGroundedAndEnemyWithinScaledRange()
        {
            SpriteTile[] inhaleTiles = CreateDummyTiles(2);
            FormDef normalForm = new FormDef
            {
                Name = "normal",
                Key = "0",
                Inhale = new AnimClip { RawFrames = inhaleTiles, Frames = inhaleTiles }
            };
            FormDef swordForm = new FormDef
            {
                Name = "sword",
                Key = "w",
                Inhale = null
            };
            EnemyDef enemy = new EnemyDef { Name = "waddle-dee" };

            // In range in front of Normal Kirby at 2x scale (max distance = INHALE_RANGE * 2):
            double maxInhaleX = 100.0 + KirbyTaskbarBuddyContext.INHALE_RANGE * 2;
            Assert.True(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, maxInhaleX, 2));

            // Out of range (exceeds INHALE_RANGE * 2):
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, maxInhaleX + 1.0, 2));

            // Enemy already behind Kirby (enemyX < kirbyX):
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, 99.0, 2));

            // Copy ability form (not "normal"), sleeping, jumping, guarding, already inhaling, or no active enemy:
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(swordForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, 140.0, 2));
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Sleeping, false, SwallowState.None, enemy, 100.0, 140.0, 2));
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Awake, true, SwallowState.None, enemy, 100.0, 140.0, 2));
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, 140.0, 2, isGuarding: true));
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.Inhaling, enemy, 100.0, 140.0, 2));
            Assert.False(KirbyTaskbarBuddyContext.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, null, 100.0, 140.0, 2));
        }

        [Fact]
        public void TryTriggerEnemyHit_And_AdvanceHitState_StripsCopyAbilityDespawnsEnemyAndPlaysTwoSixFramePoses()
        {
            SpriteTile[] rawHitTiles = CreateDummyTiles(3);
            SpriteTile[] hitFrames = new SpriteTile[FormDefinitions.HitSeq.Length];
            for (int i = 0; i < hitFrames.Length; i++)
            {
                hitFrames[i] = rawHitTiles[FormDefinitions.HitSeq[i]];
            }

            FormDef normalForm = new FormDef
            {
                Name = "normal",
                Key = "0",
                Hit = new AnimClip
                {
                    Speed = 1.0,
                    Loop = false,
                    Sequence = FormDefinitions.HitSeq,
                    RawFrames = rawHitTiles,
                    Frames = hitFrames
                }
            };
            FormDef swordForm = new FormDef
            {
                Name = "sword",
                Key = "w"
            };
            FormDef[] forms = new FormDef[] { normalForm, swordForm };
            EnemyDef enemy = new EnemyDef { Name = "waddle-dee" };

            FormDef currentForm = swordForm;
            bool isHit = false;
            double hitAnimTime = 0.0;
            EnemyDef activeEnemy = enemy;
            bool isEnemyHit = false;
            double enemyHitAnimTime = 0.0;
            int scale = 2;
            double kirbyX = 200.0;
            double contactThreshold = KirbyTaskbarBuddyContext.ENEMY_CONTACT_DISTANCE * scale;
            double enemyX = kirbyX + contactThreshold;

            // Outside contact distance, jumping, guarding, sleeping, or inhaling does not trigger enemy hit:
            Assert.False(KirbyTaskbarBuddyContext.TryTriggerEnemyHit(
                forms, ref currentForm, SleepState.Awake, false, SwallowState.None,
                ref isHit, ref hitAnimTime, ref activeEnemy, ref isEnemyHit, ref enemyHitAnimTime,
                kirbyX, kirbyX + contactThreshold + 1.0, scale));
            Assert.False(KirbyTaskbarBuddyContext.TryTriggerEnemyHit(
                forms, ref currentForm, SleepState.Awake, false, SwallowState.None,
                ref isHit, ref hitAnimTime, ref activeEnemy, ref isEnemyHit, ref enemyHitAnimTime,
                kirbyX, kirbyX, scale, isGuarding: true));
            Assert.Same(swordForm, currentForm);
            Assert.Same(enemy, activeEnemy);
            Assert.False(isEnemyHit);
            Assert.False(KirbyTaskbarBuddyContext.CanTakeEnemyHit(
                SleepState.Awake, true, SwallowState.None, false, enemy, kirbyX, kirbyX, scale));
            Assert.False(KirbyTaskbarBuddyContext.CanTakeEnemyHit(
                SleepState.Awake, false, SwallowState.None, false, enemy, kirbyX, kirbyX, scale, isGuarding: true));
            Assert.False(KirbyTaskbarBuddyContext.CanTakeEnemyHit(
                SleepState.Sleeping, false, SwallowState.None, false, enemy, kirbyX, kirbyX, scale));
            Assert.False(KirbyTaskbarBuddyContext.CanTakeEnemyHit(
                SleepState.Awake, false, SwallowState.Inhaling, false, enemy, kirbyX, kirbyX, scale));

            // Walking into the enemy within contact distance immediately strips Sword ability back to Normal
            // and enters the hit animation for both Kirby and the enemy:
            Assert.True(KirbyTaskbarBuddyContext.TryTriggerEnemyHit(
                forms, ref currentForm, SleepState.Awake, false, SwallowState.None,
                ref isHit, ref hitAnimTime, ref activeEnemy, ref isEnemyHit, ref enemyHitAnimTime,
                kirbyX, enemyX, scale));
            Assert.Same(normalForm, currentForm);
            Assert.True(isHit);
            Assert.Equal(0.0, hitAnimTime);
            Assert.Same(enemy, activeEnemy);
            Assert.True(isEnemyHit);
            Assert.Equal(0.0, enemyHitAnimTime);

            // First 8 ticks: Kirby recoils left and the enemy recoils right by the exact same distance:
            double startKirbyX = kirbyX;
            double startEnemyX = enemyX;
            for (int tick = 0; tick < 8; tick++)
            {
                double prevKirbyX = kirbyX;
                double prevEnemyX = enemyX;
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                bool enemyActive = KirbyTaskbarBuddyContext.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale);
                Assert.True(isHit);
                Assert.True(enemyActive);
                Assert.Same(rawHitTiles[0], frame);
                Assert.Equal(prevKirbyX - KirbyTaskbarBuddyContext.HIT_PUSHBACK_SPEED * scale, kirbyX, 5);
                Assert.Equal(prevEnemyX + KirbyTaskbarBuddyContext.HIT_PUSHBACK_SPEED * scale, enemyX, 5);
            }
            double expectedPushbackDist = 8 * KirbyTaskbarBuddyContext.HIT_PUSHBACK_SPEED * scale;
            double pushedBackKirbyX = startKirbyX - expectedPushbackDist;
            double pushedBackEnemyX = startEnemyX + expectedPushbackDist;
            Assert.Equal(pushedBackKirbyX, kirbyX, 5);
            Assert.Equal(pushedBackEnemyX, enemyX, 5);

            // Next 8 ticks (ticks 8..15): Kirby holds frame 0 and enemy holds its hit state without further pushback:
            for (int tick = 0; tick < 8; tick++)
            {
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                bool enemyActive = KirbyTaskbarBuddyContext.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale);
                Assert.True(isHit);
                Assert.True(enemyActive);
                Assert.Same(rawHitTiles[0], frame);
                Assert.Equal(pushedBackKirbyX, kirbyX, 5);
                Assert.Equal(pushedBackEnemyX, enemyX, 5);
            }

            // Next 8 ticks (ticks 16..23): Kirby plays frame 1 and enemy finishes the remaining 8 frames of its 24-frame hit state:
            for (int tick = 0; tick < 8; tick++)
            {
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                bool enemyActive = KirbyTaskbarBuddyContext.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale);
                Assert.True(isHit);
                Assert.True(enemyActive);
                Assert.Same(rawHitTiles[1], frame);
                Assert.Equal(pushedBackKirbyX, kirbyX, 5);
                Assert.Equal(pushedBackEnemyX, enemyX, 5);
            }

            // On tick 24, the enemy completes its 24-frame hit sequence and disappears, while Kirby plays his 9th tick of frame 1:
            Assert.False(KirbyTaskbarBuddyContext.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale));
            SpriteTile frameTick24 = KirbyTaskbarBuddyContext.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
            Assert.True(isHit);
            Assert.Same(rawHitTiles[1], frameTick24);

            // Next 9 ticks (ticks 25..33): Kirby holds standing frame (frame 2) before resuming walking:
            for (int tick = 0; tick < 9; tick++)
            {
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                Assert.True(isHit);
                Assert.Same(rawHitTiles[2], frame);
                Assert.Equal(pushedBackKirbyX, kirbyX, 5);
            }

            // On tick 34, Kirby's hit animation finishes and returns null:
            SpriteTile doneFrame = KirbyTaskbarBuddyContext.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
            Assert.False(isHit);
            Assert.Equal(0.0, hitAnimTime);
            Assert.Equal(pushedBackKirbyX, kirbyX, 5);
            Assert.Null(doneFrame);
        }

        [Fact]
        public void AdvanceSwallowState_ProgressesFromMouthfulToSwallowingToNone()
        {
            SpriteTile[] mouthfulTiles = CreateDummyTiles(1);
            SpriteTile[] swallowTiles = CreateDummyTiles(3);
            FormDef normalForm = new FormDef
            {
                Name = "normal",
                Key = "0",
                Mouthful = new AnimClip { Speed = 0.08, Loop = false, RawFrames = mouthfulTiles, Frames = mouthfulTiles },
                Swallow = new AnimClip { Speed = 0.15, Loop = false, RawFrames = swallowTiles, Frames = swallowTiles }
            };

            SwallowState state = SwallowState.Mouthful;
            double animTime = 0.0;

            // Step through Mouthful until it transitions to Swallowing:
            SpriteTile frame = null;
            int mouthfulTicks = 0;
            while (state == SwallowState.Mouthful && mouthfulTicks < 100)
            {
                frame = KirbyTaskbarBuddyContext.AdvanceSwallowState(ref state, ref animTime, 1.0, normalForm);
                Assert.NotNull(frame);
                mouthfulTicks++;
            }

            Assert.Equal(SwallowState.Swallowing, state);
            Assert.Same(swallowTiles[0], frame);
            Assert.True(mouthfulTicks > 10, "Mouthful should hold for multiple frames before swallowing");

            // Step through Swallowing across all 3 frames until it returns to SwallowState.None:
            bool sawFrame1 = false;
            bool sawFrame2 = false;
            int swallowTicks = 0;
            while (state == SwallowState.Swallowing && swallowTicks < 100)
            {
                frame = KirbyTaskbarBuddyContext.AdvanceSwallowState(ref state, ref animTime, 1.0, normalForm);
                if (ReferenceEquals(frame, swallowTiles[1])) sawFrame1 = true;
                if (ReferenceEquals(frame, swallowTiles[2])) sawFrame2 = true;
                swallowTicks++;
            }

            Assert.Equal(SwallowState.None, state);
            Assert.Equal(0.0, animTime);
            Assert.Null(frame);
            Assert.True(sawFrame1 && sawFrame2, "Swallow animation should advance through frames 1 and 2");
        }

        [Fact]
        public void AdvanceSwallowState_TransformsIntoSwallowedEnemyCopyAbility_OrStaysNormalWhenNone()
        {
            SpriteTile[] mouthfulTiles = CreateDummyTiles(1);
            SpriteTile[] rawSwallowTiles = CreateDummyTiles(5);
            SpriteTile[] noAbilityFrames = new SpriteTile[FormDefinitions.SwallowNoAbilitySeq.Length];
            for (int i = 0; i < noAbilityFrames.Length; i++)
            {
                noAbilityFrames[i] = rawSwallowTiles[FormDefinitions.SwallowNoAbilitySeq[i]];
            }
            SpriteTile[] abilityFrames = new SpriteTile[FormDefinitions.SwallowAbilitySeq.Length];
            for (int i = 0; i < abilityFrames.Length; i++)
            {
                abilityFrames[i] = rawSwallowTiles[FormDefinitions.SwallowAbilitySeq[i]];
            }

            FormDef normalForm = new FormDef
            {
                Name = "normal",
                Key = "0",
                Mouthful = new AnimClip { Speed = 1.0, Loop = false, RawFrames = mouthfulTiles, Frames = mouthfulTiles },
                Swallow = new AnimClip { Speed = 0.5, Loop = false, RawFrames = rawSwallowTiles, Frames = noAbilityFrames },
                SwallowAbility = new AnimClip { Speed = 1.0, Loop = false, RawFrames = rawSwallowTiles, Frames = abilityFrames }
            };
            FormDef beamForm = new FormDef
            {
                Name = "beam",
                Key = "e"
            };
            FormDef[] forms = new FormDef[] { normalForm, beamForm };

            EnemyDef waddleDee = new EnemyDef { Name = "waddle-dee", CopyAbility = null };
            EnemyDef waddleDoo = new EnemyDef { Name = "waddle-doo", CopyAbility = "beam" };

            // 1. Swallowing Waddle Dee (CopyAbility = null) plays normal Swallow and keeps Kirby in Normal form:
            SwallowState state = SwallowState.Mouthful;
            double animTime = 0.0;
            FormDef currentForm = normalForm;
            EnemyDef swallowedEnemy = waddleDee;

            for (int tick = 0; tick < 50 && state != SwallowState.None; tick++)
            {
                KirbyTaskbarBuddyContext.AdvanceSwallowState(ref state, ref animTime, 1.0, ref currentForm, forms, ref swallowedEnemy);
            }

            Assert.Equal(SwallowState.None, state);
            Assert.Same(normalForm, currentForm);
            Assert.Null(swallowedEnemy);

            // 2. Swallowing Waddle Doo (CopyAbility = "beam") steps through all 37 SwallowAbility frames and transforms into Beam form:
            state = SwallowState.Mouthful;
            animTime = 0.0;
            currentForm = normalForm;
            swallowedEnemy = waddleDoo;

            System.Collections.Generic.List<SpriteTile> playedAbilityFrames = new System.Collections.Generic.List<SpriteTile>();
            for (int tick = 0; tick < 60 && state != SwallowState.None; tick++)
            {
                SpriteTile frame = KirbyTaskbarBuddyContext.AdvanceSwallowState(ref state, ref animTime, 1.0, ref currentForm, forms, ref swallowedEnemy);
                if (state == SwallowState.Swallowing && frame != null)
                {
                    playedAbilityFrames.Add(frame);
                }
            }

            Assert.Equal(SwallowState.None, state);
            Assert.Same(beamForm, currentForm);
            Assert.Null(swallowedEnemy);
            Assert.Equal(37, playedAbilityFrames.Count);
            for (int i = 0; i < 37; i++)
            {
                Assert.Same(rawSwallowTiles[FormDefinitions.SwallowAbilitySeq[i]], playedAbilityFrames[i]);
            }
        }

        [Fact]
        public void InhaleParticles_SpawnFourInRectangularArea_MoveTowardMouth_AndRespawnOnDespawn()
        {
            System.Random rng = new System.Random(42);
            InhaleParticle[] particles = new InhaleParticle[InhaleParticleEffect.PARTICLE_COUNT];

            // 1. Start with 4 particle spawns in the rectangular area in front of Kirby:
            Assert.Equal(4, particles.Length);
            InhaleParticleEffect.InitParticles(particles, rng);

            for (int i = 0; i < particles.Length; i++)
            {
                Assert.InRange(particles[i].Dx, InhaleParticleEffect.PARTICLE_MIN_DX, InhaleParticleEffect.PARTICLE_MAX_DX);
                Assert.InRange(particles[i].Dy, InhaleParticleEffect.PARTICLE_MIN_DY, InhaleParticleEffect.PARTICLE_MAX_DY);
                Assert.Equal(particles[i].Dy <= 0.0, particles[i].BlackOnTop);
            }

            // 2. Each step moves particles closer to the mouth (0, 0), and reaching the mouth immediately spawns a new one:
            particles[0] = new InhaleParticle { Dx = 30.0, Dy = -10.0, BlackOnTop = true };
            particles[1] = new InhaleParticle { Dx = 1.5, Dy = 0.5, BlackOnTop = false }; // Within 1 tick of mouth -> will despawn & respawn

            double prevDist0 = System.Math.Sqrt(particles[0].Dx * particles[0].Dx + particles[0].Dy * particles[0].Dy);
            InhaleParticleEffect.StepParticles(particles, 1.0, rng);
            double nextDist0 = System.Math.Sqrt(particles[0].Dx * particles[0].Dx + particles[0].Dy * particles[0].Dy);

            // Particle 0 moved toward the mouth by PARTICLE_SPEED:
            Assert.Equal(prevDist0 - InhaleParticleEffect.PARTICLE_SPEED, nextDist0, 5);
            Assert.True(particles[0].Dx > 0.0 && particles[0].Dy < 0.0);

            // Particle 1 reached the mouth, despawned, and respawned in the rectangular spawn area:
            Assert.InRange(particles[1].Dx, InhaleParticleEffect.PARTICLE_MIN_DX, InhaleParticleEffect.PARTICLE_MAX_DX);
            Assert.InRange(particles[1].Dy, InhaleParticleEffect.PARTICLE_MIN_DY, InhaleParticleEffect.PARTICLE_MAX_DY);

            // 3. RenderParticlesTile rasterizes the 4 particles into a valid SpriteTile:
            int[] scratch = new int[CanvasConstants.TILE_SIZE * CanvasConstants.TILE_SIZE];
            SpriteTile reusableTile = new SpriteTile { PremulPixels = new int[CanvasConstants.TILE_SIZE * CanvasConstants.TILE_SIZE] };
            SpriteTile rendered = InhaleParticleEffect.RenderParticlesTile(particles, scratch, reusableTile);

            Assert.Same(reusableTile, rendered);
            Assert.True(rendered.Width >= 2 && rendered.Height >= 4);
            Assert.InRange(rendered.MinX, 0, CanvasConstants.TILE_SIZE - 1);
            Assert.InRange(rendered.MinY, 0, CanvasConstants.TILE_SIZE - 1);

            // 4. InhaleParticleEffect instance steps and renders a SpriteTile:
            InhaleParticleEffect effect = new InhaleParticleEffect(new System.Random(7));
            effect.Reset();
            Assert.True(effect.Step(1.0));
            Assert.NotNull(effect.Render());
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

        [Fact]
        public void FullscreenFilterHelpers_IgnoreShellClassesAndOverlayStyles()
        {
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Progman"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("WorkerW"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Shell_TrayWnd"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Shell_SecondaryTrayWnd"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Windows.UI.Core.CoreWindow"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("XamlExplorerHostIslandWindow"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("MultitaskingViewFrame"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("ForegroundStaging"));

            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass(null));
            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass(""));
            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("UnityWndClass"));
            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("UnrealWindow"));

            Assert.True(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_TRANSPARENT));
            Assert.True(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_NOACTIVATE));
            Assert.True(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_TOOLWINDOW));
            Assert.False(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(0));
            Assert.False(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_TOPMOST));
        }

        [Fact]
        public void TryParseRegistryHelpers_ParseValidValues_AndRejectCorruptOrNullWithoutThrowing()
        {
            int intResult;
            Assert.True(RegistrySettingsService.TryParseRegistryInt(3, out intResult));
            Assert.Equal(3, intResult);

            Assert.True(RegistrySettingsService.TryParseRegistryInt(4L, out intResult));
            Assert.Equal(4, intResult);

            Assert.True(RegistrySettingsService.TryParseRegistryInt("2", out intResult));
            Assert.Equal(2, intResult);

            Assert.False(RegistrySettingsService.TryParseRegistryInt(null, out intResult));
            Assert.False(RegistrySettingsService.TryParseRegistryInt("invalid", out intResult));
            Assert.False(RegistrySettingsService.TryParseRegistryInt(long.MaxValue, out intResult));
            Assert.False(RegistrySettingsService.TryParseRegistryInt(new byte[] { 1, 2 }, out intResult));

            double doubleResult;
            Assert.True(RegistrySettingsService.TryParseRegistryDouble(60, out doubleResult));
            Assert.Equal(60.0, doubleResult);

            Assert.True(RegistrySettingsService.TryParseRegistryDouble(180L, out doubleResult));
            Assert.Equal(180.0, doubleResult);

            Assert.True(RegistrySettingsService.TryParseRegistryDouble(300.0, out doubleResult));
            Assert.Equal(300.0, doubleResult);

            Assert.True(RegistrySettingsService.TryParseRegistryDouble("-1", out doubleResult));
            Assert.Equal(-1.0, doubleResult);

            Assert.False(RegistrySettingsService.TryParseRegistryDouble(null, out doubleResult));
            Assert.False(RegistrySettingsService.TryParseRegistryDouble("not-a-number", out doubleResult));
            Assert.False(RegistrySettingsService.TryParseRegistryDouble(new object(), out doubleResult));
        }

        [Fact]
        public void GlobalKeyboardHook_DeduplicatesAutoRepeatUntilKeyUp_AndIdleMonitorTracksElapsedSeconds()
        {
            int keyDownCount = 0;
            int initialDownCount = 0;

            using (GlobalKeyboardHook hook = new GlobalKeyboardHook(
                (vk, isInitial) =>
                {
                    keyDownCount++;
                    if (isInitial) initialDownCount++;
                },
                installOsHook: false))
            {
                hook.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, 0x31);
                hook.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, 0x31); // Auto-repeat while held
                Assert.Equal(2, keyDownCount);
                Assert.Equal(1, initialDownCount);

                hook.ProcessKeyMessage(NativeMethods.WM_KEYUP, 0x31);
                hook.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, 0x31); // Pressed again after release
                Assert.Equal(3, keyDownCount);
                Assert.Equal(2, initialDownCount);
            }

            IdleActivityMonitor monitor = new IdleActivityMonitor(1000, new Point(10, 10));
            Assert.Equal(4.5, monitor.GetIdleSeconds(5500), 5);
            monitor.RecordActivity(5000);
            Assert.Equal(0.5, monitor.GetIdleSeconds(5500), 5);
            Assert.True(monitor.PollActivity(new Point(25, 10)));
        }

        [Fact]
        public void KirbyOverlayWindow_ClearOverlayAndUpdateSpriteFrame_ToggleIsClearedStateWithoutShowHide()
        {
            using (KirbyOverlayWindow window = new KirbyOverlayWindow())
            {
                System.IntPtr handle = window.Handle;
                Assert.NotEqual(System.IntPtr.Zero, handle);
                Assert.False(window.IsCleared);

                window.ClearOverlay(2);
                Assert.True(window.IsCleared);

                // Calling ClearOverlay again while already cleared is an idempotent no-op.
                window.ClearOverlay(2);
                Assert.True(window.IsCleared);

                SpriteTile tile = CreateDummyTiles(1)[0];
                window.UpdateSpriteFrame(tile, 2, 100, 100);
                Assert.False(window.IsCleared);

                // RenderAtTaskbar with null tile clears the overlay; with a valid tile renders and clears IsCleared.
                System.Drawing.Rectangle workArea = new System.Drawing.Rectangle(0, 0, 1920, 1040);
                window.RenderAtTaskbar(null, 2, workArea, 200.0, 0.0);
                Assert.True(window.IsCleared);
                window.RenderAtTaskbar(tile, 2, workArea, 200.0, 0.0);
                Assert.False(window.IsCleared);
            }

            using (OverlayStage stage = new OverlayStage(2))
            {
                Assert.NotNull(stage.EnemyLayer);
                Assert.NotNull(stage.KirbyLayer);
                Assert.NotNull(stage.VfxLayer);
                stage.ClearAll(2);
                Assert.True(stage.EnemyLayer.IsCleared);
                Assert.True(stage.KirbyLayer.IsCleared);
                Assert.True(stage.VfxLayer.IsCleared);
            }
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
