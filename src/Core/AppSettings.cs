namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Holds user-configurable runtime settings (Kirby sprite scale, idle sleep timeout, combat toggle,
    /// foreground overlay toggle, and key-switching pause state) along with normalization helpers.
    /// </summary>
    public sealed class AppSettings {

        public const int DEFAULT_SCALE = 2;
        public const double DEFAULT_SLEEP_TIMEOUT_SECONDS = 300.0;

        public static readonly double[] SleepTimeoutValues = new[]
        {
            10.0,
            30.0,
            60.0,
            180.0,
            300.0,
            600.0,
            -1.0
        };

        public static readonly string[] SleepTimeoutLabels = new[]
        {
            "10 Seconds",
            "30 Seconds",
            "1 Minute",
            "3 Minutes",
            "5 Minutes (Default)",
            "10 Minutes",
            "Never"
        };

        public int Scale;
        public double IdleSleepSeconds;
        public bool CombatEnabled;
        public bool OverlayEnabled;
        public bool IsPaused;

        public AppSettings() {
            Scale = DEFAULT_SCALE;
            IdleSleepSeconds = DEFAULT_SLEEP_TIMEOUT_SECONDS;
            CombatEnabled = true;
            OverlayEnabled = true;
            IsPaused = false;
        }

        /// <summary>
        /// Clamps a candidate scale factor to the supported <c>1..4</c> range, falling back to <c>2</c> (Default).
        /// </summary>
        public static int NormalizeScale(int scale) {
            return scale >= 1 && scale <= 4 ? scale : DEFAULT_SCALE;
        }

        /// <summary>
        /// Validates a candidate sleep timeout against <see cref="SleepTimeoutValues"/>, falling back to <c>300.0</c> (5 Minutes).
        /// </summary>
        public static double NormalizeSleepTimeout(double seconds) {
            for (int i = 0; i < SleepTimeoutValues.Length; i++) {
                if (SleepTimeoutValues[i] == seconds) {
                    return seconds;
                }
            }

            return DEFAULT_SLEEP_TIMEOUT_SECONDS;
        }

    }

}