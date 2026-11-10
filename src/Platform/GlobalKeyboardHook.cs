using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Manages the low-level Win32 keyboard hook (<c>WH_KEYBOARD_LL</c>), deduplicating OS key auto-repeat
    /// and forwarding key-down events to the application without a static singleton.
    /// </summary>
    public sealed class GlobalKeyboardHook : IDisposable {

        // Instance reference to the hook delegate prevents the GC from collecting the callback while unmanaged code holds it.
        private readonly NativeMethods.LowLevelKeyboardProc _proc;
        private readonly HashSet<int> _downKeys = new HashSet<int>();
        private readonly Action<int, bool> _onKeyDown;
        private IntPtr _hookId = IntPtr.Zero;

        public GlobalKeyboardHook(Action<int, bool> onKeyDown, bool installOsHook = true) {
            _onKeyDown = onKeyDown;
            _proc = HookCallback;

            if (installOsHook) {
                _hookId = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_KEYBOARD_LL,
                    _proc,
                    NativeMethods.GetModuleHandle(null),
                    0);
            }
        }

        /// <summary>
        /// Processes a low-level keyboard message (<c>WM_KEYDOWN</c>, <c>WM_SYSKEYDOWN</c>, <c>WM_KEYUP</c>, <c>WM_SYSKEYUP</c>),
        /// invoking the key-down callback with <c>isInitialKeyDown = true</c> only on the first transition
        /// into the pressed state (ignoring OS auto-repeat until the key is released).
        /// </summary>
        public void ProcessKeyMessage(int msg, int vkCode) {
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN) {
                bool isFirstDown = _downKeys.Add(vkCode);

                if (_onKeyDown != null) {
                    _onKeyDown(vkCode, isFirstDown);
                }
            }
            else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP) {
                _downKeys.Remove(vkCode);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                int msg = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);
                ProcessKeyMessage(msg, vkCode);
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose() {
            if (_hookId != IntPtr.Zero) {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

    }

}
