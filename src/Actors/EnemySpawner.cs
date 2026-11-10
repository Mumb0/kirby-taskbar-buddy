using System;
using System.Collections.Generic;
using System.Drawing;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Manages random enemy spawn timing and owns the single <see cref="EnemyActor"/> instance on the taskbar.
    /// </summary>
    public sealed class EnemySpawner {

        public const double SPAWN_MIN_SECONDS = 5.0;
        public const double SPAWN_MAX_SECONDS = 10.0;

        private readonly IList<EnemyDef> _enemies;
        private readonly Random _rng;
        private readonly EnemyActor _actor = new EnemyActor();
        private double _spawnTimerSeconds;

        public EnemyActor Actor {
            get { return _actor; }
        }

        public bool HasEnemyCatalog {
            get { return _enemies != null && _enemies.Count > 0; }
        }

        public EnemySpawner(IList<EnemyDef> enemies, Random rng) {
            _enemies = enemies;
            _rng = rng ?? new Random();
            ResetTimer();
        }

        public void ResetTimer() {
            _spawnTimerSeconds = NextSpawnInterval(_rng, SPAWN_MIN_SECONDS, SPAWN_MAX_SECONDS);
        }

        public void ClearActiveEnemy() {
            _actor.Clear();
            ResetTimer();
        }

        /// <summary>
        /// Decrements the spawn timer when no enemy is active and spawns a random enemy at the right edge
        /// of <paramref name="workArea"/> once the timer elapses.
        /// </summary>
        public bool EnsureActiveOrTickTimer(double dtSeconds, Rectangle workArea, int halfWidth) {
            if (_actor.IsActive) {
                return true;
            }

            if (!HasEnemyCatalog) {
                return false;
            }

            _spawnTimerSeconds -= dtSeconds;

            if (_spawnTimerSeconds <= 0.0) {
                EnemyDef chosen = _enemies[_rng.Next(_enemies.Count)];
                _actor.Spawn(chosen, workArea.Right + halfWidth);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates the next random enemy spawn delay in seconds within <c>[minSeconds, maxSeconds]</c>.
        /// </summary>
        public static double NextSpawnInterval(Random rng, double minSeconds, double maxSeconds) {
            if (maxSeconds <= minSeconds || rng == null) {
                return minSeconds;
            }

            return minSeconds + rng.NextDouble() * (maxSeconds - minSeconds);
        }

    }

}