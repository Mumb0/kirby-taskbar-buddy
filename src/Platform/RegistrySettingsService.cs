using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Win32;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Persists user settings (<see cref="AppSettings"/>) under <c>HKCU\Software\KirbyTaskbarBuddy</c>
    /// and manages the Windows startup registration under <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
    /// </summary>
    public static class RegistrySettingsService {

        public const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string SETTINGS_REG_KEY = @"Software\KirbyTaskbarBuddy";
        public const string REG_VAL_SCALE = "Scale";
        public const string REG_VAL_SLEEP_SECONDS = "SleepTimeoutSeconds";
        public const string REG_VAL_COMBAT = "CombatEnabled";
        public const string APP_NAME = "KirbyTaskbarBuddy";
        public const string LEGACY_APP_NAME = "KirbyWallpaperKeyBridge";

        /// <summary>
        /// Safely parses a registry value (<c>REG_DWORD</c>, <c>REG_QWORD</c>, or <c>REG_SZ</c>) as a 32-bit integer
        /// without throwing exceptions on missing or corrupted data.
        /// </summary>
        public static bool TryParseRegistryInt(object rawValue, out int result) {
            result = 0;

            if (rawValue == null) {
                return false;
            }

            if (rawValue is int) {
                result = (int)rawValue;
                return true;
            }

            if (rawValue is long) {
                long longVal = (long)rawValue;

                if (longVal >= int.MinValue && longVal <= int.MaxValue) {
                    result = (int)longVal;
                    return true;
                }

                return false;
            }

            string strVal = rawValue as string;

            if (strVal != null) {
                return int.TryParse(strVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
            }

            return false;
        }

        /// <summary>
        /// Safely parses a registry value (<c>REG_DWORD</c>, <c>REG_QWORD</c>, or <c>REG_SZ</c>) as a double
        /// without throwing exceptions on missing or corrupted data.
        /// </summary>
        public static bool TryParseRegistryDouble(object rawValue, out double result) {
            result = 0.0;

            if (rawValue == null) {
                return false;
            }

            if (rawValue is int) {
                result = (int)rawValue;
                return true;
            }

            if (rawValue is long) {
                result = (long)rawValue;
                return true;
            }

            if (rawValue is double) {
                result = (double)rawValue;
                return true;
            }

            string strVal = rawValue as string;

            if (strVal != null) {
                return double.TryParse(strVal, NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out result);
            }

            return false;
        }

        /// <summary>
        /// Loads persisted user settings from <c>HKCU\Software\KirbyTaskbarBuddy</c> into <paramref name="settings"/>.
        /// </summary>
        public static void LoadSettings(AppSettings settings) {
            if (settings == null) {
                return;
            }

            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SETTINGS_REG_KEY, false)) {
                    if (key != null) {
                        int parsedScale;

                        if (TryParseRegistryInt(key.GetValue(REG_VAL_SCALE), out parsedScale)) {
                            settings.Scale = AppSettings.NormalizeScale(parsedScale);
                        }

                        double parsedSleep;

                        if (TryParseRegistryDouble(key.GetValue(REG_VAL_SLEEP_SECONDS), out parsedSleep)) {
                            settings.IdleSleepSeconds = AppSettings.NormalizeSleepTimeout(parsedSleep);
                        }

                        int parsedCombat;

                        if (TryParseRegistryInt(key.GetValue(REG_VAL_COMBAT), out parsedCombat)) {
                            settings.CombatEnabled = parsedCombat != 0;
                        }
                    }
                }
            }
            catch (SecurityException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.LoadSettings", ex);
            }
            catch (UnauthorizedAccessException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.LoadSettings", ex);
            }
            catch (IOException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.LoadSettings", ex);
            }
            catch (ObjectDisposedException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.LoadSettings", ex);
            }
        }

        /// <summary>
        /// Saves <paramref name="settings"/> to <c>HKCU\Software\KirbyTaskbarBuddy</c>.
        /// </summary>
        public static void SaveSettings(AppSettings settings) {
            if (settings == null) {
                return;
            }

            try {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SETTINGS_REG_KEY)) {
                    if (key != null) {
                        key.SetValue(REG_VAL_SCALE, settings.Scale, RegistryValueKind.DWord);
                        key.SetValue(REG_VAL_SLEEP_SECONDS, (int)settings.IdleSleepSeconds, RegistryValueKind.DWord);
                        key.SetValue(REG_VAL_COMBAT, settings.CombatEnabled ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch (SecurityException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SaveSettings", ex);
            }
            catch (UnauthorizedAccessException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SaveSettings", ex);
            }
            catch (IOException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SaveSettings", ex);
            }
            catch (ObjectDisposedException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SaveSettings", ex);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if Kirby Taskbar Buddy is registered to launch at Windows startup.
        /// </summary>
        public static bool IsStartupEnabled() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, false)) {
                    return key != null && (key.GetValue(APP_NAME) != null || key.GetValue(LEGACY_APP_NAME) != null);
                }
            }
            catch (SecurityException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.IsStartupEnabled", ex);
                return false;
            }
            catch (UnauthorizedAccessException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.IsStartupEnabled", ex);
                return false;
            }
            catch (IOException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.IsStartupEnabled", ex);
                return false;
            }
            catch (ObjectDisposedException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.IsStartupEnabled", ex);
                return false;
            }
        }

        /// <summary>
        /// Enables or disables launching Kirby Taskbar Buddy at Windows startup, cleaning up any legacy key name.
        /// </summary>
        public static void SetStartup(bool enable) {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, true)) {
                    if (key != null) {
                        key.DeleteValue(LEGACY_APP_NAME, false);

                        if (enable) {
                            string exePath = Assembly.GetExecutingAssembly().Location;
                            key.SetValue(APP_NAME, "\"" + exePath + "\"");
                        }
                        else {
                            key.DeleteValue(APP_NAME, false);
                        }
                    }
                }
            }
            catch (SecurityException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SetStartup", ex);
            }
            catch (UnauthorizedAccessException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SetStartup", ex);
            }
            catch (IOException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SetStartup", ex);
            }
            catch (ObjectDisposedException ex) {
                Diagnostics.LogWarning("RegistrySettingsService.SetStartup", ex);
            }
        }

    }

}
