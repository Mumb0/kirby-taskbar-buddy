using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KirbyTaskbarBuddy
{
    /// <summary>
    /// Centralizes Win32/GDI P/Invoke declarations, window style constants, shared canvas geometry,
    /// and zero-allocation input/memory helpers.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// Logical unscaled canvas size (80x80 pixels) used to align all sprites.
        /// </summary>
        public const int TILE_SIZE = 80;

        /// <summary>
        /// Horizontal center of Kirby's feet within the 80x80 logical tile.
        /// </summary>
        public const int PIVOT_X = 40;

        /// <summary>
        /// Vertical floor line (Y=54) where Kirby's feet rest within the 80x80 logical tile.
        /// Leaves 54px above for tall hats/weapons (Parasol, Hammer) and 25px below for Mirror's guard bubble.
        /// </summary>
        public const int PIVOT_Y = 54;

        // Extended Window Styles for a click-through, non-activating, topmost desktop overlay:
        // - WS_EX_LAYERED:     Enables per-pixel alpha blending via UpdateLayeredWindow.
        // - WS_EX_TRANSPARENT: Passes all mouse clicks/hover events through to windows underneath.
        // - WS_EX_TOPMOST:     Keeps Kirby above normal desktop windows.
        // - WS_EX_TOOLWINDOW:  Hides the window from the Alt+Tab switcher.
        // - WS_EX_NOACTIVATE:  Prevents the overlay from ever stealing keyboard focus.
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;
        public const int ULW_ALPHA = 0x00000002;

        // Global low-level keyboard hook constants
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref POINT pptDst,
            ref SIZE psize,
            IntPtr hdcSrc,
            ref POINT pprSrc,
            int crKey,
            ref BLENDFUNCTION pblend,
            int dwFlags);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr CreateDIBSection(
            IntPtr hdc,
            [In] ref BITMAPINFO pbmi,
            uint iUsage,
            out IntPtr ppvBits,
            IntPtr hSection,
            uint dwOffset);

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        /// <summary>
        /// Forces garbage collection and instructs Windows to trim unused pages (such as temporary
        /// startup PNG decode buffers) from the process working set.
        /// </summary>
        public static void TrimMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        /// <summary>
        /// Maps a Win32 Virtual-Key code to a Kirby ability key string using pre-interned literals.
        /// Returns null for unmapped keys so fast typing allocates zero strings in the keyboard hook.
        /// </summary>
        public static string VkCodeToName(int vk)
        {
            switch (vk)
            {
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
                default:   return null;
            }
        }
    }
}
