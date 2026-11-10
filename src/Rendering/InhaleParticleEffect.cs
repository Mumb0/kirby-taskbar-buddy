using System;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// State of a single black-and-white air particle during Normal Kirby's inhale animation,
    /// tracked in unscaled 1x pixel offsets (<see cref="Dx"/>, <see cref="Dy"/>) relative to Kirby's open mouth.
    /// </summary>
    public struct InhaleParticle {

        public double Dx;
        public double Dy;
        public bool BlackOnTop;

    }

    /// <summary>
    /// Procedural inhale wind particle system that spawns, steps, and rasterizes 4 black-and-white air particles
    /// flying into Normal Kirby's mouth using double-buffered <see cref="SpriteTile"/> instances.
    /// </summary>
    public sealed class InhaleParticleEffect : IVfxEffect {

        public const double OFFSET_X = 36.0;
        public const double OFFSET_Y = 0.0;
        public const int PARTICLE_COUNT = 4;
        public const double PARTICLE_SPEED = 2.4;
        public const double PARTICLE_MIN_DX = 12.0;
        public const double PARTICLE_MAX_DX = 52.0;
        public const double PARTICLE_MIN_DY = -20.0;
        public const double PARTICLE_MAX_DY = 20.0;

        private readonly Random _rng;
        private readonly InhaleParticle[] _particles = new InhaleParticle[PARTICLE_COUNT];
        private readonly int[] _scratch = new int[CanvasConstants.TILE_SIZE * CanvasConstants.TILE_SIZE];
        private readonly SpriteTile _tileA = new SpriteTile
        {
            PremulPixels = new int[CanvasConstants.TILE_SIZE * CanvasConstants.TILE_SIZE]
        };
        private readonly SpriteTile _tileB = new SpriteTile
        {
            PremulPixels = new int[CanvasConstants.TILE_SIZE * CanvasConstants.TILE_SIZE]
        };
        private bool _useTileB;

        public double OffsetX {
            get { return OFFSET_X; }
        }

        public double OffsetY {
            get { return OFFSET_Y; }
        }

        public InhaleParticle[] Particles {
            get { return _particles; }
        }

        public InhaleParticleEffect(Random rng) {
            _rng = rng ?? new Random();
            Reset();
        }

        /// <summary>
        /// Spawns all initial (<see cref="PARTICLE_COUNT"/> = 4) inhale wind particles at random positions
        /// in the rectangular area in front of Kirby when the inhale animation starts.
        /// </summary>
        public void Reset() {
            InitParticles(_particles, _rng);
        }

        /// <summary>
        /// Moves each individual inhale particle straight toward Kirby's mouth (<c>Dx = 0, Dy = 0</c>)
        /// and respawns particles that reach the mouth.
        /// </summary>
        public bool Step(double delta60) {
            StepParticles(_particles, delta60, _rng);
            return true;
        }

        /// <summary>
        /// Rasterizes the active particles into the next double-buffered <see cref="SpriteTile"/>.
        /// </summary>
        public SpriteTile Render() {
            _useTileB = !_useTileB;
            SpriteTile target = _useTileB ? _tileB : _tileA;
            return RenderParticlesTile(_particles, _scratch, target);
        }

        /// <summary>
        /// Spawns a single inhale wind particle at a random position within the rectangular inhale area
        /// in front of Kirby's mouth (<c>[PARTICLE_MIN_DX, PARTICLE_MAX_DX] x [PARTICLE_MIN_DY, PARTICLE_MAX_DY]</c>).
        /// </summary>
        public static InhaleParticle SpawnParticle(Random rng) {
            double rx = rng != null ? rng.NextDouble() : 0.5;
            double ry = rng != null ? rng.NextDouble() : 0.5;
            double dx = PARTICLE_MIN_DX + rx * (PARTICLE_MAX_DX - PARTICLE_MIN_DX);
            double dy = PARTICLE_MIN_DY + ry * (PARTICLE_MAX_DY - PARTICLE_MIN_DY);
            return new InhaleParticle
            {
                Dx = dx,
                Dy = dy,
                BlackOnTop = dy <= 0.0
            };
        }

        /// <summary>
        /// Populates <paramref name="particles"/> with initial random positions in front of Kirby's mouth.
        /// </summary>
        public static void InitParticles(InhaleParticle[] particles, Random rng) {
            if (particles == null) {
                return;
            }

            for (int i = 0; i < particles.Length; i++) {
                particles[i] = SpawnParticle(rng);
            }
        }

        /// <summary>
        /// Moves each individual inhale particle straight toward Kirby's mouth (<c>Dx = 0, Dy = 0</c>) by
        /// <c>PARTICLE_SPEED * delta60</c>. As soon as a particle reaches the mouth and despawns,
        /// a new particle is immediately spawned at a random position in the rectangular inhale area.
        /// </summary>
        public static void StepParticles(InhaleParticle[] particles, double delta60, Random rng) {
            if (particles == null || delta60 <= 0.0) {
                return;
            }

            double step = PARTICLE_SPEED * delta60;

            for (int i = 0; i < particles.Length; i++) {
                double dx = particles[i].Dx;
                double dy = particles[i].Dy;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist <= step || dist <= 1.0) {
                    particles[i] = SpawnParticle(rng);
                }
                else {
                    particles[i].Dx = dx - dx / dist * step;
                    particles[i].Dy = dy - dy / dist * step;
                }
            }
        }

        /// <summary>
        /// Rasterizes the currently active inhale air particles (<paramref name="particles"/>) onto the 80x80
        /// <paramref name="scratch"/> buffer and crops them into a <see cref="SpriteTile"/>.
        /// Each particle is a 2x4 block (a 2x2 black block stacked with a 2x2 white block) offset from Kirby's mouth.
        /// </summary>
        public static SpriteTile RenderParticlesTile(
            InhaleParticle[] particles,
            int[] scratch,
            SpriteTile targetTile = null) {
            if (particles == null || particles.Length == 0 || scratch == null) {
                return null;
            }

            Array.Clear(scratch, 0, scratch.Length);

            int black = SpriteLoader.MakePremulArgb(255, 0, 0, 0);
            int white = SpriteLoader.MakePremulArgb(255, 255, 255, 255);

            for (int i = 0; i < particles.Length; i++) {
                int px = CanvasConstants.INHALE_MOUTH_X + (int)Math.Round(particles[i].Dx);
                int py = CanvasConstants.INHALE_MOUTH_Y + (int)Math.Round(particles[i].Dy) - 2;
                int topColor = particles[i].BlackOnTop ? black : white;
                int botColor = particles[i].BlackOnTop ? white : black;

                Write2x2Block(scratch, px, py, topColor);
                Write2x2Block(scratch, px, py + 2, botColor);
            }

            return targetTile != null
                ? SpriteLoader.BuildSpriteTileInto(scratch, targetTile)
                : SpriteLoader.BuildSpriteTile(scratch);
        }

        private static void Write2x2Block(int[] scratch, int x, int y, int color) {
            for (int dy = 0; dy < 2; dy++) {
                int ty = y + dy;

                if (ty < 0 || ty >= CanvasConstants.TILE_SIZE) {
                    continue;
                }

                int row = ty * CanvasConstants.TILE_SIZE;

                for (int dx = 0; dx < 2; dx++) {
                    int tx = x + dx;

                    if (tx >= 0 && tx < CanvasConstants.TILE_SIZE) {
                        scratch[row + tx] = color;
                    }
                }
            }
        }

    }

}
