using System;
using System.Collections.Generic;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Encapsulates Kirby's position, active <see cref="FormDef"/>, walk/guard animation timer,
    /// jump physics (<see cref="JumpPhysicsState"/>), hit recoil (<see cref="HitRecoilState"/>),
    /// idle sleep controller (<see cref="SleepController"/>), swallow controller (<see cref="SwallowController"/>),
    /// and foreground visual effect (<see cref="IVfxEffect"/>).
    /// </summary>
    public sealed class KirbyActor {

        private readonly IList<FormDef> _forms;
        private readonly IDictionary<string, FormDef> _keyToForm;
        private readonly SleepController _sleep;
        private readonly SwallowController _swallow;
        private readonly InhaleParticleEffect _inhaleVfx;

        public FormDef CurrentForm;
        public double X;
        public double AnimTime;
        public JumpPhysicsState Jump;
        public HitRecoilState HitRecoil;
        public IVfxEffect ActiveVfx;

        public IList<FormDef> Forms {
            get { return _forms; }
        }

        public SleepController Sleep {
            get { return _sleep; }
        }

        public SwallowController Swallow {
            get { return _swallow; }
        }

        public InhaleParticleEffect InhaleVfx {
            get { return _inhaleVfx; }
        }

        public KirbyActor(
            IList<FormDef> forms,
            IDictionary<string, FormDef> keyToForm,
            FormDef initialForm,
            SpriteTile[] sleepFrames,
            Random rng,
            int initialScale) {
            _forms = forms;
            _keyToForm = keyToForm;
            CurrentForm = initialForm;
            _sleep = new SleepController(sleepFrames);
            _swallow = new SwallowController();
            _inhaleVfx = new InhaleParticleEffect(rng);
            X = -32 * initialScale;
        }

        /// <summary>
        /// Triggers a jump on Spacebar or toggles the matching copy ability (pressing the active ability key
        /// a second time reverts Kirby back to Normal form).
        /// </summary>
        public void HandleKeySwitch(string keyName) {
            if (keyName == null || _swallow.State != SwallowState.None || HitRecoil.IsHit) {
                return;
            }

            if (keyName == " ") {
                Jump.StartJump();
                return;
            }

            FormDef nextForm = KeyBindings.ResolveNextForm(_keyToForm, CurrentForm, keyName);

            if (nextForm != null && nextForm != CurrentForm) {
                CurrentForm = nextForm;
            }
        }

        /// <summary>
        /// Clears any in-progress inhale, swallow, hit recoil, or foreground VFX state when combat is disabled.
        /// </summary>
        public void ResetCombatState() {
            _swallow.Reset();
            HitRecoil.Reset();
            ActiveVfx = null;
        }

    }

}