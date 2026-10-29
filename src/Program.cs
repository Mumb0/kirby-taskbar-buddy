using System;
using System.Threading;
using System.Windows.Forms;

namespace KirbyTaskbarBuddy
{
    /// <summary>
    /// Application entry point responsible for DPI configuration and single-instance lifecycle management.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Must be called before creating any HWND so Windows does not bitmap-stretch
            // the pixel-art overlay or return DPI-virtualized screen bounds.
            try { NativeMethods.SetProcessDPIAware(); } catch { }

            // If an instance is already running, signal its named exit event so it removes
            // its system tray icon and unhooks its keyboard hook cleanly before we start.
            try
            {
                EventWaitHandle exitEvt = EventWaitHandle.OpenExisting("Local\\KirbyTaskbarBuddyExitEvent");
                exitEvt.Set();
                Thread.Sleep(300);
            }
            catch { }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\KirbyTaskbarBuddyMutex", out createdNew))
            using (EventWaitHandle exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\KirbyTaskbarBuddyExitEvent"))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                KirbyTaskbarBuddyContext context = new KirbyTaskbarBuddyContext();

                // Listen on a background wait thread for a future instance asking us to exit.
                ThreadPool.RegisterWaitForSingleObject(exitEvent, (state, timedOut) =>
                {
                    context.RequestCleanExit();
                }, null, -1, true);

                Application.Run(context);
            }
        }
    }
}
