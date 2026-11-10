using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class FormDefinitionsTests
    {
        [Fact]
        public void PopulateForms_RegistersNormalAndAll19CopyAbilities()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;

            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            Assert.Equal(20, forms.Count);
            Assert.Equal(20, keyToForm.Count);
            Assert.NotNull(normalForm);
            Assert.Equal("normal", normalForm.Name);
            Assert.Equal("0", normalForm.Key);
            Assert.Same(normalForm, keyToForm["0"]);
        }

        [Fact]
        public void PopulateForms_AllAbilitiesHaveRequiredClipsAndValidSequences()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;

            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            HashSet<string> seenNames = new HashSet<string>();
            HashSet<string> seenKeys = new HashSet<string>();

            foreach (FormDef form in forms)
            {
                Assert.False(string.IsNullOrEmpty(form.Name));
                Assert.False(string.IsNullOrEmpty(form.Key));
                Assert.False(string.IsNullOrEmpty(form.FileName));

                Assert.True(seenNames.Add(form.Name), "Duplicate form name: " + form.Name);
                Assert.True(seenKeys.Add(form.Key), "Duplicate form key: " + form.Key);
                Assert.Same(form, keyToForm[form.Key]);

                AssertClipDefinitionValid(form.Name, "Walk", form.Walk, minRects: 4);
                AssertClipDefinitionValid(form.Name, "Guard", form.Guard, minRects: 2);
                AssertClipDefinitionValid(form.Name, "Jump", form.Jump, minRects: 5);
                AssertClipDefinitionValid(form.Name, "Stand", form.Stand, minRects: 2);

                if (form.Name == "normal")
                {
                    AssertClipDefinitionValid(form.Name, "Inhale", form.Inhale, minRects: 2);
                    Assert.False(form.Inhale.Loop);
                    AssertClipDefinitionValid(form.Name, "Mouthful", form.Mouthful, minRects: 1);
                    Assert.False(form.Mouthful.Loop);
                    AssertClipDefinitionValid(form.Name, "Swallow", form.Swallow, minRects: 5);
                    Assert.False(form.Swallow.Loop);
                    Assert.Equal(3, form.Swallow.Sequence.Length);
                    AssertClipDefinitionValid(form.Name, "SwallowAbility", form.SwallowAbility, minRects: 5);
                    Assert.False(form.SwallowAbility.Loop);
                    Assert.Equal(37, form.SwallowAbility.Sequence.Length);
                    AssertClipDefinitionValid(form.Name, "Hit", form.Hit, minRects: 3);
                    Assert.False(form.Hit.Loop);
                    Assert.Equal(3, form.Hit.SourceRects.Length);
                    Assert.Equal(new Rectangle(97, 432, 24, 24), form.Hit.SourceRects[0]);
                    Assert.Equal(new Rectangle(126, 432, 24, 24), form.Hit.SourceRects[1]);
                    Assert.Equal(new Rectangle(113, 55, 24, 23), form.Hit.SourceRects[2]);
                    Assert.Equal(34, form.Hit.Sequence.Length);
                }
            }
        }

        [Fact]
        public void SleepAndStandSequences_AllIndicesAreInBounds()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            Assert.Equal(8, FormDefinitions.SleepCoords.Length);

            foreach (int idx in FormDefinitions.FallAsleepSeq)
            {
                Assert.InRange(idx, 0, FormDefinitions.SleepCoords.Length - 1);
            }
            foreach (int idx in FormDefinitions.SleepLoopSeq)
            {
                Assert.InRange(idx, 0, FormDefinitions.SleepCoords.Length - 1);
            }
            foreach (int idx in FormDefinitions.WakeUpSeq)
            {
                Assert.InRange(idx, 0, FormDefinitions.SleepCoords.Length - 1);
            }

            foreach (FormDef form in forms)
            {
                int standCount = form.Stand.SourceRects.Length;
                foreach (int idx in FormDefinitions.HaltStandSeq)
                {
                    Assert.InRange(idx, 0, standCount - 1);
                }
                foreach (int idx in FormDefinitions.PostWakeStandSeq)
                {
                    Assert.InRange(idx, 0, standCount - 1);
                }
            }
        }

        [Fact]
        public void PopulateEnemies_RegistersWaddleDeeAndWaddleDooWithValidWalkClipsAndSpeeds()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            List<EnemyDef> enemies = new List<EnemyDef>();
            FormDefinitions.PopulateEnemies(enemies);

            Assert.Equal(2, enemies.Count);
            Assert.Equal("waddle-dee", enemies[0].Name);
            Assert.Null(enemies[0].CopyAbility);
            Assert.Equal("waddle-doo", enemies[1].Name);
            Assert.Equal("beam", enemies[1].CopyAbility);

            foreach (EnemyDef enemy in enemies)
            {
                Assert.Equal("enemy-waddle-dee.png", enemy.FileName);
                Assert.True(enemy.WalkSpeed > 0.0 && enemy.WalkSpeed < KirbyTaskbarBuddyContext.WALK_SPEED);
                AssertClipDefinitionValid(enemy.Name, "Walk", enemy.Walk, minRects: 2);
                Assert.Equal(2, enemy.Walk.SourceRects.Length);
                AssertClipDefinitionValid(enemy.Name, "Inhaled", enemy.Inhaled, minRects: 1);

                if (!string.IsNullOrEmpty(enemy.CopyAbility))
                {
                    Assert.True(forms.Exists(f => f.Name == enemy.CopyAbility));
                }
            }
        }

        [Fact]
        public void BuilderHelpers_RowAndStrip_ProduceExpectedRectangles()
        {
            Rectangle[] row = FormDefinitions.Row(10, 24, 32, 5, 35, 65);
            Assert.Equal(3, row.Length);
            Assert.Equal(new Rectangle(5, 10, 24, 32), row[0]);
            Assert.Equal(new Rectangle(35, 10, 24, 32), row[1]);
            Assert.Equal(new Rectangle(65, 10, 24, 32), row[2]);

            Rectangle[] strip = FormDefinitions.Strip(6, 95, 26, 24, 3, 30, 5);
            Assert.Equal(3, strip.Length);
            Assert.Equal(new Rectangle(6, 95, 26, 24), strip[0]);
            Assert.Equal(new Rectangle(36, 100, 26, 24), strip[1]);
            Assert.Equal(new Rectangle(66, 105, 26, 24), strip[2]);
        }

        private static void AssertClipDefinitionValid(string formName, string clipName, AnimClip clip, int minRects)
        {
            Assert.NotNull(clip);
            Assert.NotNull(clip.SourceRects);
            Assert.True(
                clip.SourceRects.Length >= minRects,
                string.Format("{0}.{1} expected at least {2} rects, got {3}", formName, clipName, minRects, clip.SourceRects.Length));

            foreach (Rectangle r in clip.SourceRects)
            {
                Assert.True(r.X >= 0 && r.Y >= 0, string.Format("{0}.{1} has negative coordinates: {2}", formName, clipName, r));
                Assert.True(r.Width > 0 && r.Height > 0, string.Format("{0}.{1} has non-positive size: {2}", formName, clipName, r));
                Assert.True(
                    r.Width <= NativeMethods.TILE_SIZE && r.Height <= NativeMethods.TILE_SIZE,
                    string.Format("{0}.{1} rect {2} exceeds TILE_SIZE ({3})", formName, clipName, r, NativeMethods.TILE_SIZE));
            }

            if (clip.Sequence != null)
            {
                Assert.NotEmpty(clip.Sequence);
                foreach (int seqIdx in clip.Sequence)
                {
                    Assert.InRange(seqIdx, 0, clip.SourceRects.Length - 1);
                }
            }
        }
    }
}
