using System.Collections.Generic;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Maps Win32 Virtual-Key codes to Kirby ability key strings and resolves copy ability transitions.
    /// </summary>
    public static class KeyBindings {

        /// <summary>
        /// Maps a Win32 Virtual-Key code to a Kirby ability key string using pre-interned literals.
        /// Returns null for unmapped keys so fast typing allocates zero strings in the keyboard hook.
        /// </summary>
        public static string VkCodeToName(int vk) {
            switch (vk) {
                case 0x30:
                case 0x60: return "0"; // Top-row 0 or Numpad 0 -> Normal Kirby
                case 0x41: return "a";
                case 0x42: return "b";
                case 0x43: return "c";
                case 0x44: return "d";
                case 0x45: return "e";
                case 0x46: return "f";
                case 0x47: return "g";
                case 0x48: return "h";
                case 0x4A: return "j";
                case 0x4C: return "l";
                case 0x4D: return "m";
                case 0x4E: return "n";
                case 0x50: return "p";
                case 0x52: return "r";
                case 0x53: return "s";
                case 0x55: return "u";
                case 0x56: return "v";
                case 0x57: return "w";
                case 0x59: return "y";
                case 0x20: return " "; // Spacebar -> Jump
                default: return null;
            }
        }

        /// <summary>
        /// Resolves the next <see cref="FormDef"/> for a given ability key press.
        /// Pressing the currently active copy ability's key a second time reverts Kirby to Normal form (<c>"0"</c>).
        /// </summary>
        public static FormDef ResolveNextForm(
            Dictionary<string, FormDef> keyToForm,
            FormDef currentForm,
            string keyName) {
            if (keyToForm == null || keyName == null) {
                return currentForm;
            }

            FormDef target;

            if (!keyToForm.TryGetValue(keyName, out target)) {
                return currentForm;
            }

            FormDef normalForm;
            keyToForm.TryGetValue("0", out normalForm);
            return currentForm == target && target != normalForm ? normalForm : target;
        }

    }

}
