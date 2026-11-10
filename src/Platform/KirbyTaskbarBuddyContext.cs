using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Core application controller coordinating the system tray menu, registry persistence,
    /// global keyboard hook, desktop environment monitor, 60 Hz physics/animation loop, and actor state machines.
    /// </summary>
    public class KirbyTaskbarBuddyContext : ApplicationContext {

        // Movement and jump physics constants (calibrated in unscaled 1x pixels per 60Hz tick)
        internal const double WALK_SPEED = 0.75;
        internal const double JUMP_INIT_VEL = 4.2;
        internal const double JUMP_GRAVITY = 0.18;
        internal const double ENEMY_SPAWN_MIN_SECONDS = 5.0;
        internal const double ENEMY_SPAWN_MAX_SECONDS = 10.0;
        internal const double INHALE_RANGE = 80.0;
        internal const double INHALE_PULL_SPEED = 2.0;
        internal const double INHALE_TOUCH_DISTANCE = 8.0;
        internal const double ENEMY_CONTACT_DISTANCE = 16.0;
        internal const int HIT_PUSHBACK_FRAMES = 8;
        internal const double HIT_PUSHBACK_SPEED = 1.5;
        internal const int ENEMY_HIT_FRAMES = 24;

        private readonly AppSettings _settings = new AppSettings();
        private readonly TrayMenuController _trayMenu;
        private readonly OverlayStage _stage;
        private readonly GlobalKeyboardHook _keyboardHook;
        private readonly IdleActivityMonitor _idleMonitor;
        private readonly DesktopEnvironmentMonitor _envMonitor;
        private readonly Timer _animTimer;
        private readonly Stopwatch _stopwatch;
        private long _lastTickMs;

        private readonly List<FormDef> _forms = new List<FormDef>();
        private readonly Dictionary<string, FormDef> _keyToForm = new Dictionary<string, FormDef>();
        private FormDef _currentForm;

        private readonly List<EnemyDef> _enemies = new List<EnemyDef>();
        private readonly Random _rng = new Random();
        private EnemyDef _activeEnemy;
        private double _enemyX;
        private double _enemyAnimTime;
        private bool _isEnemyHit;
        private double _enemyHitAnimTime;
        private double _enemySpawnTimerSeconds;

        private readonly SpriteTile[] _sleepFrames;
        private SleepState _sleepState = SleepState.Awake;
        private double _sleepAnimTime;

        private SwallowState _swallowState = SwallowState.None;
        private double _swallowAnimTime;
        private EnemyDef _swallowedEnemy;
        private bool _isHit;
        private double _hitAnimTime;

        private readonly InhaleParticleEffect _inhaleVfx;
        private IVfxEffect _activeVfx;

        private double _kirbyX = -64;
        private double _animTime;
        private bool _isJumping;
        private double _jumpY;
        private double _jumpVelY;

        public KirbyTaskbarBuddyContext() {
            Icon loadedTrayIcon;
            IntPtr trayIconHandle;

            FormDefinitions.PopulateForms(_forms, _keyToForm, out _currentForm);
            FormDefinitions.PopulateEnemies(_enemies);
            SpriteLoader.LoadAllSpritesheets(_forms, _enemies, out _sleepFrames, out loadedTrayIcon, out trayIconHandle);

            RegistrySettingsService.LoadSettings(_settings);
            _kirbyX = -32 * _settings.Scale;
            _inhaleVfx = new InhaleParticleEffect(_rng);
            _enemySpawnTimerSeconds = NextEnemySpawnInterval(_rng, ENEMY_SPAWN_MIN_SECONDS, ENEMY_SPAWN_MAX_SECONDS);

            bool startupEnabled = RegistrySettingsService.IsStartupEnabled();

            if (startupEnabled) {
                RegistrySettingsService.SetStartup(true);
            }

            _trayMenu = new TrayMenuController(_settings, startupEnabled, loadedTrayIcon, trayIconHandle);
            _trayMenu.OverlayToggled += OnToggleOverlay;
            _trayMenu.CombatToggled += OnToggleCombat;
            _trayMenu.ScaleSelected += SetScale;
            _trayMenu.SleepTimeoutSelected += SetSleepTimeout;
            _trayMenu.PauseToggled += OnTogglePause;
            _trayMenu.StartupToggled += OnToggleStartup;
            _trayMenu.ExitRequested += OnExit;

            _envMonitor = new DesktopEnvironmentMonitor();
            _stage = new OverlayStage(_settings.Scale);

            _stopwatch = Stopwatch.StartNew();
            _lastTickMs = _stopwatch.ElapsedMilliseconds;
            _idleMonitor = new IdleActivityMonitor(_lastTickMs, Cursor.Position);

            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += OnTick;
            _animTimer.Start();

            _keyboardHook = new GlobalKeyboardHook(OnGlobalKeyDown);

            Diagnostics.TrimMemory();
        }

        /// <summary>
        /// Marshals a clean shutdown onto the UI thread when signaled by a newly launched instance.
        /// </summary>
        public void RequestCleanExit() {
            if (_stage != null) {
                _stage.BeginInvokeOnUiThread(OnExit);
            }
        }

        private void SetScale(int newScale) {
            newScale = AppSettings.NormalizeScale(newScale);

            if (_settings.Scale == newScale) {
                return;
            }

            _settings.Scale = newScale;
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
            RegistrySettingsService.SaveSettings(_settings);
            Diagnostics.TrimMemory();
        }

        private void SetSleepTimeout(double seconds) {
            _settings.IdleSleepSeconds = AppSettings.NormalizeSleepTimeout(seconds);
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());

            if (_settings.IdleSleepSeconds < 0) {
                WakeFromSleep(ref _sleepState, ref _sleepAnimTime);
            }

            RegistrySettingsService.SaveSettings(_settings);
        }

        /// <summary>
        /// Resets the idle timer on keyboard/mouse activity and transitions Kirby out of sleep if napping.
        /// </summary>
        private void NotifyUserActivity() {
            if (_stopwatch != null && _idleMonitor != null) {
                _idleMonitor.RecordActivity(_stopwatch.ElapsedMilliseconds);
            }

            WakeFromSleep(ref _sleepState, ref _sleepAnimTime);
        }

        /// <summary>
        /// Transitions Kirby out of sleep when user activity occurs or when the sleep timer is disabled.
        /// Interrupting <see cref="SleepState.Halting"/> returns directly to <see cref="SleepState.Awake"/>,
        /// whereas interrupting <see cref="SleepState.FallingAsleep"/> or <see cref="SleepState.Sleeping"/>
        /// triggers the startled <see cref="SleepState.WakingUp"/> animation.
        /// </summary>
        internal static void WakeFromSleep(ref SleepState sleepState, ref double sleepAnimTime) {
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
        /// Clamps frame delta time so system suspend/resume or debugger pauses do not teleport Kirby across the screen.
        /// </summary>
        internal static double ClampDeltaTime(double dtSeconds) {
            if (dtSeconds <= 0 || dtSeconds > 0.25) {
                return 0.01667;
            }

            return dtSeconds;
        }

        /// <summary>
        /// Advances the multi-stage sleep state machine by <paramref name="delta60"/> (60Hz-normalized ticks)
        /// and returns the <see cref="SpriteTile"/> to render, or <c>null</c> once Kirby returns to <see cref="SleepState.Awake"/>.
        /// </summary>
        internal static SpriteTile AdvanceSleepState(
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

        /// <summary>
        /// Checks whether the mouse pointer is touching Kirby's 24x24 sprite body on the taskbar
        /// (Kirby only guards when on the ground, never mid-jump).
        /// </summary>
        internal static bool IsMouseNearKirby(Point mouse, double kirbyX, int workAreaBottom, int scale, bool isJumping) {
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
        internal static void StepJumpPhysics(ref bool isJumping, ref double kirbyX, ref double jumpY, ref double jumpVelY,
            double delta60, int scale = 2) {
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
        internal static int GetJumpFrameIndex(double jumpVelY) {
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

        /// <summary>
        /// Generates the next random enemy spawn delay in seconds within <c>[minSeconds, maxSeconds]</c>.
        /// </summary>
        internal static double NextEnemySpawnInterval(Random rng, double minSeconds, double maxSeconds) {
            if (maxSeconds <= minSeconds || rng == null) {
                return minSeconds;
            }

            return minSeconds + rng.NextDouble() * (maxSeconds - minSeconds);
        }

        /// <summary>
        /// Advances an active enemy horizontally from right to left and updates its walk animation timer.
        /// Returns <c>true</c> while the enemy remains on screen (<c>enemyX &gt;= despawnLeftX</c>),
        /// or <c>false</c> once it has walked completely past the left edge and should despawn.
        /// </summary>
        internal static bool StepEnemyPhysics(
            ref double enemyX,
            ref double enemyAnimTime,
            EnemyDef enemyDef,
            double delta60,
            int scale,
            int despawnLeftX) {
            if (enemyDef == null || enemyDef.Walk == null || !enemyDef.Walk.HasFrames()) {
                return false;
            }

            enemyX -= enemyDef.WalkSpeed * scale * delta60;
            enemyAnimTime += enemyDef.Walk.Speed * delta60;
            return enemyX >= despawnLeftX;
        }

        /// <summary>
        /// Pulls an inhaled enemy rapidly toward Kirby's mouth (<paramref name="kirbyX"/>).
        /// Returns <c>true</c> while the enemy is still flying toward Kirby, or <c>false</c>
        /// once the enemy reaches Kirby (<c>enemyX &lt;= kirbyX + INHALE_TOUCH_DISTANCE * scale</c>).
        /// </summary>
        internal static bool StepInhaledEnemyPhysics(
            ref double enemyX,
            ref double enemyAnimTime,
            EnemyDef enemyDef,
            double kirbyX,
            double delta60,
            int scale) {
            if (enemyDef == null) {
                return false;
            }

            AnimClip activeClip = enemyDef.Inhaled != null && enemyDef.Inhaled.HasFrames()
                ? enemyDef.Inhaled
                : enemyDef.Walk;

            if (activeClip == null || !activeClip.HasFrames()) {
                return false;
            }

            enemyX -= INHALE_PULL_SPEED * scale * delta60;
            enemyAnimTime += activeClip.Speed * delta60;
            return enemyX > kirbyX + INHALE_TOUCH_DISTANCE * scale;
        }

        /// <summary>
        /// Advances a hit enemy's 24-frame knockback sequence: pushes the enemy backward (to the right)
        /// for the first <see cref="HIT_PUSHBACK_FRAMES"/> frames by the same distance as Kirby, holds the hit pose
        /// (<see cref="EnemyDef.Inhaled"/>) until <see cref="ENEMY_HIT_FRAMES"/> (24 frames), and then returns
        /// <c>false</c> so the enemy disappears.
        /// </summary>
        internal static bool StepHitEnemyPhysics(
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

        /// <summary>
        /// Checks whether Kirby can begin inhaling an approaching enemy: Kirby must be awake, on the ground,
        /// not guarding, in Normal form with an <see cref="FormDef.Inhale"/> clip, and the enemy must be in front
        /// of Kirby within <see cref="INHALE_RANGE"/>.
        /// </summary>
        internal static bool CanStartInhale(
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
        internal static SpriteTile AdvanceSwallowState(
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
        internal static SpriteTile AdvanceSwallowState(
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

        /// <summary>
        /// Checks whether Kirby is in contact with an active enemy and should take damage:
        /// Kirby must be awake, on the ground, not guarding, not currently inhaling/swallowing or already hit,
        /// and within <see cref="ENEMY_CONTACT_DISTANCE"/> of the enemy.
        /// </summary>
        internal static bool CanTakeEnemyHit(
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
        internal static bool TryTriggerEnemyHit(
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
        /// Advances Normal Kirby's 2-frame enemy-hit animation (<see cref="FormDef.Hit"/>) by <paramref name="delta60"/> ticks,
        /// applying backward recoil to <paramref name="kirbyX"/> during the first <see cref="HIT_PUSHBACK_FRAMES"/> frames,
        /// and returns the <see cref="SpriteTile"/> to render, or <c>null</c> once the hit sequence completes.
        /// </summary>
        internal static SpriteTile AdvanceHitState(
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

        private void StepHitEnemyOnContact(double delta60, Rectangle workArea) {
            _enemyAnimTime = 0.0;

            if (StepHitEnemyPhysics(ref _enemyX, ref _enemyHitAnimTime, _activeEnemy, delta60, _settings.Scale)) {
                RenderActiveEnemy(workArea, true);
            }
        }

        /// <summary>
        /// Main ~60 Hz update loop driving input idle detection, fullscreen auto-hide,
        /// sleep/wake state transitions, cursor guard proximity, jump physics, and frame rendering.
        /// </summary>
        private void OnTick(object sender, EventArgs e) {
            long nowMs = _stopwatch.ElapsedMilliseconds;
            double dtSeconds = ClampDeltaTime((nowMs - _lastTickMs) / 1000.0);
            _lastTickMs = nowMs;

            Point mouse = Cursor.Position;

            if (_idleMonitor.PollActivity(mouse)) {
                NotifyUserActivity();
            }
            else if (_settings.IdleSleepSeconds > 0 && _sleepState == SleepState.Awake && !_isJumping &&
                     _swallowState == SwallowState.None && !_isHit && _sleepFrames != null &&
                     _sleepFrames.Length >= 8) {
                if (_idleMonitor.GetIdleSeconds(nowMs) >= _settings.IdleSleepSeconds) {
                    bool hasStandFrames = _currentForm != null && _currentForm.Stand != null &&
                                          _currentForm.Stand.HasFrames(2);
                    _sleepState = hasStandFrames ? SleepState.Halting : SleepState.FallingAsleep;
                    _sleepAnimTime = 0.0;
                }
            }

            if (_envMonitor.StepTick(_stage) && _settings.OverlayEnabled && !_envMonitor.IsFullscreenCached &&
                _stage != null) {
                _stage.RefreshTopmostOrder();
            }

            if (_stage == null) {
                return;
            }

            int scale = _settings.Scale;

            if (!_settings.OverlayEnabled || _envMonitor.IsFullscreenCached) {
                _stage.ClearAll(scale);
                return;
            }

            Rectangle workArea = _envMonitor.CachedWorkArea;
            int halfWidth = CanvasConstants.TILE_SIZE / 2 * scale;
            double delta60 = dtSeconds * 60.0;

            UpdateEnemy(dtSeconds, delta60, workArea, halfWidth);

            // Advance the multi-stage sleep state machine when Kirby is not in his normal Awake state.
            SpriteTile frameToRender =
                AdvanceSleepState(ref _sleepState, ref _sleepAnimTime, delta60, _currentForm, _sleepFrames);

            if (_sleepState == SleepState.Awake) {
                if (_currentForm == null || _currentForm.Walk == null || !_currentForm.Walk.HasFrames()) {
                    return;
                }

                // Check if mouse pointer is close to Kirby's body on the taskbar (only guards when on the ground).
                bool isMouseNear = IsMouseNearKirby(mouse, _kirbyX, workArea.Bottom, scale, _isJumping);
                bool isGuarding = !_isJumping && isMouseNear && _currentForm.Guard != null &&
                                  _currentForm.Guard.HasFrames();

                if (_settings.CombatEnabled &&
                    TryTriggerEnemyHit(_forms, ref _currentForm, _sleepState, _isJumping, _swallowState, ref _isHit,
                        ref _hitAnimTime, ref _activeEnemy, ref _isEnemyHit, ref _enemyHitAnimTime, _kirbyX, _enemyX,
                        scale, isGuarding)) {
                    _animTime = 0.0;
                    StepHitEnemyOnContact(delta60, workArea);
                }

                if (_isHit) {
                    _activeVfx = null;
                    frameToRender = AdvanceHitState(ref _isHit, ref _hitAnimTime, ref _kirbyX, delta60, _currentForm,
                        scale);
                }

                bool startedInhaleThisTick = false;

                if (!_isHit && _swallowState == SwallowState.None &&
                    CanStartInhale(_currentForm, _sleepState, _isJumping, _swallowState, _activeEnemy, _kirbyX, _enemyX,
                        scale, isGuarding, _isEnemyHit)) {
                    _swallowState = SwallowState.Inhaling;
                    _swallowAnimTime = 0.0;
                    _inhaleVfx.Reset();
                    _activeVfx = _inhaleVfx;
                    startedInhaleThisTick = true;
                }

                if (!_isHit && _swallowState == SwallowState.Inhaling) {
                    if (!_settings.CombatEnabled || _activeEnemy == null || _isEnemyHit || _enemyX < _kirbyX ||
                        _currentForm.Inhale == null || !_currentForm.Inhale.HasFrames()) {
                        _swallowState = SwallowState.None;
                        _swallowAnimTime = 0.0;
                        _activeVfx = null;
                    }
                    else {
                        _swallowAnimTime += _currentForm.Inhale.Speed * delta60;
                        frameToRender = _currentForm.Inhale.GetFrame(_swallowAnimTime);

                        if (!startedInhaleThisTick) {
                            _inhaleVfx.Step(delta60);
                        }

                        _activeVfx = _inhaleVfx;
                    }
                }
                else if (_swallowState == SwallowState.Mouthful || _swallowState == SwallowState.Swallowing) {
                    _activeVfx = null;
                    FormDef prevForm = _currentForm;
                    frameToRender = AdvanceSwallowState(ref _swallowState, ref _swallowAnimTime, delta60,
                        ref _currentForm, _forms, ref _swallowedEnemy);

                    if (_currentForm != prevForm) {
                        _animTime = 0.0;
                    }
                }

                if (_swallowState == SwallowState.None && !_isHit) {
                    _activeVfx = null;

                    bool useJump = _isJumping && _currentForm.Jump != null && _currentForm.Jump.HasFrames(5);
                    bool useGuard = !useJump && isGuarding;
                    AnimClip activeClip = useJump ? _currentForm.Jump : useGuard ? _currentForm.Guard : _currentForm.Walk;

                    if (_isJumping) {
                        StepJumpPhysics(ref _isJumping, ref _kirbyX, ref _jumpY, ref _jumpVelY, delta60, scale);
                    }
                    else if (!isMouseNear) {
                        _kirbyX += WALK_SPEED * scale * delta60;
                        _animTime += activeClip.Speed * delta60;
                    }
                    else {
                        // Match SNES Kirby Super Star guard flash / shield shimmer speed (4 frames per state @ 60Hz = 15 FPS).
                        _animTime += activeClip.Speed * delta60;
                    }

                    // Wrap Kirby seamlessly from the right edge of the monitor back to the left edge.
                    if (_kirbyX > workArea.Right + halfWidth) {
                        _kirbyX = workArea.Left - halfWidth;
                    }

                    if (_settings.CombatEnabled &&
                        TryTriggerEnemyHit(_forms, ref _currentForm, _sleepState, _isJumping, _swallowState,
                            ref _isHit, ref _hitAnimTime, ref _activeEnemy, ref _isEnemyHit, ref _enemyHitAnimTime,
                            _kirbyX, _enemyX, scale, useGuard)) {
                        _animTime = 0.0;
                        StepHitEnemyOnContact(delta60, workArea);
                        frameToRender = AdvanceHitState(ref _isHit, ref _hitAnimTime, ref _kirbyX, delta60,
                            _currentForm, scale);
                    }
                    else if (useJump) {
                        frameToRender = activeClip.Frames[GetJumpFrameIndex(_jumpVelY)];
                    }
                    else {
                        frameToRender = AnimClip.GetFrame(activeClip, _animTime);
                    }
                }
            }
            else {
                _activeVfx = null;
            }

            if (frameToRender == null) {
                return;
            }

            _stage.KirbyLayer.RenderAtTaskbar(frameToRender, scale, workArea, _kirbyX, _jumpY);
            UpdateVfx(workArea, scale);
        }

        /// <summary>
        /// Renders the currently active visual effect (<see cref="_activeVfx"/>, such as Kirby's inhale wind
        /// or copy ability attack particles) onto <see cref="OverlayStage.VfxLayer"/>, or clears the layer when inactive.
        /// </summary>
        private void UpdateVfx(Rectangle workArea, int scale) {
            if (_stage == null || _stage.VfxLayer == null) {
                return;
            }

            SpriteTile vfxTile = _activeVfx != null ? _activeVfx.Render() : null;

            if (vfxTile == null) {
                _stage.VfxLayer.ClearOverlay(scale);
                return;
            }

            double vfxWorldX = _kirbyX + _activeVfx.OffsetX * scale;
            _stage.VfxLayer.RenderAtTaskbar(vfxTile, scale, workArea, vfxWorldX, _activeVfx.OffsetY);
        }

        /// <summary>
        /// Spawns at most one enemy at a random interval on the right side of the screen,
        /// walks it left across the taskbar (even while Kirby is napping), and despawns it off the left edge.
        /// </summary>
        private void UpdateEnemy(double dtSeconds, double delta60, Rectangle workArea, int halfWidth) {
            if (_stage == null || _stage.EnemyLayer == null) {
                return;
            }

            int scale = _settings.Scale;

            if (!_settings.CombatEnabled || _enemies.Count == 0) {
                _stage.EnemyLayer.ClearOverlay(scale);
                return;
            }

            if (_activeEnemy == null) {
                _enemySpawnTimerSeconds -= dtSeconds;

                if (_enemySpawnTimerSeconds <= 0.0) {
                    _activeEnemy = _enemies[_rng.Next(_enemies.Count)];
                    _enemyX = workArea.Right + halfWidth;
                    _enemyAnimTime = 0.0;
                    _isEnemyHit = false;
                    _enemyHitAnimTime = 0.0;
                }
                else {
                    return;
                }
            }

            int despawnLeftX = workArea.Left - halfWidth;
            bool isBeingInhaled = _swallowState == SwallowState.Inhaling;
            bool remainActive = _isEnemyHit
                ? StepHitEnemyPhysics(ref _enemyX, ref _enemyHitAnimTime, _activeEnemy, delta60, scale)
                : isBeingInhaled
                    ? StepInhaledEnemyPhysics(ref _enemyX, ref _enemyAnimTime, _activeEnemy, _kirbyX, delta60, scale)
                    : StepEnemyPhysics(ref _enemyX, ref _enemyAnimTime, _activeEnemy, delta60, scale, despawnLeftX);

            if (!remainActive) {
                if (isBeingInhaled && !_isEnemyHit) {
                    _swallowedEnemy = _activeEnemy;
                    _swallowState = SwallowState.Mouthful;
                    _swallowAnimTime = 0.0;
                    _activeVfx = null;
                }

                _activeEnemy = null;
                _isEnemyHit = false;
                _enemyHitAnimTime = 0.0;
                _stage.EnemyLayer.ClearOverlay(scale);

                _enemySpawnTimerSeconds = NextEnemySpawnInterval(_rng, ENEMY_SPAWN_MIN_SECONDS, ENEMY_SPAWN_MAX_SECONDS);
                return;
            }

            RenderActiveEnemy(workArea, isBeingInhaled || _isEnemyHit);
        }

        private void RenderActiveEnemy(Rectangle workArea, bool useHitOrInhaledPose) {
            if (_activeEnemy == null || _stage == null || _stage.EnemyLayer == null) {
                return;
            }

            AnimClip enemyClip = useHitOrInhaledPose && _activeEnemy.Inhaled != null && _activeEnemy.Inhaled.HasFrames()
                ? _activeEnemy.Inhaled
                : _activeEnemy.Walk;
            SpriteTile enemyTile = AnimClip.GetFrame(enemyClip, _enemyAnimTime);

            _stage.EnemyLayer.RenderAtTaskbar(enemyTile, _settings.Scale, workArea, _enemyX, 0.0);
        }

        /// <summary>
        /// Triggers a jump on Spacebar or toggles the matching copy ability (pressing the active ability key
        /// a second time reverts Kirby back to Normal form).
        /// </summary>
        private void HandleKeySwitch(string keyName) {
            if (keyName == null || _swallowState != SwallowState.None || _isHit) {
                return;
            }

            if (keyName == " ") {
                if (!_isJumping) {
                    _isJumping = true;
                    _jumpY = 0.0;
                    _jumpVelY = JUMP_INIT_VEL;
                }

                return;
            }

            FormDef nextForm = KeyBindings.ResolveNextForm(_keyToForm, _currentForm, keyName);

            if (nextForm != null && nextForm != _currentForm) {
                _currentForm = nextForm;
            }
        }

        private void OnGlobalKeyDown(int vkCode, bool isInitialKeyDown) {
            bool wasSleeping = _sleepState != SleepState.Awake;
            NotifyUserActivity();

            if (isInitialKeyDown && !_settings.IsPaused && !wasSleeping) {
                string keyName = KeyBindings.VkCodeToName(vkCode);

                if (keyName != null) {
                    HandleKeySwitch(keyName);
                }
            }
        }

        private void OnToggleOverlay() {
            _settings.OverlayEnabled = !_settings.OverlayEnabled;
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
        }

        private void OnToggleCombat() {
            _settings.CombatEnabled = !_settings.CombatEnabled;

            if (!_settings.CombatEnabled) {
                _activeEnemy = null;
                _swallowedEnemy = null;
                _isEnemyHit = false;
                _enemyHitAnimTime = 0.0;
                _swallowState = SwallowState.None;
                _swallowAnimTime = 0.0;
                _isHit = false;
                _hitAnimTime = 0.0;
                _activeVfx = null;

                if (_stage != null) {
                    if (_stage.EnemyLayer != null) {
                        _stage.EnemyLayer.ClearOverlay(_settings.Scale);
                    }

                    if (_stage.VfxLayer != null) {
                        _stage.VfxLayer.ClearOverlay(_settings.Scale);
                    }
                }

                _enemySpawnTimerSeconds = NextEnemySpawnInterval(_rng, ENEMY_SPAWN_MIN_SECONDS, ENEMY_SPAWN_MAX_SECONDS);
            }

            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
            RegistrySettingsService.SaveSettings(_settings);
        }

        private void OnTogglePause() {
            _settings.IsPaused = !_settings.IsPaused;
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
        }

        private void OnToggleStartup() {
            bool nextState = !RegistrySettingsService.IsStartupEnabled();
            RegistrySettingsService.SetStartup(nextState);
            _trayMenu.SyncState(_settings, RegistrySettingsService.IsStartupEnabled());
        }

        private void OnExit() {
            if (_animTimer != null) {
                _animTimer.Stop();
            }

            if (_stage != null) {
                _stage.Dispose();
            }

            if (_keyboardHook != null) {
                _keyboardHook.Dispose();
            }

            if (_trayMenu != null) {
                _trayMenu.Dispose();
            }

            Application.Exit();
        }

    }

}
