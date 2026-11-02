using System.Collections.Generic;
using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class KeyMappingAndSwitchingTests
    {
        [Fact]
        public void AllRegisteredForms_AreReachableViaVkCodeToName()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            HashSet<string> mappedKeys = new HashSet<string>();
            for (int vk = 0; vk <= 0xFF; vk++)
            {
                string keyName = NativeMethods.VkCodeToName(vk);
                if (keyName != null)
                {
                    mappedKeys.Add(keyName);
                }
            }

            foreach (FormDef form in forms)
            {
                Assert.True(
                    mappedKeys.Contains(form.Key),
                    string.Format("Form '{0}' with key '{1}' is not reachable from NativeMethods.VkCodeToName", form.Name, form.Key));
            }
        }

        [Fact]
        public void AllVkCodeMappings_ExceptSpacebar_ResolveToRegisteredForm()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            for (int vk = 0; vk <= 0xFF; vk++)
            {
                string keyName = NativeMethods.VkCodeToName(vk);
                if (keyName == null) continue;

                if (vk == 0x20)
                {
                    Assert.Equal(" ", keyName);
                }
                else
                {
                    Assert.True(
                        keyToForm.ContainsKey(keyName),
                        string.Format("VK 0x{0:X2} maps to '{1}', which is not registered in FormDefinitions", vk, keyName));
                }
            }

            // Both top-row '0' (0x30) and Numpad '0' (0x60) map to Normal Kirby ("0").
            Assert.Equal("0", NativeMethods.VkCodeToName(0x30));
            Assert.Equal("0", NativeMethods.VkCodeToName(0x60));

            // Unmapped keys and out-of-range VK codes return null without allocating strings.
            Assert.Null(NativeMethods.VkCodeToName(0x49)); // 'I'
            Assert.Null(NativeMethods.VkCodeToName(0x4B)); // 'K'
            Assert.Null(NativeMethods.VkCodeToName(0x5A)); // 'Z'
            Assert.Null(NativeMethods.VkCodeToName(0x1B)); // Escape
            Assert.Null(NativeMethods.VkCodeToName(-1));
        }

        [Fact]
        public void ResolveNextForm_SwitchesBetweenAbilities_AndTogglesBackToNormal()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            FormDef current = normalForm;

            // Pressing "0" while already Normal stays Normal.
            current = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, "0");
            Assert.Same(normalForm, current);

            // Pressing Sword ("w") switches to Sword.
            FormDef swordForm = keyToForm["w"];
            current = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, "w");
            Assert.Same(swordForm, current);

            // Pressing Sword ("w") a second time toggles back to Normal.
            current = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, "w");
            Assert.Same(normalForm, current);

            // Switching directly from Sword ("w") to Fire ("d") and then pressing "0" returns to Normal.
            FormDef fireForm = keyToForm["d"];
            current = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, "w");
            Assert.Same(swordForm, current);

            current = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, "d");
            Assert.Same(fireForm, current);

            current = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, "0");
            Assert.Same(normalForm, current);
        }

        [Fact]
        public void ResolveNextForm_EveryCopyAbilityTogglesOnAndOffCleanly()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            foreach (FormDef form in forms)
            {
                if (form == normalForm) continue;

                FormDef activated = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, normalForm, form.Key);
                Assert.Same(form, activated);

                FormDef reverted = KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, activated, form.Key);
                Assert.Same(normalForm, reverted);
            }
        }

        [Fact]
        public void ResolveNextForm_IgnoresUnmappedNullOrSpaceKeys()
        {
            List<FormDef> forms = new List<FormDef>();
            Dictionary<string, FormDef> keyToForm = new Dictionary<string, FormDef>();
            FormDef normalForm;
            FormDefinitions.PopulateForms(forms, keyToForm, out normalForm);

            FormDef current = keyToForm["m"]; // Mirror

            Assert.Same(current, KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, null));
            Assert.Same(current, KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, " "));
            Assert.Same(current, KirbyTaskbarBuddyContext.ResolveNextForm(keyToForm, current, "z"));
            Assert.Same(current, KirbyTaskbarBuddyContext.ResolveNextForm(null, current, "w"));
        }
    }
}
