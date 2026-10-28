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

    public void UpdateSpriteFrame(Bitmap frameBmp, int screenX, int screenY)
    {
        if (IsDisposed || !IsHandleCreated || frameBmp == null) return;

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            hBitmap = frameBmp.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memDc, hBitmap);

            SIZE size = new SIZE { cx = frameBmp.Width, cy = frameBmp.Height };
            POINT pointSource = new POINT { x = 0, y = 0 };
            POINT topPos = new POINT { x = screenX, y = screenY };
            BLENDFUNCTION blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA
            };

            UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
            if (hBitmap != IntPtr.Zero)
            {
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
            }
            DeleteDC(memDc);
        }
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
        public Bitmap[] RawFrames64;
        public Bitmap[] ScaledFrames;
        public Bitmap[] RawGuardFrames64;
        public Bitmap[] ScaledGuardFrames;
        public Bitmap[] RawJumpFrames64;
        public Bitmap[] ScaledJumpFrames;
        public Bitmap[] RawStandFrames64;
        public Bitmap[] ScaledStandFrames;
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

    private Bitmap[] _rawSleepFrames64;
    private Bitmap[] _scaledSleepFrames;
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
    private Bitmap _lastRenderedBitmap = null;
    private int _topmostRefreshCounter = 0;

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

    private Bitmap OpenSpriteBitmap(string fileName)
    {
        try
        {
            Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fileName);
            if (resStream != null)
            {
                return new Bitmap(resStream);
            }
        }
        catch { }

        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(exeDir, "sprites", fileName);
        if (!File.Exists(path)) path = Path.Combine(exeDir, fileName);
        if (File.Exists(path)) return new Bitmap(path);
        return null;
    }

    private static Bitmap ExtractAlignedTile(Bitmap rawSheet, Rectangle r, Color bg0, Color bgCorner, bool isGuard = false)
    {
        int maxY = r.Height - 1;
        for (int dy = 0; dy < r.Height; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                Color c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                if (!IsBgColor(c, bg0, bgCorner)) maxY = dy;
            }
        }

        int feetMinX = r.Width, feetMaxX = 0;
        int startY = Math.Max(0, maxY - 3);
        for (int dy = startY; dy <= maxY; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                Color c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
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

        Bitmap tile64 = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format32bppPArgb);
        for (int dy = 0; dy < r.Height; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                Color c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                if (!IsBgColor(c, bg0, bgCorner))
                {
                    int tx = destX + dx;
                    int ty = destY + dy;
                    if (tx >= 0 && tx < TILE_SIZE && ty >= 0 && ty < TILE_SIZE)
                    {
                        tile64.SetPixel(tx, ty, Color.FromArgb(255, c.R, c.G, c.B));
                    }
                }
            }
        }
        return tile64;
    }

    private static Bitmap ExtractMirrorGuardTile(Bitmap rawSheet, Rectangle rainRect, Color bg0)
    {
        Rectangle kirbyRect = new Rectangle(37, 880, 31, 27);
        Rectangle bubbleRect = new Rectangle(2, 877, 32, 34);
        Color kirbyCorner = rawSheet.GetPixel(kirbyRect.X, kirbyRect.Y);
        Color rainCorner = rawSheet.GetPixel(rainRect.X, rainRect.Y);
        Color bubbleCorner = rawSheet.GetPixel(bubbleRect.X, bubbleRect.Y);

        // Place Guard Mirror Kirby at his exact floor-aligned position (bodyPivotX=17, maxY=25)
        // and center the 30x32 bubble symmetrically around him (extending 3px below his feet)
        int kirbyDestX = PIVOT_X - 17;
        int kirbyDestY = PIVOT_Y - 25;
        int sphereDestX = kirbyDestX;
        int sphereDestY = kirbyDestY - 4;

        Bitmap tile64 = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format32bppPArgb);

        for (int dy = 0; dy < rainRect.Height; dy++)
        {
            for (int dx = 0; dx < rainRect.Width; dx++)
            {
                Color rc = rawSheet.GetPixel(rainRect.X + dx, rainRect.Y + dy);
                if (IsBgColor(rc, bg0, rainCorner)) continue;

                int tx = sphereDestX + dx;
                int ty = sphereDestY + dy;
                if (tx < 0 || tx >= TILE_SIZE || ty < 0 || ty >= TILE_SIZE) continue;

                int kdx = tx - kirbyDestX;
                int kdy = ty - kirbyDestY;
                bool hasKirby = false;
                Color kc = Color.Empty;
                if (kdx >= 0 && kdx < kirbyRect.Width && kdy >= 0 && kdy < kirbyRect.Height)
                {
                    kc = rawSheet.GetPixel(kirbyRect.X + kdx, kirbyRect.Y + kdy);
                    if (!IsBgColor(kc, bg0, kirbyCorner)) hasKirby = true;
                }

                Color bc = rawSheet.GetPixel(bubbleRect.X + dx, bubbleRect.Y + dy);
                bool hasBubbleRim = !IsBgColor(bc, bg0, bubbleCorner);
                if (hasBubbleRim)
                {
                    // Clean up orange/magenta pixels on the bubble rim into crisp sky-blue
                    if (bc.R > 180 && bc.G < 200)
                    {
                        bc = Color.FromArgb(255, 80, 180, 248);
                    }
                    tile64.SetPixel(tx, ty, Color.FromArgb(245, bc.R, bc.G, bc.B));
                }
                else
                {
                    // Smooth out the 3 pure-white stripe bands between Orange and Yellow into warm golden-amber
                    if (rc.R >= 248 && rc.G >= 248 && rc.B >= 248)
                    {
                        rc = Color.FromArgb(255, 248, 200, 64);
                    }

                    if (hasKirby)
                    {
                        int nr = (int)(kc.R * 0.55 + rc.R * 0.45);
                        int ng = (int)(kc.G * 0.55 + rc.G * 0.45);
                        int nb = (int)(kc.B * 0.55 + rc.B * 0.45);
                        tile64.SetPixel(tx, ty, Color.FromArgb(255, nr, ng, nb));
                    }
                    else
                    {
                        tile64.SetPixel(tx, ty, Color.FromArgb(175, rc.R, rc.G, rc.B));
                    }
                }
            }
        }

        return tile64;
    }

    private static Bitmap[] ExtractJumpTiles(Bitmap rawSheet, Rectangle[] jumpCoords, Color bg0)
    {
        if (jumpCoords == null || jumpCoords.Length == 0) return null;
        Bitmap[] tiles = new Bitmap[jumpCoords.Length];
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
                    Color c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
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

            Bitmap tile64 = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format32bppPArgb);
            for (int dy = 0; dy < r.Height; dy++)
            {
                for (int dx = 0; dx < r.Width; dx++)
                {
                    Color c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                    if (!IsBgColor(c, bg0, bg0))
                    {
                        int tx = destX + dx;
                        int ty = destY + dy;
                        if (tx >= 0 && tx < TILE_SIZE && ty >= 0 && ty < TILE_SIZE)
                        {
                            tile64.SetPixel(tx, ty, Color.FromArgb(255, c.R, c.G, c.B));
                        }
                    }
                }
            }
            tiles[i] = tile64;
        }
        return tiles;
    }

    private static Bitmap ExtractSleepTile(Bitmap rawSheet, Rectangle r, Color bg0)
    {
        Color bgCorner = rawSheet.GetPixel(r.X, r.Y);
        int destX = PIVOT_X - 18;
        int destY = PIVOT_Y - (r.Height - 1);

        Bitmap tile64 = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format32bppPArgb);
        for (int dy = 0; dy < r.Height; dy++)
        {
            for (int dx = 0; dx < r.Width; dx++)
            {
                Color c = rawSheet.GetPixel(r.X + dx, r.Y + dy);
                if (!IsBgColor(c, bg0, bgCorner))
                {
                    int tx = destX + dx;
                    int ty = destY + dy;
                    if (tx >= 0 && tx < TILE_SIZE && ty >= 0 && ty < TILE_SIZE)
                    {
                        tile64.SetPixel(tx, ty, Color.FromArgb(255, c.R, c.G, c.B));
                    }
                }
            }
        }
        return tile64;
    }

    private void LoadAllSpritesheets()
    {
        foreach (FormDef form in _forms)
        {
            using (Bitmap rawSheet = OpenSpriteBitmap(form.FileName))
            {
                if (rawSheet == null) continue;
                Color bg0 = rawSheet.GetPixel(0, 0);
                Color bgCorner = rawSheet.GetPixel(form.Coords[0].X, form.Coords[0].Y);

                Bitmap[] base4 = new Bitmap[form.Coords.Length];
                for (int i = 0; i < form.Coords.Length; i++)
                {
                    base4[i] = ExtractAlignedTile(rawSheet, form.Coords[i], bg0, bgCorner);
                }

                // 6-frame ping-pong walk cycle [0, 1, 2, 3, 2, 1]
                form.RawFrames64 = new Bitmap[] { base4[0], base4[1], base4[2], base4[3], base4[2], base4[1] };

                if (form.GuardCoords != null && form.GuardCoords.Length > 0)
                {
                    form.RawGuardFrames64 = new Bitmap[form.GuardCoords.Length];
                    for (int i = 0; i < form.GuardCoords.Length; i++)
                    {
                        Rectangle gr = form.GuardCoords[i];
                        if (form.Name == "mirror")
                        {
                            form.RawGuardFrames64[i] = ExtractMirrorGuardTile(rawSheet, gr, bg0);
                        }
                        else
                        {
                            Color gCorner = rawSheet.GetPixel(gr.X, gr.Y);
                            form.RawGuardFrames64[i] = ExtractAlignedTile(rawSheet, gr, bg0, gCorner, true);
                        }
                    }
                }

                if (form.JumpCoords != null && form.JumpCoords.Length > 0)
                {
                    form.RawJumpFrames64 = ExtractJumpTiles(rawSheet, form.JumpCoords, bg0);
                }

                if (form.StandCoords != null && form.StandCoords.Length > 0)
                {
                    form.RawStandFrames64 = new Bitmap[form.StandCoords.Length];
                    for (int i = 0; i < form.StandCoords.Length; i++)
                    {
                        Rectangle sr = form.StandCoords[i];
                        Color sCorner = rawSheet.GetPixel(sr.X, sr.Y);
                        form.RawStandFrames64[i] = ExtractAlignedTile(rawSheet, sr, bg0, sCorner);
                    }
                }
            }
        }

        using (Bitmap sleepSheet = OpenSpriteBitmap("kirby-sleep.png"))
        {
            if (sleepSheet != null)
            {
                Color bg0 = sleepSheet.GetPixel(0, 0);
                _rawSleepFrames64 = new Bitmap[_sleepCoords.Length];
                for (int i = 0; i < _sleepCoords.Length; i++)
                {
                    _rawSleepFrames64[i] = ExtractSleepTile(sleepSheet, _sleepCoords[i], bg0);
                }
            }
        }

        RebuildScaledFrames();
    }

    private static Bitmap[] ScaleTileArray(Bitmap[] rawTiles, int targetSize)
    {
        if (rawTiles == null) return null;
        Bitmap[] scaledArr = new Bitmap[rawTiles.Length];
        for (int i = 0; i < rawTiles.Length; i++)
        {
            Bitmap scaled = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;
                g.DrawImage(rawTiles[i], new Rectangle(0, 0, targetSize, targetSize));
            }
            scaledArr[i] = scaled;
        }
        return scaledArr;
    }

    private void RebuildScaledFrames()
    {
        int targetSize = TILE_SIZE * _scale;
        foreach (FormDef form in _forms)
        {
            if (form.RawFrames64 == null) continue;
            if (form.ScaledFrames != null)
            {
                foreach (Bitmap old in form.ScaledFrames) if (old != null) old.Dispose();
            }
            if (form.ScaledGuardFrames != null)
            {
                foreach (Bitmap old in form.ScaledGuardFrames) if (old != null) old.Dispose();
            }
            if (form.ScaledJumpFrames != null)
            {
                foreach (Bitmap old in form.ScaledJumpFrames) if (old != null) old.Dispose();
            }
            if (form.ScaledStandFrames != null)
            {
                foreach (Bitmap old in form.ScaledStandFrames) if (old != null) old.Dispose();
            }
            form.ScaledFrames = ScaleTileArray(form.RawFrames64, targetSize);
            form.ScaledGuardFrames = ScaleTileArray(form.RawGuardFrames64, targetSize);
            form.ScaledJumpFrames = ScaleTileArray(form.RawJumpFrames64, targetSize);
            form.ScaledStandFrames = ScaleTileArray(form.RawStandFrames64, targetSize);
        }

        if (_rawSleepFrames64 != null)
        {
            if (_scaledSleepFrames != null)
            {
                foreach (Bitmap old in _scaledSleepFrames) if (old != null) old.Dispose();
            }
            _scaledSleepFrames = ScaleTileArray(_rawSleepFrames64, targetSize);
        }

        _lastRenderedBitmap = null;
    }

    private static bool IsBgColor(Color c, Color bg0, Color bgCorner)
    {
        if (c.A == 0) return true;
        if (c.R == bg0.R && c.G == bg0.G && c.B == bg0.B) return true;
        if (c.R == bgCorner.R && c.G == bgCorner.G && c.B == bgCorner.B) return true;
        if (c.R == 112 && c.G == 154 && c.B == 209) return true;
        if (c.R == 150 && c.G == 150 && c.B == 255) return true;
        return false;
    }

    private void SetScale(int newScale)
    {
        if (_scale == newScale) return;
        _scale = newScale;
        _scale2xItem.Checked = (_scale == 2);
        _scale3xItem.Checked = (_scale == 3);
        _scale4xItem.Checked = (_scale == 4);
        RebuildScaledFrames();
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
        else if (_idleSleepSeconds > 0 && _sleepState == SleepState.Awake && !_isJumping && _scaledSleepFrames != null && _scaledSleepFrames.Length >= 8)
        {
            double idleSeconds = (nowMs - _lastInputMs) / 1000.0;
            if (idleSeconds >= _idleSleepSeconds)
            {
                bool hasStandFrames = (_currentForm != null && _currentForm.ScaledStandFrames != null && _currentForm.ScaledStandFrames.Length >= 2);
                _sleepState = hasStandFrames ? SleepState.Halting : SleepState.FallingAsleep;
                _sleepAnimTime = 0.0;
            }
        }

        if (!_overlayEnabled || IsFullscreenAppActive())
        {
            if (_overlay.Visible) _overlay.Hide();
            return;
        }

        if (!_overlay.Visible) _overlay.Show();

        Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
        int spriteSize = TILE_SIZE * _scale;
        int halfWidth = (TILE_SIZE / 2) * _scale;
        double delta60 = dtSeconds * 60.0;

        Bitmap frameToRender = null;

        if (_sleepState != SleepState.Awake && _scaledSleepFrames != null && _scaledSleepFrames.Length >= 8)
        {
            if (_sleepState == SleepState.Halting)
            {
                _sleepAnimTime += 0.05 * delta60;
                int seqIdx = (int)Math.Floor(_sleepAnimTime);
                if (seqIdx >= _haltStandSeq.Length || _currentForm == null || _currentForm.ScaledStandFrames == null || _currentForm.ScaledStandFrames.Length < 2)
                {
                    _sleepState = SleepState.FallingAsleep;
                    _sleepAnimTime = 0.0;
                    frameToRender = _scaledSleepFrames[_fallAsleepSeq[0]];
                }
                else
                {
                    frameToRender = _currentForm.ScaledStandFrames[_haltStandSeq[seqIdx]];
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
                    frameToRender = _scaledSleepFrames[_sleepLoopSeq[0]];
                }
                else
                {
                    frameToRender = _scaledSleepFrames[_fallAsleepSeq[seqIdx]];
                }
            }
            else if (_sleepState == SleepState.Sleeping)
            {
                _sleepAnimTime += 0.045 * delta60;
                int seqIdx = ((int)Math.Floor(_sleepAnimTime)) % _sleepLoopSeq.Length;
                if (seqIdx < 0) seqIdx += _sleepLoopSeq.Length;
                frameToRender = _scaledSleepFrames[_sleepLoopSeq[seqIdx]];
            }
            else if (_sleepState == SleepState.WakingUp)
            {
                _sleepAnimTime += 0.09 * delta60;
                int seqIdx = (int)Math.Floor(_sleepAnimTime);
                if (seqIdx >= _wakeUpSeq.Length)
                {
                    if (_currentForm != null && _currentForm.ScaledStandFrames != null && _currentForm.ScaledStandFrames.Length >= 2)
                    {
                        _sleepState = SleepState.PostWakeStanding;
                        _sleepAnimTime = 0.0;
                        frameToRender = _currentForm.ScaledStandFrames[_postWakeStandSeq[0]];
                    }
                    else
                    {
                        _sleepState = SleepState.Awake;
                        _sleepAnimTime = 0.0;
                    }
                }
                else
                {
                    frameToRender = _scaledSleepFrames[_wakeUpSeq[seqIdx]];
                }
            }
            else if (_sleepState == SleepState.PostWakeStanding)
            {
                _sleepAnimTime += 0.06 * delta60;
                int seqIdx = (int)Math.Floor(_sleepAnimTime);
                if (seqIdx >= _postWakeStandSeq.Length || _currentForm == null || _currentForm.ScaledStandFrames == null || _currentForm.ScaledStandFrames.Length < 2)
                {
                    _sleepState = SleepState.Awake;
                    _sleepAnimTime = 0.0;
                }
                else
                {
                    frameToRender = _currentForm.ScaledStandFrames[_postWakeStandSeq[seqIdx]];
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

            if (_currentForm == null || _currentForm.ScaledFrames == null) return;

            bool useJump = _isJumping && _currentForm.ScaledJumpFrames != null && _currentForm.ScaledJumpFrames.Length >= 5;
            bool useGuard = !useJump && isMouseNear && _currentForm.ScaledGuardFrames != null && _currentForm.ScaledGuardFrames.Length > 0;
            Bitmap[] activeFrames = useJump ? _currentForm.ScaledJumpFrames : (useGuard ? _currentForm.ScaledGuardFrames : _currentForm.ScaledFrames);

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

        if (screenX != _lastRenderedX || screenY != _lastRenderedY || frameToRender != _lastRenderedBitmap)
        {
            _overlay.UpdateSpriteFrame(frameToRender, screenX, screenY);
            _lastRenderedX = screenX;
            _lastRenderedY = screenY;
            _lastRenderedBitmap = frameToRender;
        }
    }

    private bool IsFullscreenAppActive()
    {
        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == _overlay.Handle) return false;

            StringBuilder sb = new StringBuilder(64);
            GetClassName(fg, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                return false;

            RECT rect;
            if (GetWindowRect(fg, out rect))
            {
                Rectangle screen = Screen.PrimaryScreen.Bounds;
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
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
        try
        {
            using (Bitmap sheet = OpenSpriteBitmap("kirby-normal.png"))
            {
                if (sheet != null)
                {
                    using (Bitmap crop = new Bitmap(24, 24))
                    {
                        Color bg = sheet.GetPixel(0, 0);
                        for (int y = 0; y < 24; y++)
                        {
                            for (int x = 0; x < 24; x++)
                            {
                                Color c = sheet.GetPixel(32 + x, 125 + y);
                                if (c.R == bg.R && c.G == bg.G && c.B == bg.B)
                                    crop.SetPixel(x, y, Color.Transparent);
                                else
                                    crop.SetPixel(x, y, c);
                            }
                        }
                        return Icon.FromHandle(crop.GetHicon());
                    }
                }
            }
        }
        catch { }
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
                    _instance.HandleKeySwitch(keyName);
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
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString().ToLowerInvariant();
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x60 && vk <= 0x69) return (vk - 0x60).ToString();
        switch (vk)
        {
            case 0x20: return " ";
            case 0x0D: return "enter";
            case 0x08: return "backspace";
            case 0x09: return "tab";
            case 0x1B: return "escape";
            case 0x25: return "arrowleft";
            case 0x26: return "arrowup";
            case 0x27: return "arrowright";
            case 0x28: return "arrowdown";
            default:   return "vk_" + vk;
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

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
