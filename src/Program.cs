using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

[assembly: InternalsVisibleTo("KirbyTaskbarBuddy.Tests")]

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Application entry point responsible for DPI configuration and single-instance lifecycle management.
    /// </summary>
    public static class Program {

        [STAThread]
        public static void Main(string[] args) {
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--clean-sprites", StringComparison.OrdinalIgnoreCase)) {
                string dir = args.Length > 1 ? args[1] : "sprites";
                SpriteLoader.StripSpritesheetBackgrounds(dir);
                return;
            }

            // Must be called before creating any HWND so Windows does not bitmap-stretch
            // the pixel-art overlay or return DPI-virtualized screen bounds.
            try {
                NativeMethods.SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException ex) {
                NativeMethods.LogWarning("Program.SetProcessDPIAware", ex);
            }
            catch (DllNotFoundException ex) {
                NativeMethods.LogWarning("Program.SetProcessDPIAware", ex);
            }

            // If an instance is already running, signal its named exit event so it removes
            // its system tray icon and unhooks its keyboard hook cleanly before we start.
            try {
                EventWaitHandle exitEvt;

                if (EventWaitHandle.TryOpenExisting("Local\\KirbyTaskbarBuddyExitEvent", out exitEvt)) {
                    using (exitEvt) {
                        exitEvt.Set();
                    }

                    Thread.Sleep(300);
                }
            }
            catch (WaitHandleCannotBeOpenedException ex) {
                NativeMethods.LogWarning("Program.SignalExistingInstance", ex);
            }
            catch (UnauthorizedAccessException ex) {
                NativeMethods.LogWarning("Program.SignalExistingInstance", ex);
            }
            catch (IOException ex) {
                NativeMethods.LogWarning("Program.SignalExistingInstance", ex);
            }

            bool createdNew;

            using (Mutex mutex = new Mutex(true, "Local\\KirbyTaskbarBuddyMutex", out createdNew))
            using (EventWaitHandle exitEvent =
                   new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\KirbyTaskbarBuddyExitEvent")) {
                if (!createdNew) {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                KirbyTaskbarBuddyContext context = new KirbyTaskbarBuddyContext();

                // Listen on a background wait thread for a future instance asking us to exit.
                ThreadPool.RegisterWaitForSingleObject(exitEvent, (state, timedOut) => { context.RequestCleanExit(); }, null,
                    -1, true);

                Application.Run(context);
            }
        }

    }

}