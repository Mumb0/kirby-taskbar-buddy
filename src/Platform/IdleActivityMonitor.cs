using System.Drawing;
using System.Runtime.InteropServices;

namespace KirbyTaskbarBuddy {

    /// <summary>
    /// Tracks mouse movement and Win32 <c>GetLastInputInfo</c> ticks to measure user idle duration
    /// for Kirby's automatic sleep timer.
    /// </summary>
    public sealed class IdleActivityMonitor {

        private long _lastInputMs;
        private Point _lastMousePos;
        private uint _lastSysInputTick;

        public long LastInputMs {
            get { return _lastInputMs; }
        }

        public IdleActivityMonitor(long initialMs, Point initialMousePos) {
            _lastInputMs = initialMs;
            _lastMousePos = initialMousePos;

            NativeMethods.LASTINPUTINFO initLii = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO))
            };

            if (NativeMethods.GetLastInputInfo(ref initLii)) {
                _lastSysInputTick = initLii.dwTime;
            }
        }

        /// <summary>
        /// Checks whether the mouse cursor moved or the system-wide input tick (<c>GetLastInputInfo</c>)
        /// advanced since the previous poll.
        /// </summary>
        public bool PollActivity(Point currentMousePos) {
            bool hasInput = false;

            if (currentMousePos.X != _lastMousePos.X || currentMousePos.Y != _lastMousePos.Y) {
                _lastMousePos = currentMousePos;
                hasInput = true;
            }

            NativeMethods.LASTINPUTINFO lii = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO))
            };

            if (NativeMethods.GetLastInputInfo(ref lii) && lii.dwTime != _lastSysInputTick) {
                _lastSysInputTick = lii.dwTime;
                hasInput = true;
            }

            return hasInput;
        }

        /// <summary>
        /// Records user activity at <paramref name="nowMs"/>.
        /// </summary>
        public void RecordActivity(long nowMs) {
            _lastInputMs = nowMs;
        }

        /// <summary>
        /// Returns the elapsed idle time in seconds since the last recorded user input.
        /// </summary>
        public double GetIdleSeconds(long nowMs) {
            return (nowMs - _lastInputMs) / 1000.0;
        }

    }

}