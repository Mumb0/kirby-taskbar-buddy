using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public sealed class SpriteTile
{
    public int MinX;
    public int MinY;
    public int Width;
    public int Height;
    public int[] PremulPixels;
}

public class KirbyOverlayWindow : Form
{
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;
    private const int ULW_ALPHA = 0x00000002;
    private const int TILE_SIZE = 80;

    private IntPtr _memDc = IntPtr.Zero;
    private IntPtr _hDib = IntPtr.Zero;
    private IntPtr _oldBmp = IntPtr.Zero;
    private IntPtr _dibBits = IntPtr.Zero;
    private int _dibSize = 0;
    private SpriteTile _renderedTile = null;
    private int _renderedScale = 0;
    private Rectangle _dirtyRect = Rectangle.Empty;

    public KirbyOverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(128, 128);
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    private void FreeDib()
    {
        if (_memDc != IntPtr.Zero)
        {
            if (_oldBmp != IntPtr.Zero)
            {
                SelectObject(_memDc, _oldBmp);
                _oldBmp = IntPtr.Zero;
            }
            if (_hDib != IntPtr.Zero)
            {
                DeleteObject(_hDib);
                _hDib = IntPtr.Zero;
            }
            DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }
        _dibBits = IntPtr.Zero;
        _dibSize = 0;
        _renderedTile = null;
        _dirtyRect = Rectangle.Empty;
    }

    private void EnsureDib(int targetSize)
    {
        if (_memDc != IntPtr.Zero && _dibSize == targetSize && _dibBits != IntPtr.Zero) return;

        FreeDib();

        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            _memDc = CreateCompatibleDC(screenDc);
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = targetSize;
            bmi.bmiHeader.biHeight = -targetSize; // Top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            _hDib = CreateDIBSection(_memDc, ref bmi, 0, out _dibBits, IntPtr.Zero, 0);
            if (_hDib != IntPtr.Zero)
            {
                _oldBmp = SelectObject(_memDc, _hDib);
                _dibSize = targetSize;
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public void UpdateSpriteFrame(SpriteTile tile, int scale, int screenX, int screenY)
    {
        if (IsDisposed || !IsHandleCreated || tile == null) return;

        int targetSize = TILE_SIZE * scale;
        EnsureDib(targetSize);
        if (_memDc == IntPtr.Zero || _dibBits == IntPtr.Zero) return;

        if (tile != _renderedTile || scale != _renderedScale)
        {
            if (!_dirtyRect.IsEmpty)
            {
                int clearRight = Math.Min(targetSize, _dirtyRect.Right);
                int clearBottom = Math.Min(targetSize, _dirtyRect.Bottom);
                for (int y = Math.Max(0, _dirtyRect.Top); y < clearBottom; y++)
                {
                    int rowBase = (y * targetSize) << 2;
                    for (int x = Math.Max(0, _dirtyRect.Left); x < clearRight; x++)
                    {
                        Marshal.WriteInt32(_dibBits, rowBase + (x << 2), 0);
                    }
                }
            }

            int startX = tile.MinX * scale;
            int startY = tile.MinY * scale;
            int srcIdx = 0;
            for (int sy = 0; sy < tile.Height; sy++)
            {
                int baseY = startY + sy * scale;
                for (int sx = 0; sx < tile.Width; sx++)
                {
                    int c = tile.PremulPixels[srcIdx++];
                    if (c != 0)
                    {
                        int baseX = startX + sx * scale;
                        for (int dy = 0; dy < scale; dy++)
                        {
                            int rowOffset = ((baseY + dy) * targetSize + baseX) << 2;
                            for (int dx = 0; dx < scale; dx++)
                            {
                                Marshal.WriteInt32(_dibBits, rowOffset + (dx << 2), c);
                            }
                        }
                    }
                }
            }

            _dirtyRect = new Rectangle(startX, startY, tile.Width * scale, tile.Height * scale);
            _renderedTile = tile;
            _renderedScale = scale;
        }

        SIZE size = new SIZE { cx = targetSize, cy = targetSize };
        POINT pointSource = new POINT { x = 0, y = 0 };
        POINT topPos = new POINT { x = screenX, y = screenY };
        BLENDFUNCTION blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC_SRC_ALPHA
        };

        UpdateLayeredWindow(Handle, IntPtr.Zero, ref topPos, ref size, _memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
    }

    protected override void Dispose(bool disposing)
    {
        FreeDib();
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
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
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public int bmiColors;
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
}

public class KirbyTaskbarBuddy : ApplicationContext
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "KirbyTaskbarBuddy";
    private const string LEGACY_APP_NAME = "KirbyWallpaperKeyBridge";

    private const int TILE_SIZE = 80;
    private const int PIVOT_X = 40;
    private const int PIVOT_Y = 54;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static LowLevelKeyboardProc _proc;
    private static IntPtr _hookID = IntPtr.Zero;

    private static readonly HashSet<int> _downKeys = new HashSet<int>();
    private static KirbyTaskbarBuddy _instance;

    private bool _isPaused = false;
    private bool _overlayEnabled = true;
    private int _scale = 2;
    private double _idleSleepSeconds = 300.0;

    private NotifyIcon _trayIcon;
    private MenuItem _overlayItem;
    private MenuItem _pauseItem;
    private MenuItem _startupItem;
    private MenuItem _scale2xItem;
    private MenuItem _scale3xItem;
    private MenuItem _scale4xItem;
    private MenuItem[] _sleepMenuItems;
    private double[] _sleepTimeoutValues;

    private KirbyOverlayWindow _overlay;
    private System.Windows.Forms.Timer _animTimer;
    private Stopwatch _stopwatch;
    private long _lastTickMs;

    private class FormDef
    {
        public string Name;
        public string Key;
        public string FileName;
        public Rectangle[] Coords;
        public Rectangle[] GuardCoords;
        public Rectangle[] JumpCoords;
        public Rectangle[] StandCoords;
        public SpriteTile[] Frames;
        public SpriteTile[] GuardFrames;
        public SpriteTile[] JumpFrames;
        public SpriteTile[] StandFrames;
    }

    private sealed class FastSheet : IDisposable
    {
        private Stream _stream;
        private Bitmap _bmp;
        private BitmapData _data;
        private IntPtr _scan0;
        private int _stride;
        public readonly int Width;
        public readonly int Height;

        public FastSheet(Stream stream, Bitmap bmp, int lockWidth, int lockHeight)
        {
            _stream = stream;
            _bmp = bmp;
            Width = Math.Min(bmp.Width, Math.Max(1, lockWidth));
            Height = Math.Min(bmp.Height, Math.Max(1, lockHeight));
            _data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            _scan0 = _data.Scan0;
            _stride = _data.Stride;
        }

        public int GetPixel(int x, int y)
        {
            return Marshal.ReadInt32(_scan0, y * _stride + (x << 2));
        }

        public void Dispose()
        {
            if (_bmp != null)
            {
                if (_data != null)
                {
                    try { _bmp.UnlockBits(_data); } catch { }
                    _data = null;
                }
                _bmp.Dispose();
                _bmp = null;
            }
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }
    }

    private readonly List<FormDef> _forms = new List<FormDef>();
    private readonly Dictionary<string, FormDef> _keyToForm = new Dictionary<string, FormDef>();
    private FormDef _currentForm;

    private const double JUMP_INIT_VEL = 4.2;
    private const double JUMP_GRAVITY = 0.18;

    private enum SleepState
    {
        Awake,
        Halting,
        FallingAsleep,
        Sleeping,
        WakingUp,
        PostWakeStanding
    }

    private static readonly Rectangle[] _sleepCoords = new Rectangle[]
    {
        new Rectangle(4, 6, 30, 28),   // 0: Falling asleep (eyes closed)
        new Rectangle(36, 6, 30, 28),  // 1: Falling asleep (eyes open)
        new Rectangle(68, 12, 30, 22), // 2: Sleeping 1 (inhale peak)
        new Rectangle(99, 12, 30, 22), // 3: Sleeping 2
        new Rectangle(130, 13, 30, 21),// 4: Sleeping 3
        new Rectangle(162, 14, 30, 20),// 5: Sleeping 4 (exhale low)
        new Rectangle(194, 6, 30, 28), // 6: Waking up (eyes closed)
        new Rectangle(226, 6, 30, 28)  // 7: Waking up (eyes open)
    };

    private static readonly int[] _haltStandSeq = new int[] { 0, 0, 1, 0, 0, 1, 0 };
    private static readonly int[] _fallAsleepSeq = new int[] { 1, 1, 0, 1, 1, 0, 1, 1, 0 };
    private static readonly int[] _sleepLoopSeq = new int[] { 2, 3, 4, 5, 4, 3 };
    private static readonly int[] _wakeUpSeq = new int[] { 6, 7, 6, 7, 6, 7 };
    private static readonly int[] _postWakeStandSeq = new int[] { 0, 1, 0 };

    private SpriteTile[] _sleepFrames;
    private SleepState _sleepState = SleepState.Awake;
    private double _sleepAnimTime = 0.0;
    private long _lastInputMs = 0;
    private Point _lastMousePos;
    private uint _lastSysInputTick = 0;

    private double _kirbyX = -64;
    private double _animTime = 0.0;
    private bool _isJumping = false;
    private double _jumpY = 0.0;
    private double _jumpVelY = 0.0;
    private int _lastRenderedX = int.MinValue;
    private int _lastRenderedY = int.MinValue;
    private SpriteTile _lastRenderedTile = null;
    private int _topmostRefreshCounter = 0;
    private int _envCheckCounter = 0;
    private bool _isFullscreenCached = false;
    private Rectangle _cachedWorkArea;
    private readonly StringBuilder _classNameSb = new StringBuilder(64);
    private IntPtr _trayIconHandle = IntPtr.Zero;
    private Icon _loadedTrayIcon = null;

    [STAThread]
    public static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }

        // Signal any running instance to exit cleanly before starting
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
            _instance = new KirbyTaskbarBuddy();

            ThreadPool.RegisterWaitForSingleObject(exitEvent, (state, timedOut) =>
            {
                if (_instance != null && _instance._overlay != null && _instance._overlay.IsHandleCreated)
                {
                    _instance._overlay.BeginInvoke(new Action(() => _instance.OnExit(null, EventArgs.Empty)));
                }
            }, null, -1, true);

            Application.Run(_instance);
        }
    }

    public KirbyTaskbarBuddy()
    {
        InitFormDefinitions();
        LoadAllSpritesheets();

        _overlayItem = new MenuItem("Show Foreground Taskbar Kirby", OnToggleOverlay) { Checked = _overlayEnabled };
        _pauseItem = new MenuItem("Pause Key Switching", OnTogglePause);
        if (IsStartupEnabled()) SetStartup(true);
        _startupItem = new MenuItem("Start with Windows", OnToggleStartup) { Checked = IsStartupEnabled() };

        _scale2xItem = new MenuItem("2x (Default)", (s, e) => SetScale(2)) { Checked = (_scale == 2) };
        _scale3xItem = new MenuItem("3x (Large)", (s, e) => SetScale(3)) { Checked = (_scale == 3) };
        _scale4xItem = new MenuItem("4x (Extra Large)", (s, e) => SetScale(4)) { Checked = (_scale == 4) };
        MenuItem scaleMenu = new MenuItem("Kirby Size", new MenuItem[] { _scale2xItem, _scale3xItem, _scale4xItem });

        string[] sleepLabels = new string[]
        {
            "10 Seconds",
            "30 Seconds",
            "1 Minute",
            "3 Minutes",
            "5 Minutes (Default)",
            "10 Minutes",
            "Never"
        };
        _sleepTimeoutValues = new double[] { 10.0, 30.0, 60.0, 180.0, 300.0, 600.0, -1.0 };
        _sleepMenuItems = new MenuItem[sleepLabels.Length];
        for (int i = 0; i < sleepLabels.Length; i++)
        {
            double val = _sleepTimeoutValues[i];
            _sleepMenuItems[i] = new MenuItem(sleepLabels[i], (s, e) => SetSleepTimeout(val))
            {
                Checked = (_idleSleepSeconds == val)
            };
        }
        MenuItem sleepMenu = new MenuItem("Sleep Timer", _sleepMenuItems);

        ContextMenu menu = new ContextMenu(new MenuItem[]
        {
            new MenuItem("Kirby Taskbar Buddy") { Enabled = false },
            new MenuItem("-"),
            _overlayItem,
            scaleMenu,
            sleepMenu,
            _pauseItem,
            _startupItem,
            new MenuItem("-"),
            new MenuItem("Exit", OnExit)
        });

        _trayIcon = new NotifyIcon
        {
            Icon = LoadKirbyTrayIcon(),
            Text = "Kirby Taskbar Buddy",
            ContextMenu = menu,
            Visible = true
        };

        _overlay = new KirbyOverlayWindow();
        _overlay.Show();
        _cachedWorkArea = Screen.PrimaryScreen.WorkingArea;

        _stopwatch = Stopwatch.StartNew();
        _lastTickMs = _stopwatch.ElapsedMilliseconds;
        _lastInputMs = _lastTickMs;
        _lastMousePos = Cursor.Position;
        LASTINPUTINFO initLii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        if (GetLastInputInfo(ref initLii)) _lastSysInputTick = initLii.dwTime;

        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += OnTick;
        _animTimer.Start();

        _proc = HookCallback;
        _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);

        TrimMemory();
    }

    private void InitFormDefinitions()
    {
        AddForm("normal", "0", "kirby-normal.png", new[] {
            new Rectangle(32, 125, 24, 24), new Rectangle(60, 125, 24, 24),
            new Rectangle(87, 125, 24, 24), new Rectangle(114, 125, 24, 24)
        }, new[] {
            new Rectangle(79, 263, 26, 25), new Rectangle(108, 263, 26, 25)
        }, new[] {
            new Rectangle(66, 193, 23, 24), new Rectangle(94, 193, 23, 23),
            new Rectangle(122, 194, 21, 22), new Rectangle(147, 193, 24, 23),
            new Rectangle(176, 193, 23, 24)
        }, new[] {
            new Rectangle(113, 55, 24, 23), new Rectangle(142, 55, 24, 23)
        });
        AddForm("ice", "l", "kirby-ice.png", new[] {
            new Rectangle(6, 213, 24, 34), new Rectangle(35, 213, 24, 34),
            new Rectangle(63, 213, 24, 34), new Rectangle(92, 213, 24, 34)
        }, new[] {
            new Rectangle(42, 605, 34, 26), new Rectangle(42, 642, 34, 26),
            new Rectangle(42, 679, 34, 26)
        }, new[] {
            new Rectangle(6, 372, 24, 33), new Rectangle(35, 381, 26, 24),
            new Rectangle(66, 381, 26, 27), new Rectangle(97, 372, 25, 32),
            new Rectangle(127, 365, 23, 40)
        }, new[] {
            new Rectangle(35, 84, 24, 32), new Rectangle(64, 84, 24, 32)
        });
        AddForm("bomb", "b", "kirby-bomb.png", new[] {
            new Rectangle(0, 101, 31, 29), new Rectangle(32, 101, 32, 29),
            new Rectangle(68, 101, 32, 29), new Rectangle(98, 101, 32, 29)
        }, new[] {
            new Rectangle(304, 103, 32, 28), new Rectangle(339, 103, 32, 28)
        }, new[] {
            new Rectangle(6, 174, 27, 30), new Rectangle(38, 180, 23, 23),
            new Rectangle(66, 180, 21, 27), new Rectangle(92, 178, 28, 25),
            new Rectangle(125, 168, 23, 36)
        }, new[] {
            new Rectangle(40, 34, 29, 28), new Rectangle(74, 34, 29, 28)
        });
        AddForm("beam", "e", "kirby-beam.png", new[] {
            new Rectangle(5, 101, 27, 37), new Rectangle(35, 101, 27, 37),
            new Rectangle(65, 101, 27, 37), new Rectangle(96, 101, 27, 37)
        }, new[] {
            new Rectangle(5, 348, 31, 27), new Rectangle(39, 348, 31, 27)
        }, new[] {
            new Rectangle(6, 193, 26, 30), new Rectangle(37, 199, 27, 24),
            new Rectangle(69, 199, 23, 29), new Rectangle(97, 191, 28, 31),
            new Rectangle(130, 184, 24, 39)
        }, new[] {
            new Rectangle(37, 29, 26, 31), new Rectangle(68, 29, 26, 31)
        });
        AddForm("cutter", "c", "kirby-cutter.png", new[] {
            new Rectangle(4, 126, 29, 35), new Rectangle(34, 126, 29, 35),
            new Rectangle(66, 126, 29, 35), new Rectangle(100, 126, 29, 35)
        }, new[] {
            new Rectangle(5, 413, 28, 30), new Rectangle(36, 413, 28, 30)
        }, new[] {
            new Rectangle(6, 214, 23, 37), new Rectangle(34, 227, 23, 28),
            new Rectangle(62, 227, 23, 33), new Rectangle(90, 221, 27, 29),
            new Rectangle(122, 216, 24, 35)
        }, new[] {
            new Rectangle(35, 34, 24, 34), new Rectangle(64, 34, 24, 34)
        });
        AddForm("fighter", "f", "kirby-fighter.png", new[] {
            new Rectangle(6, 95, 26, 24), new Rectangle(36, 95, 26, 24),
            new Rectangle(66, 95, 26, 24), new Rectangle(96, 95, 26, 24)
        }, new[] {
            new Rectangle(5, 333, 29, 25), new Rectangle(37, 333, 29, 25)
        }, new[] {
            new Rectangle(6, 176, 25, 24), new Rectangle(36, 171, 23, 28),
            new Rectangle(64, 175, 21, 23), new Rectangle(90, 176, 26, 23),
            new Rectangle(121, 169, 23, 31)
        }, new[] {
            new Rectangle(35, 27, 24, 23), new Rectangle(64, 27, 24, 23)
        });
        AddForm("fire", "d", "kirby-fire.png", new[] {
            new Rectangle(6, 309, 24, 34), new Rectangle(35, 309, 24, 34),
            new Rectangle(63, 309, 24, 34), new Rectangle(92, 309, 24, 34)
        }, new[] {
            new Rectangle(5, 941, 28, 31), new Rectangle(36, 941, 28, 31)
        }, new[] {
            new Rectangle(6, 529, 24, 33), new Rectangle(35, 538, 26, 24),
            new Rectangle(66, 538, 26, 27), new Rectangle(97, 529, 25, 32),
            new Rectangle(127, 522, 23, 40)
        }, new[] {
            new Rectangle(35, 134, 24, 32), new Rectangle(64, 134, 24, 32)
        });
        AddForm("hammer", "h", "kirby-hammer.png", new[] {
            new Rectangle(5, 164, 44, 49), new Rectangle(52, 164, 44, 49),
            new Rectangle(99, 164, 44, 49), new Rectangle(146, 164, 44, 49)
        }, new[] {
            new Rectangle(5, 426, 29, 34), new Rectangle(37, 426, 29, 34)
        }, new[] {
            new Rectangle(8, 240, 42, 43), new Rectangle(82, 258, 31, 42),
            new Rectangle(132, 259, 27, 45), new Rectangle(174, 239, 44, 42),
            new Rectangle(236, 234, 37, 47)
        }, new[] {
            new Rectangle(53, 100, 36, 40), new Rectangle(94, 100, 36, 40)
        });
        AddForm("jet", "j", "kirby-jet.png", new[] {
            new Rectangle(6, 114, 28, 29), new Rectangle(39, 114, 28, 29),
            new Rectangle(70, 114, 28, 29), new Rectangle(101, 114, 28, 29)
        }, new[] {
            new Rectangle(5, 283, 30, 28), new Rectangle(38, 283, 30, 28)
        }, new[] {
            new Rectangle(6, 195, 26, 29), new Rectangle(37, 200, 24, 23),
            new Rectangle(66, 200, 26, 24), new Rectangle(97, 197, 27, 26),
            new Rectangle(129, 193, 25, 31)
        }, new[] {
            new Rectangle(40, 32, 29, 28), new Rectangle(74, 32, 29, 28)
        });
        AddForm("mirror", "m", "kirby-mirror.png", new[] {
            new Rectangle(5, 117, 27, 37), new Rectangle(35, 117, 27, 37),
            new Rectangle(65, 117, 27, 37), new Rectangle(96, 117, 27, 37)
        }, new[] {
            new Rectangle(220, 877, 32, 34), new Rectangle(255, 877, 32, 34),
            new Rectangle(290, 877, 32, 34), new Rectangle(325, 877, 32, 34),
            new Rectangle(360, 877, 32, 34), new Rectangle(395, 877, 32, 34),
            new Rectangle(430, 877, 32, 34), new Rectangle(465, 877, 32, 34),
            new Rectangle(500, 877, 32, 34), new Rectangle(535, 877, 32, 34),
            new Rectangle(570, 877, 32, 34), new Rectangle(605, 877, 32, 34),
            new Rectangle(640, 877, 32, 34), new Rectangle(675, 877, 32, 34),
            new Rectangle(710, 877, 32, 34)
        }, new[] {
            new Rectangle(6, 272, 26, 30), new Rectangle(37, 278, 27, 24),
            new Rectangle(69, 278, 23, 29), new Rectangle(97, 270, 28, 31),
            new Rectangle(130, 263, 24, 39)
        }, new[] {
            new Rectangle(37, 44, 26, 31), new Rectangle(68, 44, 26, 31)
        });
        AddForm("ninja", "n", "kirby-ninja.png", new[] {
            new Rectangle(207, 45, 27, 32), new Rectangle(235, 45, 27, 32),
            new Rectangle(264, 45, 27, 32), new Rectangle(296, 45, 27, 32)
        }, new[] {
            new Rectangle(5, 234, 28, 26), new Rectangle(36, 234, 28, 26)
        }, new[] {
            new Rectangle(6, 136, 23, 33), new Rectangle(34, 145, 23, 24),
            new Rectangle(62, 145, 21, 31), new Rectangle(90, 141, 24, 27),
            new Rectangle(119, 130, 23, 39)
        }, new[] {
            new Rectangle(35, 43, 24, 30), new Rectangle(64, 43, 24, 30)
        });
        AddForm("paint", "p", "kirby-paint.png", new[] {
            new Rectangle(5, 113, 26, 28), new Rectangle(34, 113, 26, 28),
            new Rectangle(62, 113, 26, 28), new Rectangle(92, 113, 26, 28)
        }, new[] {
            new Rectangle(181, 196, 26, 25), new Rectangle(210, 196, 26, 25)
        }, new[] {
            new Rectangle(6, 195, 24, 28), new Rectangle(35, 198, 23, 24),
            new Rectangle(63, 199, 22, 25), new Rectangle(90, 196, 24, 26),
            new Rectangle(119, 195, 23, 28)
        }, new[] {
            new Rectangle(35, 29, 24, 27), new Rectangle(64, 29, 24, 27)
        });
        AddForm("parasol", "r", "kirby-parasol.png", new[] {
            new Rectangle(269, 92, 42, 49), new Rectangle(314, 92, 42, 49),
            new Rectangle(359, 92, 42, 49), new Rectangle(404, 92, 42, 49)
        }, new[] {
            new Rectangle(669, 116, 29, 25), new Rectangle(701, 116, 29, 25)
        }, new[] {
            new Rectangle(11, 167, 37, 44), new Rectangle(77, 186, 30, 41),
            new Rectangle(130, 187, 21, 45), new Rectangle(165, 168, 42, 41),
            new Rectangle(229, 162, 30, 47)
        }, new[] {
            new Rectangle(42, 91, 31, 46), new Rectangle(78, 91, 31, 46)
        });
        AddForm("plasma", "a", "kirby-plasma.png", new[] {
            new Rectangle(6, 357, 24, 34), new Rectangle(35, 357, 24, 34),
            new Rectangle(63, 357, 24, 34), new Rectangle(92, 357, 24, 34)
        }, new[] {
            new Rectangle(5, 1078, 28, 29), new Rectangle(36, 1078, 28, 29)
        }, new[] {
            new Rectangle(6, 608, 24, 33), new Rectangle(35, 617, 26, 24),
            new Rectangle(66, 617, 26, 27), new Rectangle(97, 608, 25, 32),
            new Rectangle(127, 601, 23, 40)
        }, new[] {
            new Rectangle(35, 159, 24, 32), new Rectangle(64, 159, 24, 32)
        });
        AddForm("stone", "s", "kirby-stone.png", new[] {
            new Rectangle(5, 123, 27, 32), new Rectangle(33, 123, 27, 32),
            new Rectangle(62, 123, 27, 32), new Rectangle(94, 123, 27, 32)
        }, new[] {
            new Rectangle(5, 406, 28, 25), new Rectangle(36, 406, 28, 25)
        }, new[] {
            new Rectangle(6, 214, 23, 33), new Rectangle(34, 223, 23, 24),
            new Rectangle(62, 223, 21, 31), new Rectangle(90, 219, 24, 27),
            new Rectangle(119, 208, 23, 39)
        }, new[] {
            new Rectangle(35, 29, 24, 30), new Rectangle(64, 29, 24, 30)
        });
        AddForm("suplex", "u", "kirby-suplex.png", new[] {
            new Rectangle(6, 95, 26, 24), new Rectangle(36, 95, 26, 24),
            new Rectangle(66, 95, 26, 24), new Rectangle(96, 95, 26, 24)
        }, new[] {
            new Rectangle(5, 333, 29, 25), new Rectangle(37, 333, 29, 25)
        }, new[] {
            new Rectangle(6, 176, 25, 24), new Rectangle(36, 171, 23, 28),
            new Rectangle(64, 175, 21, 23), new Rectangle(90, 176, 26, 23),
            new Rectangle(121, 169, 23, 31)
        }, new[] {
            new Rectangle(35, 27, 24, 23), new Rectangle(64, 27, 24, 23)
        });
        AddForm("sword", "w", "kirby-sword.png", new[] {
            new Rectangle(3, 101, 32, 30), new Rectangle(34, 101, 32, 30),
            new Rectangle(67, 101, 32, 30), new Rectangle(101, 101, 32, 30)
        }, new[] {
            new Rectangle(5, 321, 32, 28), new Rectangle(40, 321, 32, 28)
        }, new[] {
            new Rectangle(6, 174, 27, 30), new Rectangle(38, 180, 23, 23),
            new Rectangle(66, 180, 21, 27), new Rectangle(92, 178, 28, 25),
            new Rectangle(125, 168, 23, 36)
        }, new[] {
            new Rectangle(40, 34, 29, 28), new Rectangle(74, 34, 29, 28)
        });
        AddForm("wheel", "v", "kirby-wheel.png", new[] {
            new Rectangle(5, 121, 26, 28), new Rectangle(34, 121, 26, 28),
            new Rectangle(62, 121, 26, 28), new Rectangle(92, 121, 26, 28)
        }, new[] {
            new Rectangle(152, 204, 26, 25), new Rectangle(181, 204, 26, 25)
        }, new[] {
            new Rectangle(6, 203, 24, 28), new Rectangle(35, 206, 23, 24),
            new Rectangle(63, 207, 22, 25), new Rectangle(90, 204, 24, 26),
            new Rectangle(119, 203, 23, 28)
        }, new[] {
            new Rectangle(35, 37, 24, 27), new Rectangle(64, 37, 24, 27)
        });
        AddForm("wing", "g", "kirby-wing.png", new[] {
            new Rectangle(4, 127, 31, 32), new Rectangle(35, 127, 31, 32),
            new Rectangle(66, 127, 31, 32), new Rectangle(100, 127, 31, 32)
        }, new[] {
            new Rectangle(5, 448, 30, 29), new Rectangle(38, 448, 30, 29)
        }, new[] {
            new Rectangle(6, 214, 26, 31), new Rectangle(37, 220, 24, 23),
            new Rectangle(66, 222, 25, 30), new Rectangle(96, 217, 28, 27),
            new Rectangle(129, 212, 23, 32)
        }, new[] {
            new Rectangle(39, 37, 28, 32), new Rectangle(72, 37, 28, 32)
        });
        AddForm("yoyo", "y", "kirby-yoyo.png", new[] {
            new Rectangle(5, 121, 26, 28), new Rectangle(34, 121, 26, 28),
            new Rectangle(62, 121, 26, 28), new Rectangle(92, 121, 26, 28)
        }, new[] {
            new Rectangle(152, 204, 26, 25), new Rectangle(181, 204, 26, 25)
        }, new[] {
            new Rectangle(6, 203, 24, 28), new Rectangle(35, 206, 23, 24),
            new Rectangle(63, 207, 22, 25), new Rectangle(90, 204, 24, 26),
            new Rectangle(119, 203, 23, 28)
        }, new[] {
            new Rectangle(35, 37, 24, 27), new Rectangle(64, 37, 24, 27)
        });
    }

    private void AddForm(string name, string key, string file, Rectangle[] coords, Rectangle[] guardCoords, Rectangle[] jumpCoords, Rectangle[] standCoords)
    {
        FormDef def = new FormDef { Name = name, Key = key, FileName = file, Coords = coords, GuardCoords = guardCoords, JumpCoords = jumpCoords, StandCoords = standCoords };
        _forms.Add(def);
        _keyToForm[key] = def;
        if (name == "normal") _currentForm = def;
    }

    private static void ExpandBounds(Rectangle[] rects, ref int maxW, ref int maxH)
    {
        if (rects == null) return;
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].Right > maxW) maxW = rects[i].Right;
            if (rects[i].Bottom > maxH) maxH = rects[i].Bottom;
        }
    }

    private FastSheet OpenSpriteSheet(string fileName, int reqW, int reqH)
    {
        try
        {
            Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fileName);
            if (resStream != null)
            {
                return new FastSheet(resStream, new Bitmap(resStream), reqW, reqH);
            }
        }
        catch { }

        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(exeDir, "sprites", fileName);
        if (!File.Exists(path)) path = Path.Combine(exeDir, fileName);
        if (File.Exists(path)) return new FastSheet(null, new Bitmap(path), reqW, reqH);
        return null;
    }

    private static int MakePremulArgb(int a, int r, int g, int b)
    {
        if (a <= 0) return 0;
        if (a >= 255) return (255 << 24) | (r << 16) | (g << 8) | b;
        int pr = (r * a + 127) / 255;
        int pg = (g * a + 127) / 255;
        int pb = (b * a + 127) / 255;
        return (a << 24) | (pr << 16) | (pg << 8) | pb;
    }

    private static SpriteTile BuildSpriteTile(int[] scratchTile)
    {
        int minX = TILE_SIZE, maxX = -1, minY = TILE_SIZE, maxY = -1;
        for (int y = 0; y < TILE_SIZE; y++)
        {
            int row = y * TILE_SIZE;
            for (int x = 0; x < TILE_SIZE; x++)
            {
                if (scratchTile[row + x] != 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new SpriteTile { MinX = 0, MinY = 0, Width = 1, Height = 1, PremulPixels = new int[1] };
        }

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        int[] pixels = new int[w * h];
        for (int sy = 0; sy < h; sy++)
        {
            Array.Copy(scratchTile, (minY + sy) * TILE_SIZE + minX, pixels, sy * w, w);
        }
        return new SpriteTile { MinX = minX, MinY = minY, Width = w, Height = h, PremulPixels = pixels };
    }

    private static SpriteTile ExtractAlignedTile(FastSheet rawSheet, Rectangle r, int bg0, int bgCorner, int[] scratch, bool isGuard = false)
    {
        int maxY = r.Height - 1;
        for (int dy = 0; dy < r.Height; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                if (!IsBgColor(c, bg0, bgCorner)) maxY = dy;
            }
        }

        int feetMinX = r.Width, feetMaxX = 0;
        int startY = Math.Max(0, maxY - 3);
        for (int dy = startY; dy <= maxY; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                if (!IsBgColor(c, bg0, bgCorner))
                {
                    if ((!isGuard || dy >= maxY - 1) && dx < feetMinX) feetMinX = dx;
                    if (dx > feetMaxX) feetMaxX = dx;
                }
            }
        }

        int bodyPivotX = (feetMinX + feetMaxX) / 2;
        int destX = PIVOT_X - bodyPivotX;
        int destY = PIVOT_Y - maxY;

        Array.Clear(scratch, 0, scratch.Length);
        for (int dy = 0; dy < r.Height; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                if (!IsBgColor(c, bg0, bgCorner))
                {
                    int tx = destX + dx;
                    int ty = destY + dy;
                    if (tx >= 0 && tx < TILE_SIZE && ty >= 0 && ty < TILE_SIZE)
                    {
                        scratch[ty * TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                    }
                }
            }
        }
        return BuildSpriteTile(scratch);
    }

    private static SpriteTile ExtractMirrorGuardTile(FastSheet rawSheet, Rectangle rainRect, int bg0, int[] scratch)
    {
        Rectangle kirbyRect = new Rectangle(37, 880, 31, 27);
        Rectangle bubbleRect = new Rectangle(2, 877, 32, 34);
        int kirbyCorner = rawSheet.GetPixel(kirbyRect.X, kirbyRect.Y);
        int rainCorner = rawSheet.GetPixel(rainRect.X, rainRect.Y);
        int bubbleCorner = rawSheet.GetPixel(bubbleRect.X, bubbleRect.Y);

        // Place Guard Mirror Kirby at his exact floor-aligned position (bodyPivotX=17, maxY=25)
        // and center the 30x32 bubble symmetrically around him (extending 3px below his feet)
        int kirbyDestX = PIVOT_X - 17;
        int kirbyDestY = PIVOT_Y - 25;
        int sphereDestX = kirbyDestX;
        int sphereDestY = kirbyDestY - 4;

        Array.Clear(scratch, 0, scratch.Length);

        for (int dy = 0; dy < rainRect.Height; dy++)
        {
            for (int dx = 0; dx < rainRect.Width; dx++)
            {
                int rc = rawSheet.GetPixel(rainRect.X + dx, rainRect.Y + dy);
                if (IsBgColor(rc, bg0, rainCorner)) continue;

                int tx = sphereDestX + dx;
                int ty = sphereDestY + dy;
                if (tx < 0 || tx >= TILE_SIZE || ty < 0 || ty >= TILE_SIZE) continue;

                int kdx = tx - kirbyDestX;
                int kdy = ty - kirbyDestY;
                bool hasKirby = false;
                int kc = 0;
                if (kdx >= 0 && kdx < kirbyRect.Width && kdy >= 0 && kdy < kirbyRect.Height)
                {
                    kc = rawSheet.GetPixel(kirbyRect.X + kdx, kirbyRect.Y + kdy);
                    if (!IsBgColor(kc, bg0, kirbyCorner)) hasKirby = true;
                }

                int bc = rawSheet.GetPixel(bubbleRect.X + dx, bubbleRect.Y + dy);
                bool hasBubbleRim = !IsBgColor(bc, bg0, bubbleCorner);
                if (hasBubbleRim)
                {
                    int br = (bc >> 16) & 0xFF;
                    int bg = (bc >> 8) & 0xFF;
                    int bb = bc & 0xFF;
                    // Clean up orange/magenta pixels on the bubble rim into crisp sky-blue
                    if (br > 180 && bg < 200)
                    {
                        br = 80; bg = 180; bb = 248;
                    }
                    scratch[ty * TILE_SIZE + tx] = MakePremulArgb(245, br, bg, bb);
                }
                else
                {
                    int rr = (rc >> 16) & 0xFF;
                    int rg = (rc >> 8) & 0xFF;
                    int rb = rc & 0xFF;
                    // Smooth out the 3 pure-white stripe bands between Orange and Yellow into warm golden-amber
                    if (rr >= 248 && rg >= 248 && rb >= 248)
                    {
                        rr = 248; rg = 200; rb = 64;
                    }

                    if (hasKirby)
                    {
                        int kr = (kc >> 16) & 0xFF;
                        int kg = (kc >> 8) & 0xFF;
                        int kb = kc & 0xFF;
                        int nr = (int)(kr * 0.55 + rr * 0.45);
                        int ng = (int)(kg * 0.55 + rg * 0.45);
                        int nb = (int)(kb * 0.55 + rb * 0.45);
                        scratch[ty * TILE_SIZE + tx] = MakePremulArgb(255, nr, ng, nb);
                    }
                    else
                    {
                        scratch[ty * TILE_SIZE + tx] = MakePremulArgb(175, rr, rg, rb);
                    }
                }
            }
        }

        return BuildSpriteTile(scratch);
    }

    private static SpriteTile[] ExtractJumpTiles(FastSheet rawSheet, Rectangle[] jumpCoords, int bg0, int[] scratch)
    {
        if (jumpCoords == null || jumpCoords.Length == 0) return null;
        SpriteTile[] tiles = new SpriteTile[jumpCoords.Length];
        int refBottomY = jumpCoords[0].Bottom - 1;

        for (int i = 0; i < jumpCoords.Length; i++)
        {
            Rectangle r = jumpCoords[i];
            int minX = r.Width, maxX = 0;
            bool isFlip = (i == 1 || i == 2);
            int yStart = isFlip ? 0 : Math.Max(0, r.Height - 4);
            int yEnd = isFlip ? Math.Min(r.Height - 1, 10) : r.Height - 1;

            for (int dy = yStart; dy <= yEnd; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bg0))
                    {
                        if (dx < minX) minX = dx;
                        if (dx > maxX) maxX = dx;
                    }
                }
            }

            int bodyPivotX = (minX <= maxX) ? (minX + maxX) / 2 : r.Width / 2;
            int destX = PIVOT_X - bodyPivotX;
            int destY = PIVOT_Y - (refBottomY - r.Y);

            Array.Clear(scratch, 0, scratch.Length);
            for (int dy = 0; dy < r.Height; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bg0))
                    {
                        int tx = destX + dx;
                        int ty = destY + dy;
                        if (tx >= 0 && tx < TILE_SIZE && ty >= 0 && ty < TILE_SIZE)
                        {
                            scratch[ty * TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                        }
                    }
                }
            }
            tiles[i] = BuildSpriteTile(scratch);
        }
        return tiles;
    }

    private static SpriteTile ExtractSleepTile(FastSheet rawSheet, Rectangle r, int bg0, int[] scratch)
    {
        int bgCorner = rawSheet.GetPixel(r.X, r.Y);
        int destX = PIVOT_X - 18;
        int destY = PIVOT_Y - (r.Height - 1);

        Array.Clear(scratch, 0, scratch.Length);
        for (int dy = 0; dy < r.Height; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                int c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                if (!IsBgColor(c, bg0, bgCorner))
                {
                    int tx = destX + dx;
                    int ty = destY + dy;
                    if (tx >= 0 && tx < TILE_SIZE && ty >= 0 && ty < TILE_SIZE)
                    {
                        scratch[ty * TILE_SIZE + tx] = (255 << 24) | (c & 0x00FFFFFF);
                    }
                }
            }
        }
        return BuildSpriteTile(scratch);
    }

    private void ExtractTrayIconFromNormalSheet(FastSheet normalSheet, int bg0)
    {
        try
        {
            using (Bitmap crop = new Bitmap(24, 24, PixelFormat.Format32bppArgb))
            {
                int bgRgb = bg0 & 0x00FFFFFF;
                for (int y = 0; y < 24; y++)
                {
                    for (int x = 0; x < 24; x++)
                    {
                        int c = normalSheet.GetPixel(32 + x, 125 + y);
                        if ((c & 0x00FFFFFF) == bgRgb)
                            crop.SetPixel(x, y, Color.Transparent);
                        else
                            crop.SetPixel(x, y, Color.FromArgb(255, (c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF));
                    }
                }
                _trayIconHandle = crop.GetHicon();
                _loadedTrayIcon = Icon.FromHandle(_trayIconHandle);
            }
        }
        catch { }
    }

    private void LoadAllSpritesheets()
    {
        int[] scratch = new int[TILE_SIZE * TILE_SIZE];

        foreach (FormDef form in _forms)
        {
            int reqW = 64, reqH = 160;
            ExpandBounds(form.Coords, ref reqW, ref reqH);
            ExpandBounds(form.GuardCoords, ref reqW, ref reqH);
            ExpandBounds(form.JumpCoords, ref reqW, ref reqH);
            ExpandBounds(form.StandCoords, ref reqW, ref reqH);

            using (FastSheet rawSheet = OpenSpriteSheet(form.FileName, reqW, reqH))
            {
                if (rawSheet == null) continue;
                int bg0 = rawSheet.GetPixel(0, 0);
                int bgCorner = rawSheet.GetPixel(form.Coords[0].X, form.Coords[0].Y);

                if (form.Name == "normal" && _loadedTrayIcon == null)
                {
                    ExtractTrayIconFromNormalSheet(rawSheet, bg0);
                }

                SpriteTile[] base4 = new SpriteTile[form.Coords.Length];
                for (int i = 0; i < form.Coords.Length; i++)
                {
                    base4[i] = ExtractAlignedTile(rawSheet, form.Coords[i], bg0, bgCorner, scratch);
                }

                // 6-frame ping-pong walk cycle [0, 1, 2, 3, 2, 1] reusing tile references
                form.Frames = new SpriteTile[] { base4[0], base4[1], base4[2], base4[3], base4[2], base4[1] };

                if (form.GuardCoords != null && form.GuardCoords.Length > 0)
                {
                    form.GuardFrames = new SpriteTile[form.GuardCoords.Length];
                    for (int i = 0; i < form.GuardCoords.Length; i++)
                    {
                        Rectangle gr = form.GuardCoords[i];
                        if (form.Name == "mirror")
                        {
                            form.GuardFrames[i] = ExtractMirrorGuardTile(rawSheet, gr, bg0, scratch);
                        }
                        else
                        {
                            int gCorner = rawSheet.GetPixel(gr.X, gr.Y);
                            form.GuardFrames[i] = ExtractAlignedTile(rawSheet, gr, bg0, gCorner, scratch, true);
                        }
                    }
                }

                if (form.JumpCoords != null && form.JumpCoords.Length > 0)
                {
                    form.JumpFrames = ExtractJumpTiles(rawSheet, form.JumpCoords, bg0, scratch);
                }

                if (form.StandCoords != null && form.StandCoords.Length > 0)
                {
                    form.StandFrames = new SpriteTile[form.StandCoords.Length];
                    for (int i = 0; i < form.StandCoords.Length; i++)
                    {
                        Rectangle sr = form.StandCoords[i];
                        int sCorner = rawSheet.GetPixel(sr.X, sr.Y);
                        form.StandFrames[i] = ExtractAlignedTile(rawSheet, sr, bg0, sCorner, scratch);
                    }
                }
            }
        }

        int sleepW = 1, sleepH = 1;
        ExpandBounds(_sleepCoords, ref sleepW, ref sleepH);
        using (FastSheet sleepSheet = OpenSpriteSheet("kirby-sleep.png", sleepW, sleepH))
        {
            if (sleepSheet != null)
            {
                int bg0 = sleepSheet.GetPixel(0, 0);
                _sleepFrames = new SpriteTile[_sleepCoords.Length];
                for (int i = 0; i < _sleepCoords.Length; i++)
                {
                    _sleepFrames[i] = ExtractSleepTile(sleepSheet, _sleepCoords[i], bg0, scratch);
                }
            }
        }
    }

    private static bool IsBgColor(int c, int bg0, int bgCorner)
    {
        if (((c >> 24) & 0xFF) == 0) return true;
        int rgb = c & 0x00FFFFFF;
        if (rgb == (bg0 & 0x00FFFFFF)) return true;
        if (rgb == (bgCorner & 0x00FFFFFF)) return true;
        if (rgb == 0x00709AD1) return true; // (112, 154, 209)
        if (rgb == 0x009696FF) return true; // (150, 150, 255)
        return false;
    }

    private void SetScale(int newScale)
    {
        if (_scale == newScale) return;
        _scale = newScale;
        _scale2xItem.Checked = (_scale == 2);
        _scale3xItem.Checked = (_scale == 3);
        _scale4xItem.Checked = (_scale == 4);
        _lastRenderedTile = null;
        TrimMemory();
    }

    private static void TrimMemory()
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

    private void SetSleepTimeout(double seconds)
    {
        _idleSleepSeconds = seconds;
        if (_sleepMenuItems != null && _sleepTimeoutValues != null)
        {
            for (int i = 0; i < _sleepMenuItems.Length; i++)
            {
                _sleepMenuItems[i].Checked = (_sleepTimeoutValues[i] == _idleSleepSeconds);
            }
        }
        if (_idleSleepSeconds < 0)
        {
            if (_sleepState == SleepState.Halting)
            {
                _sleepState = SleepState.Awake;
                _sleepAnimTime = 0.0;
            }
            else if (_sleepState == SleepState.FallingAsleep || _sleepState == SleepState.Sleeping)
            {
                _sleepState = SleepState.WakingUp;
                _sleepAnimTime = 0.0;
            }
        }
    }

    private void NotifyUserActivity()
    {
        if (_stopwatch != null)
        {
            _lastInputMs = _stopwatch.ElapsedMilliseconds;
        }
        if (_sleepState == SleepState.Halting)
        {
            _sleepState = SleepState.Awake;
            _sleepAnimTime = 0.0;
        }
        else if (_sleepState == SleepState.FallingAsleep || _sleepState == SleepState.Sleeping)
        {
            _sleepState = SleepState.WakingUp;
            _sleepAnimTime = 0.0;
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        long nowMs = _stopwatch.ElapsedMilliseconds;
        double dtSeconds = (nowMs - _lastTickMs) / 1000.0;
        _lastTickMs = nowMs;
        if (dtSeconds <= 0 || dtSeconds > 0.25) dtSeconds = 0.01667;

        Point mouse = Cursor.Position;
        bool hasInput = false;
        if (mouse.X != _lastMousePos.X || mouse.Y != _lastMousePos.Y)
        {
            _lastMousePos = mouse;
            hasInput = true;
        }

        LASTINPUTINFO lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        if (GetLastInputInfo(ref lii) && lii.dwTime != _lastSysInputTick)
        {
            _lastSysInputTick = lii.dwTime;
            hasInput = true;
        }

        if (hasInput)
        {
            NotifyUserActivity();
        }
        else if (_idleSleepSeconds > 0 && _sleepState == SleepState.Awake && !_isJumping && _sleepFrames != null && _sleepFrames.Length >= 8)
        {
            double idleSeconds = (nowMs - _lastInputMs) / 1000.0;
            if (idleSeconds >= _idleSleepSeconds)
            {
                bool hasStandFrames = (_currentForm != null && _currentForm.StandFrames != null && _currentForm.StandFrames.Length >= 2);
                _sleepState = hasStandFrames ? SleepState.Halting : SleepState.FallingAsleep;
                _sleepAnimTime = 0.0;
            }
        }

        _envCheckCounter++;
        if (_envCheckCounter >= 15)
        {
            _envCheckCounter = 0;
            _cachedWorkArea = Screen.PrimaryScreen.WorkingArea;
            _isFullscreenCached = IsFullscreenAppActive(_cachedWorkArea);
        }

        if (!_overlayEnabled || _isFullscreenCached)
        {
            if (_overlay.Visible) _overlay.Hide();
            return;
        }

        if (!_overlay.Visible) _overlay.Show();

        Rectangle workArea = _cachedWorkArea;
        int halfWidth = (TILE_SIZE / 2) * _scale;
        double delta60 = dtSeconds * 60.0;

        SpriteTile frameToRender = null;

        if (_sleepState != SleepState.Awake && _sleepFrames != null && _sleepFrames.Length >= 8)
        {
            if (_sleepState == SleepState.Halting)
            {
                _sleepAnimTime += 0.05 * delta60;
                int seqIdx = (int)Math.Floor(_sleepAnimTime);
                if (seqIdx >= _haltStandSeq.Length || _currentForm == null || _currentForm.StandFrames == null || _currentForm.StandFrames.Length < 2)
                {
                    _sleepState = SleepState.FallingAsleep;
                    _sleepAnimTime = 0.0;
                    frameToRender = _sleepFrames[_fallAsleepSeq[0]];
                }
                else
                {
                    frameToRender = _currentForm.StandFrames[_haltStandSeq[seqIdx]];
                }
            }
            else if (_sleepState == SleepState.FallingAsleep)
            {
                _sleepAnimTime += 0.05 * delta60;
                int seqIdx = (int)Math.Floor(_sleepAnimTime);
                if (seqIdx >= _fallAsleepSeq.Length)
                {
                    _sleepState = SleepState.Sleeping;
                    _sleepAnimTime = 0.0;
                    frameToRender = _sleepFrames[_sleepLoopSeq[0]];
                }
                else
                {
                    frameToRender = _sleepFrames[_fallAsleepSeq[seqIdx]];
                }
            }
            else if (_sleepState == SleepState.Sleeping)
            {
                _sleepAnimTime += 0.045 * delta60;
                int seqIdx = ((int)Math.Floor(_sleepAnimTime)) % _sleepLoopSeq.Length;
                if (seqIdx < 0) seqIdx += _sleepLoopSeq.Length;
                frameToRender = _sleepFrames[_sleepLoopSeq[seqIdx]];
            }
            else if (_sleepState == SleepState.WakingUp)
            {
                _sleepAnimTime += 0.09 * delta60;
                int seqIdx = (int)Math.Floor(_sleepAnimTime);
                if (seqIdx >= _wakeUpSeq.Length)
                {
                    if (_currentForm != null && _currentForm.StandFrames != null && _currentForm.StandFrames.Length >= 2)
                    {
                        _sleepState = SleepState.PostWakeStanding;
                        _sleepAnimTime = 0.0;
                        frameToRender = _currentForm.StandFrames[_postWakeStandSeq[0]];
                    }
                    else
                    {
                        _sleepState = SleepState.Awake;
                        _sleepAnimTime = 0.0;
                    }
                }
                else
                {
                    frameToRender = _sleepFrames[_wakeUpSeq[seqIdx]];
                }
            }
            else if (_sleepState == SleepState.PostWakeStanding)
            {
                _sleepAnimTime += 0.06 * delta60;
                int seqIdx = (int)Math.Floor(_sleepAnimTime);
                if (seqIdx >= _postWakeStandSeq.Length || _currentForm == null || _currentForm.StandFrames == null || _currentForm.StandFrames.Length < 2)
                {
                    _sleepState = SleepState.Awake;
                    _sleepAnimTime = 0.0;
                }
                else
                {
                    frameToRender = _currentForm.StandFrames[_postWakeStandSeq[seqIdx]];
                }
            }
        }

        if (_sleepState == SleepState.Awake)
        {
            // Check if mouse pointer is close to Kirby's body on the taskbar (only guards when on the ground)
            double kirbyCenterY = workArea.Bottom - (16 * _scale);
            double distX = Math.Abs(mouse.X - _kirbyX);
            double distY = Math.Abs(mouse.Y - kirbyCenterY);
            bool isMouseNear = !_isJumping && (distX <= 24 * _scale) && (distY <= 28 * _scale);

            if (_isJumping)
            {
                _kirbyX += 1.5 * delta60;
                _jumpY += _jumpVelY * delta60;
                _jumpVelY -= JUMP_GRAVITY * delta60;
                if (_jumpY <= 0.0)
                {
                    _jumpY = 0.0;
                    _jumpVelY = 0.0;
                    _isJumping = false;
                }
            }
            else if (!isMouseNear)
            {
                _kirbyX += 1.5 * delta60;
                _animTime += 0.1 * delta60;
            }
            else
            {
                // Match SNES Kirby Super Star guard flash / shield shimmer speed (4 frames per state @ 60Hz = 15 FPS)
                _animTime += 0.25 * delta60;
            }

            if (_kirbyX > workArea.Right + halfWidth)
            {
                _kirbyX = workArea.Left - halfWidth;
            }

            if (_currentForm == null || _currentForm.Frames == null) return;

            bool useJump = _isJumping && _currentForm.JumpFrames != null && _currentForm.JumpFrames.Length >= 5;
            bool useGuard = !useJump && isMouseNear && _currentForm.GuardFrames != null && _currentForm.GuardFrames.Length > 0;
            SpriteTile[] activeFrames = useJump ? _currentForm.JumpFrames : (useGuard ? _currentForm.GuardFrames : _currentForm.Frames);

            int frameIdx;
            if (useJump)
            {
                if (_jumpVelY > 1.2) frameIdx = 0;       // Rising
                else if (_jumpVelY > 0.0) frameIdx = 1;  // Apex somersault 1
                else if (_jumpVelY > -1.2) frameIdx = 2; // Apex somersault 2
                else if (_jumpVelY > -2.6) frameIdx = 3; // Early fall
                else frameIdx = 4;                       // Full fall descent
            }
            else
            {
                int frameCount = activeFrames.Length;
                frameIdx = ((int)Math.Floor(_animTime)) % frameCount;
                if (frameIdx < 0) frameIdx += frameCount;
            }

            frameToRender = activeFrames[frameIdx];
        }

        if (frameToRender == null) return;

        int screenX = (int)Math.Round(_kirbyX) - halfWidth;
        int screenY = workArea.Bottom - (PIVOT_Y + 1) * _scale - (int)Math.Round(_jumpY * _scale);

        _topmostRefreshCounter++;
        if (_topmostRefreshCounter >= 60)
        {
            _topmostRefreshCounter = 0;
            SetWindowPos(_overlay.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        if (screenX != _lastRenderedX || screenY != _lastRenderedY || frameToRender != _lastRenderedTile)
        {
            _overlay.UpdateSpriteFrame(frameToRender, _scale, screenX, screenY);
            _lastRenderedX = screenX;
            _lastRenderedY = screenY;
            _lastRenderedTile = frameToRender;
        }
    }

    private bool IsFullscreenAppActive(Rectangle work)
    {
        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == _overlay.Handle) return false;

            _classNameSb.Length = 0;
            GetClassName(fg, _classNameSb, _classNameSb.Capacity);
            string cls = _classNameSb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                return false;

            RECT rect;
            if (GetWindowRect(fg, out rect))
            {
                Rectangle screen = Screen.PrimaryScreen.Bounds;
                if (rect.Left <= screen.Left && rect.Top <= screen.Top &&
                    rect.Right >= screen.Right && rect.Bottom >= screen.Bottom &&
                    rect.Bottom > work.Bottom)
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private void HandleKeySwitch(string keyName)
    {
        if (keyName == null) return;
        if (keyName == " ")
        {
            if (!_isJumping)
            {
                _isJumping = true;
                _jumpY = 0.0;
                _jumpVelY = JUMP_INIT_VEL;
            }
            return;
        }

        FormDef target;
        if (_keyToForm.TryGetValue(keyName, out target))
        {
            FormDef normalForm = _keyToForm["0"];
            FormDef nextForm = (_currentForm == target && target != normalForm) ? normalForm : target;
            if (nextForm != null && nextForm != _currentForm)
            {
                _currentForm = nextForm;
            }
        }
    }

    private Icon LoadKirbyTrayIcon()
    {
        if (_loadedTrayIcon != null) return _loadedTrayIcon;
        return SystemIcons.Application;
    }

    private void OnToggleOverlay(object sender, EventArgs e)
    {
        _overlayEnabled = !_overlayEnabled;
        _overlayItem.Checked = _overlayEnabled;
    }

    private void OnTogglePause(object sender, EventArgs e)
    {
        _isPaused = !_isPaused;
        _pauseItem.Checked = _isPaused;
        _trayIcon.Text = _isPaused ? "Kirby Taskbar Buddy (Paused)" : "Kirby Taskbar Buddy";
    }

    private bool IsStartupEnabled()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, false))
            {
                return key != null && (key.GetValue(APP_NAME) != null || key.GetValue(LEGACY_APP_NAME) != null);
            }
        }
        catch { return false; }
    }

    public static void SetStartup(bool enable)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, true))
            {
                if (key != null)
                {
                    try { key.DeleteValue(LEGACY_APP_NAME, false); } catch { }

                    if (enable)
                    {
                        string exePath = Assembly.GetExecutingAssembly().Location;
                        key.SetValue(APP_NAME, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(APP_NAME, false);
                    }
                }
            }
        }
        catch { }
    }

    private void OnToggleStartup(object sender, EventArgs e)
    {
        bool nextState = !_startupItem.Checked;
        SetStartup(nextState);
        _startupItem.Checked = IsStartupEnabled();
    }

    private void OnExit(object sender, EventArgs e)
    {
        _animTimer.Stop();
        if (_overlay != null) _overlay.Close();
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
        _trayIcon.Visible = false;
        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }
        Application.Exit();
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _instance != null)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);

            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                bool wasSleeping = (_instance._sleepState != SleepState.Awake);
                _instance.NotifyUserActivity();
                if (_downKeys.Add(vkCode) && !_instance._isPaused && !wasSleeping)
                {
                    string keyName = VkCodeToName(vkCode);
                    if (keyName != null) _instance.HandleKeySwitch(keyName);
                }
            }
            else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
            {
                _downKeys.Remove(vkCode);
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static string VkCodeToName(int vk)
    {
        switch (vk)
        {
            case 0x30:
            case 0x60: return "0";
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
            case 0x20: return " ";
            default:   return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);
}
