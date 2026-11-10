using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class CombatAndEnemyTests
    {
        [Fact]
        public void NextEnemySpawnInterval_ReturnsDelayWithinConfiguredBounds_AndHandlesEdgeCases()
        {
            System.Random rng = new System.Random(12345);
            for (int i = 0; i < 100; i++)
            {
                double delay = EnemySpawner.NextSpawnInterval(
                    rng,
                    EnemySpawner.SPAWN_MIN_SECONDS,
                    EnemySpawner.SPAWN_MAX_SECONDS);
                Assert.InRange(delay, EnemySpawner.SPAWN_MIN_SECONDS, EnemySpawner.SPAWN_MAX_SECONDS);
            }

            Assert.Equal(5.0, EnemySpawner.NextSpawnInterval(null, 5.0, 600.0));
            Assert.Equal(10.0, EnemySpawner.NextSpawnInterval(rng, 10.0, 5.0));
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
            bool active = EnemyActor.StepEnemyPhysics(ref enemyX, ref animTime, enemy, 1.0, scale, despawnLeftX);
            Assert.True(active);
            Assert.Equal(98.9, enemyX, 5);
            Assert.Equal(0.1, animTime, 5);

            // Step until enemy walks past despawnLeftX (-80).
            int ticks = 0;
            while (active && ticks < 500)
            {
                active = EnemyActor.StepEnemyPhysics(ref enemyX, ref animTime, enemy, 1.0, scale, despawnLeftX);
                ticks++;
            }

            Assert.False(active);
            Assert.True(enemyX < despawnLeftX);
            Assert.True(animTime > 15.0);

            // Null enemy or clip without frames immediately returns false.
            Assert.False(EnemyActor.StepEnemyPhysics(ref enemyX, ref animTime, null, 1.0, scale, despawnLeftX));
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

            // Single 60Hz tick at 2x scale pulls enemy left by INHALE_PULL_SPEED * 2 = 4.0px.
            bool stillFlying = EnemyActor.StepInhaledEnemyPhysics(ref enemyX, ref animTime, enemy, kirbyX, 1.0, scale);
            Assert.True(stillFlying);
            Assert.Equal(240.0 - SwallowController.INHALE_PULL_SPEED * scale, enemyX, 5);

            // Step until enemy reaches Kirby's touch threshold (kirbyX + INHALE_TOUCH_DISTANCE * scale = 116.0).
            int ticks = 0;
            while (stillFlying && ticks < 100)
            {
                stillFlying = EnemyActor.StepInhaledEnemyPhysics(ref enemyX, ref animTime, enemy, kirbyX, 1.0, scale);
                ticks++;
            }

            Assert.False(stillFlying);
            Assert.True(enemyX <= kirbyX + SwallowController.INHALE_TOUCH_DISTANCE * scale);
            Assert.False(EnemyActor.StepInhaledEnemyPhysics(ref enemyX, ref animTime, null, kirbyX, 1.0, scale));
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
            double maxInhaleX = 100.0 + SwallowController.INHALE_RANGE * 2;
            Assert.True(SwallowController.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, maxInhaleX, 2));

            // Out of range (exceeds INHALE_RANGE * 2):
            Assert.False(SwallowController.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, maxInhaleX + 1.0, 2));

            // Enemy already behind Kirby (enemyX < kirbyX):
            Assert.False(SwallowController.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, 99.0, 2));

            // Copy ability form (not "normal"), sleeping, jumping, guarding, already inhaling, or no active enemy:
            Assert.False(SwallowController.CanStartInhale(swordForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, 140.0, 2));
            Assert.False(SwallowController.CanStartInhale(normalForm, SleepState.Sleeping, false, SwallowState.None, enemy, 100.0, 140.0, 2));
            Assert.False(SwallowController.CanStartInhale(normalForm, SleepState.Awake, true, SwallowState.None, enemy, 100.0, 140.0, 2));
            Assert.False(SwallowController.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, enemy, 100.0, 140.0, 2, isGuarding: true));
            Assert.False(SwallowController.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.Inhaling, enemy, 100.0, 140.0, 2));
            Assert.False(SwallowController.CanStartInhale(normalForm, SleepState.Awake, false, SwallowState.None, null, 100.0, 140.0, 2));
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
            double contactThreshold = HitRecoilState.ENEMY_CONTACT_DISTANCE * scale;
            double enemyX = kirbyX + contactThreshold;

            // Outside contact distance, jumping, guarding, sleeping, or inhaling does not trigger enemy hit:
            Assert.False(HitRecoilState.TryTriggerEnemyHit(
                forms, ref currentForm, SleepState.Awake, false, SwallowState.None,
                ref isHit, ref hitAnimTime, ref activeEnemy, ref isEnemyHit, ref enemyHitAnimTime,
                kirbyX, kirbyX + contactThreshold + 1.0, scale));
            Assert.False(HitRecoilState.TryTriggerEnemyHit(
                forms, ref currentForm, SleepState.Awake, false, SwallowState.None,
                ref isHit, ref hitAnimTime, ref activeEnemy, ref isEnemyHit, ref enemyHitAnimTime,
                kirbyX, kirbyX, scale, isGuarding: true));
            Assert.Same(swordForm, currentForm);
            Assert.Same(enemy, activeEnemy);
            Assert.False(isEnemyHit);
            Assert.False(HitRecoilState.CanTakeEnemyHit(
                SleepState.Awake, true, SwallowState.None, false, enemy, kirbyX, kirbyX, scale));
            Assert.False(HitRecoilState.CanTakeEnemyHit(
                SleepState.Awake, false, SwallowState.None, false, enemy, kirbyX, kirbyX, scale, isGuarding: true));
            Assert.False(HitRecoilState.CanTakeEnemyHit(
                SleepState.Sleeping, false, SwallowState.None, false, enemy, kirbyX, kirbyX, scale));
            Assert.False(HitRecoilState.CanTakeEnemyHit(
                SleepState.Awake, false, SwallowState.Inhaling, false, enemy, kirbyX, kirbyX, scale));

            // Walking into the enemy within contact distance immediately strips Sword ability back to Normal
            // and enters the hit animation for both Kirby and the enemy:
            Assert.True(HitRecoilState.TryTriggerEnemyHit(
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
                SpriteTile frame = HitRecoilState.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                bool enemyActive = HitRecoilState.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale);
                Assert.True(isHit);
                Assert.True(enemyActive);
                Assert.Same(rawHitTiles[0], frame);
                Assert.Equal(prevKirbyX - HitRecoilState.HIT_PUSHBACK_SPEED * scale, kirbyX, 5);
                Assert.Equal(prevEnemyX + HitRecoilState.HIT_PUSHBACK_SPEED * scale, enemyX, 5);
            }
            double expectedPushbackDist = 8 * HitRecoilState.HIT_PUSHBACK_SPEED * scale;
            double pushedBackKirbyX = startKirbyX - expectedPushbackDist;
            double pushedBackEnemyX = startEnemyX + expectedPushbackDist;
            Assert.Equal(pushedBackKirbyX, kirbyX, 5);
            Assert.Equal(pushedBackEnemyX, enemyX, 5);

            // Next 8 ticks (ticks 8..15): Kirby holds frame 0 and enemy holds its hit state without further pushback:
            for (int tick = 0; tick < 8; tick++)
            {
                SpriteTile frame = HitRecoilState.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                bool enemyActive = HitRecoilState.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale);
                Assert.True(isHit);
                Assert.True(enemyActive);
                Assert.Same(rawHitTiles[0], frame);
                Assert.Equal(pushedBackKirbyX, kirbyX, 5);
                Assert.Equal(pushedBackEnemyX, enemyX, 5);
            }

            // Next 8 ticks (ticks 16..23): Kirby plays frame 1 and enemy finishes the remaining 8 frames of its 24-frame hit state:
            for (int tick = 0; tick < 8; tick++)
            {
                SpriteTile frame = HitRecoilState.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                bool enemyActive = HitRecoilState.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale);
                Assert.True(isHit);
                Assert.True(enemyActive);
                Assert.Same(rawHitTiles[1], frame);
                Assert.Equal(pushedBackKirbyX, kirbyX, 5);
                Assert.Equal(pushedBackEnemyX, enemyX, 5);
            }

            // On tick 24, the enemy completes its 24-frame hit sequence and disappears, while Kirby plays his 9th tick of frame 1:
            Assert.False(HitRecoilState.StepHitEnemyPhysics(ref enemyX, ref enemyHitAnimTime, activeEnemy, 1.0, scale));
            SpriteTile frameTick24 = HitRecoilState.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
            Assert.True(isHit);
            Assert.Same(rawHitTiles[1], frameTick24);

            // Next 9 ticks (ticks 25..33): Kirby holds standing frame (frame 2) before resuming walking:
            for (int tick = 0; tick < 9; tick++)
            {
                SpriteTile frame = HitRecoilState.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
                Assert.True(isHit);
                Assert.Same(rawHitTiles[2], frame);
                Assert.Equal(pushedBackKirbyX, kirbyX, 5);
            }

            // On tick 34, Kirby's hit animation finishes and returns null:
            SpriteTile doneFrame = HitRecoilState.AdvanceHitState(ref isHit, ref hitAnimTime, ref kirbyX, 1.0, currentForm, scale);
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
                frame = SwallowController.AdvanceSwallowState(ref state, ref animTime, 1.0, normalForm);
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
                frame = SwallowController.AdvanceSwallowState(ref state, ref animTime, 1.0, normalForm);
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
                SwallowController.AdvanceSwallowState(ref state, ref animTime, 1.0, ref currentForm, forms, ref swallowedEnemy);
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
                SpriteTile frame = SwallowController.AdvanceSwallowState(ref state, ref animTime, 1.0, ref currentForm, forms, ref swallowedEnemy);
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
