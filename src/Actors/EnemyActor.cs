namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Represents the currently active enemy walking across the taskbar, being pulled into Kirby's inhale,
    /// or recoiling after a collision with Kirby.
    /// </summary>
    public sealed class EnemyActor {

        public EnemyDef Def;
        public double X;
        public double AnimTime;
        public HitRecoilState HitRecoil;

        public bool IsActive {
            get { return Def != null; }
        }

        public bool IsHit {
            get { return HitRecoil.IsHit; }
        }

        public void Spawn(EnemyDef def, double startX) {
            Def = def;
            X = startX;
            AnimTime = 0.0;
            HitRecoil.Reset();
        }

        public void Clear() {
            Def = null;
            AnimTime = 0.0;
            HitRecoil.Reset();
        }

        public bool Step(double delta60, int scale, int despawnLeftX, bool isBeingInhaled, double kirbyX) {
            if (Def == null) {
                return false;
            }

            if (HitRecoil.IsHit) {
                return HitRecoil.StepEnemy(ref X, Def, delta60, scale);
            }

            if (isBeingInhaled) {
                return StepInhaledEnemyPhysics(ref X, ref AnimTime, Def, kirbyX, delta60, scale);
            }

            return StepEnemyPhysics(ref X, ref AnimTime, Def, delta60, scale, despawnLeftX);
        }

        public SpriteTile GetCurrentFrame(bool useHitOrInhaledPose) {
            if (Def == null) {
                return null;
            }

            AnimClip enemyClip = useHitOrInhaledPose && Def.Inhaled != null && Def.Inhaled.HasFrames()
                ? Def.Inhaled
                : Def.Walk;
            return AnimClip.GetFrame(enemyClip, AnimTime);
        }

        /// <summary>
        /// Advances an active enemy horizontally from right to left and updates its walk animation timer.
        /// Returns <c>true</c> while the enemy remains on screen (<c>enemyX &gt;= despawnLeftX</c>),
        /// or <c>false</c> once it has walked completely past the left edge and should despawn.
        /// </summary>
        public static bool StepEnemyPhysics(
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
        /// once the enemy reaches Kirby (<c>enemyX &lt;= kirbyX + SwallowController.INHALE_TOUCH_DISTANCE * scale</c>).
        /// </summary>
        public static bool StepInhaledEnemyPhysics(
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

            enemyX -= SwallowController.INHALE_PULL_SPEED * scale * delta60;
            enemyAnimTime += activeClip.Speed * delta60;
            return enemyX > kirbyX + SwallowController.INHALE_TOUCH_DISTANCE * scale;
        }

    }

}