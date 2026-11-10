using System;
using System.Collections.Generic;
using System.Drawing;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Coordinates the taskbar scene: updates <see cref="KirbyActor"/>, <see cref="EnemySpawner"/>,
    /// combat interactions (inhale, mouthful, swallow, collision knockback), and renders all active layers
    /// onto <see cref="OverlayStage"/>.
    /// </summary>
    public sealed class TaskbarWorld {

        private readonly KirbyActor _kirby;
        private readonly EnemySpawner _enemySpawner;

        public KirbyActor Kirby {
            get { return _kirby; }
        }

        public EnemySpawner Spawner {
            get { return _enemySpawner; }
        }

        public TaskbarWorld(
            IList<FormDef> forms,
            IDictionary<string, FormDef> keyToForm,
            FormDef initialForm,
            IList<EnemyDef> enemies,
            SpriteTile[] sleepFrames,
            Random rng,
            int initialScale) {
            _kirby = new KirbyActor(forms, keyToForm, initialForm, sleepFrames, rng, initialScale);
            _enemySpawner = new EnemySpawner(enemies, rng);
        }

        public void CheckIdleSleep(double idleSeconds, double idleSleepThresholdSeconds) {
            if (idleSleepThresholdSeconds > 0 &&
                _kirby.Sleep.IsAwake &&
                !_kirby.Jump.IsJumping &&
                _kirby.Swallow.State == SwallowState.None &&
                !_kirby.HitRecoil.IsHit &&
                _kirby.Sleep.HasSleepFrames &&
                idleSeconds >= idleSleepThresholdSeconds) {
                _kirby.Sleep.BeginSleep(_kirby.CurrentForm);
            }
        }

        public void DisableCombat(OverlayStage stage, int scale) {
            _kirby.ResetCombatState();
            _enemySpawner.ClearActiveEnemy();

            if (stage != null) {
                if (stage.EnemyLayer != null) {
                    stage.EnemyLayer.ClearOverlay(scale);
                }

                if (stage.VfxLayer != null) {
                    stage.VfxLayer.ClearOverlay(scale);
                }
            }
        }

        public void Tick(
            double dtSeconds,
            Point mouse,
            Rectangle workArea,
            AppSettings settings,
            OverlayStage stage) {
            if (stage == null || settings == null) {
                return;
            }

            int scale = settings.Scale;
            int halfWidth = CanvasConstants.TILE_SIZE / 2 * scale;
            double delta60 = dtSeconds * 60.0;

            UpdateEnemy(dtSeconds, delta60, workArea, halfWidth, settings.CombatEnabled, scale, stage.EnemyLayer);

            SpriteTile frameToRender = _kirby.Sleep.Advance(delta60, _kirby.CurrentForm);

            if (_kirby.Sleep.IsAwake) {
                FormDef currentForm = _kirby.CurrentForm;

                if (currentForm == null || currentForm.Walk == null || !currentForm.Walk.HasFrames()) {
                    return;
                }

                bool isMouseNear = JumpPhysicsState.IsMouseNearKirby(
                    mouse,
                    _kirby.X,
                    workArea.Bottom,
                    scale,
                    _kirby.Jump.IsJumping);
                bool isGuarding = !_kirby.Jump.IsJumping && isMouseNear && currentForm.Guard != null &&
                                  currentForm.Guard.HasFrames();

                EnemyActor enemy = _enemySpawner.Actor;

                if (settings.CombatEnabled &&
                    HitRecoilState.TryTriggerEnemyHit(
                        _kirby.Forms,
                        ref _kirby.CurrentForm,
                        _kirby.Sleep.State,
                        _kirby.Jump.IsJumping,
                        _kirby.Swallow.State,
                        ref _kirby.HitRecoil.IsHit,
                        ref _kirby.HitRecoil.AnimTime,
                        ref enemy.Def,
                        ref enemy.HitRecoil.IsHit,
                        ref enemy.HitRecoil.AnimTime,
                        _kirby.X,
                        enemy.X,
                        scale,
                        isGuarding)) {
                    _kirby.AnimTime = 0.0;
                    StepHitEnemyOnContact(delta60, workArea, scale, stage.EnemyLayer);
                }

                if (_kirby.HitRecoil.IsHit) {
                    _kirby.ActiveVfx = null;
                    frameToRender = _kirby.HitRecoil.AdvanceKirby(
                        ref _kirby.X,
                        delta60,
                        _kirby.CurrentForm,
                        scale);
                }

                bool startedInhaleThisTick = false;

                if (!_kirby.HitRecoil.IsHit &&
                    _kirby.Swallow.State == SwallowState.None &&
                    SwallowController.CanStartInhale(
                        _kirby.CurrentForm,
                        _kirby.Sleep.State,
                        _kirby.Jump.IsJumping,
                        _kirby.Swallow.State,
                        enemy.Def,
                        _kirby.X,
                        enemy.X,
                        scale,
                        isGuarding,
                        enemy.IsHit)) {
                    _kirby.Swallow.BeginInhale();
                    _kirby.InhaleVfx.Reset();
                    _kirby.ActiveVfx = _kirby.InhaleVfx;
                    startedInhaleThisTick = true;
                }

                if (!_kirby.HitRecoil.IsHit && _kirby.Swallow.State == SwallowState.Inhaling) {
                    if (!settings.CombatEnabled || !enemy.IsActive || enemy.IsHit || enemy.X < _kirby.X ||
                        _kirby.CurrentForm.Inhale == null || !_kirby.CurrentForm.Inhale.HasFrames()) {
                        _kirby.Swallow.Reset();
                        _kirby.ActiveVfx = null;
                    }
                    else {
                        _kirby.Swallow.AnimTime += _kirby.CurrentForm.Inhale.Speed * delta60;
                        frameToRender = _kirby.CurrentForm.Inhale.GetFrame(_kirby.Swallow.AnimTime);

                        if (!startedInhaleThisTick) {
                            _kirby.InhaleVfx.Step(delta60);
                        }

                        _kirby.ActiveVfx = _kirby.InhaleVfx;
                    }
                }
                else if (_kirby.Swallow.State == SwallowState.Mouthful ||
                         _kirby.Swallow.State == SwallowState.Swallowing) {
                    _kirby.ActiveVfx = null;
                    FormDef prevForm = _kirby.CurrentForm;
                    frameToRender = _kirby.Swallow.Advance(delta60, ref _kirby.CurrentForm, _kirby.Forms);

                    if (_kirby.CurrentForm != prevForm) {
                        _kirby.AnimTime = 0.0;
                    }
                }

                if (_kirby.Swallow.State == SwallowState.None && !_kirby.HitRecoil.IsHit) {
                    _kirby.ActiveVfx = null;
                    currentForm = _kirby.CurrentForm;

                    bool useJump = _kirby.Jump.IsJumping && currentForm.Jump != null && currentForm.Jump.HasFrames(5);
                    bool useGuard = !useJump && isGuarding;
                    AnimClip activeClip = useJump ? currentForm.Jump : useGuard ? currentForm.Guard : currentForm.Walk;

                    if (_kirby.Jump.IsJumping) {
                        _kirby.Jump.Step(ref _kirby.X, delta60, scale);
                    }
                    else if (!isMouseNear) {
                        _kirby.X += JumpPhysicsState.WALK_SPEED * scale * delta60;
                        _kirby.AnimTime += activeClip.Speed * delta60;
                    }
                    else {
                        // Match SNES Kirby Super Star guard flash / shield shimmer speed (4 frames per state @ 60Hz = 15 FPS).
                        _kirby.AnimTime += activeClip.Speed * delta60;
                    }

                    // Wrap Kirby seamlessly from the right edge of the monitor back to the left edge.
                    if (_kirby.X > workArea.Right + halfWidth) {
                        _kirby.X = workArea.Left - halfWidth;
                    }

                    if (settings.CombatEnabled &&
                        HitRecoilState.TryTriggerEnemyHit(
                            _kirby.Forms,
                            ref _kirby.CurrentForm,
                            _kirby.Sleep.State,
                            _kirby.Jump.IsJumping,
                            _kirby.Swallow.State,
                            ref _kirby.HitRecoil.IsHit,
                            ref _kirby.HitRecoil.AnimTime,
                            ref enemy.Def,
                            ref enemy.HitRecoil.IsHit,
                            ref enemy.HitRecoil.AnimTime,
                            _kirby.X,
                            enemy.X,
                            scale,
                            useGuard)) {
                        _kirby.AnimTime = 0.0;
                        StepHitEnemyOnContact(delta60, workArea, scale, stage.EnemyLayer);
                        frameToRender = _kirby.HitRecoil.AdvanceKirby(
                            ref _kirby.X,
                            delta60,
                            _kirby.CurrentForm,
                            scale);
                    }
                    else if (useJump) {
                        frameToRender = activeClip.Frames[_kirby.Jump.GetFrameIndex()];
                    }
                    else {
                        frameToRender = AnimClip.GetFrame(activeClip, _kirby.AnimTime);
                    }
                }
            }
            else {
                _kirby.ActiveVfx = null;
            }

            if (frameToRender == null) {
                return;
            }

            stage.KirbyLayer.RenderAtTaskbar(frameToRender, scale, workArea, _kirby.X, _kirby.Jump.JumpY);
            RenderVfx(workArea, scale, stage.VfxLayer);
        }

        private void StepHitEnemyOnContact(double delta60, Rectangle workArea, int scale, ISpriteLayer enemyLayer) {
            EnemyActor enemy = _enemySpawner.Actor;
            enemy.AnimTime = 0.0;

            if (enemy.HitRecoil.StepEnemy(ref enemy.X, enemy.Def, delta60, scale)) {
                RenderActiveEnemy(workArea, scale, true, enemyLayer);
            }
        }

        private void UpdateEnemy(
            double dtSeconds,
            double delta60,
            Rectangle workArea,
            int halfWidth,
            bool combatEnabled,
            int scale,
            ISpriteLayer enemyLayer) {
            if (enemyLayer == null) {
                return;
            }

            if (!combatEnabled || !_enemySpawner.HasEnemyCatalog) {
                enemyLayer.ClearOverlay(scale);
                return;
            }

            if (!_enemySpawner.EnsureActiveOrTickTimer(dtSeconds, workArea, halfWidth)) {
                return;
            }

            EnemyActor enemy = _enemySpawner.Actor;
            int despawnLeftX = workArea.Left - halfWidth;
            bool isBeingInhaled = _kirby.Swallow.State == SwallowState.Inhaling;
            bool remainActive = enemy.Step(delta60, scale, despawnLeftX, isBeingInhaled, _kirby.X);

            if (!remainActive) {
                if (isBeingInhaled && !enemy.IsHit) {
                    _kirby.Swallow.BeginMouthful(enemy.Def);
                    _kirby.ActiveVfx = null;
                }

                _enemySpawner.ClearActiveEnemy();
                enemyLayer.ClearOverlay(scale);
                return;
            }

            RenderActiveEnemy(workArea, scale, isBeingInhaled || enemy.IsHit, enemyLayer);
        }

        private void RenderActiveEnemy(
            Rectangle workArea,
            int scale,
            bool useHitOrInhaledPose,
            ISpriteLayer enemyLayer) {
            EnemyActor enemy = _enemySpawner.Actor;

            if (!enemy.IsActive || enemyLayer == null) {
                return;
            }

            SpriteTile enemyTile = enemy.GetCurrentFrame(useHitOrInhaledPose);
            enemyLayer.RenderAtTaskbar(enemyTile, scale, workArea, enemy.X, 0.0);
        }

        private void RenderVfx(Rectangle workArea, int scale, ISpriteLayer vfxLayer) {
            if (vfxLayer == null) {
                return;
            }

            IVfxEffect activeVfx = _kirby.ActiveVfx;
            SpriteTile vfxTile = activeVfx != null ? activeVfx.Render() : null;

            if (vfxTile == null) {
                vfxLayer.ClearOverlay(scale);
                return;
            }

            double vfxWorldX = _kirby.X + activeVfx.OffsetX * scale;
            vfxLayer.RenderAtTaskbar(vfxTile, scale, workArea, vfxWorldX, activeVfx.OffsetY);
        }

    }

}