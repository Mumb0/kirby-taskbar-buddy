using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Non-fatal diagnostic tracing and process working-set memory management helpers.
    /// </summary>
    internal static class Diagnostics {

        /// <summary>
        /// Writes a non-fatal diagnostic warning to registered trace/debug listeners (e.g., Visual Studio, Rider, DebugView)
        /// without allocating UI dialogs or performing disk I/O.
        /// </summary>
        public static void LogWarning(string context, Exception ex) {
            if (ex == null) {
                return;
            }

            Trace.TraceWarning("[KirbyTaskbarBuddy] {0}: {1}: {2}", context, ex.GetType().Name, ex.Message);
        }

        /// <summary>
        /// Forces garbage collection and instructs Windows to trim unused pages (such as temporary
        /// startup PNG decode buffers) from the process working set.
        /// </summary>
        public static void TrimMemory() {
            try {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

                using (Process currentProcess = Process.GetCurrentProcess()) {
                    NativeMethods.SetProcessWorkingSetSize(currentProcess.Handle, new IntPtr(-1), new IntPtr(-1));
                }
            }
            catch (InvalidOperationException ex) {
                LogWarning("Diagnostics.TrimMemory", ex);
            }
            catch (ExternalException ex) {
                LogWarning("Diagnostics.TrimMemory", ex);
            }
            catch (NotSupportedException ex) {
                LogWarning("Diagnostics.TrimMemory", ex);
            }
        }

    }

}