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
    private const string APP_NAME = "KirbyWallpaperKeyBridge";

    private const int TILE_SIZE = 64;
    private const int PIVOT_X = 32;
    private const int PIVOT_Y = TILE_SIZE - 1;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static LowLevelKeyboardProc _proc;
    private static IntPtr _hookID = IntPtr.Zero;

    private static readonly HashSet<int> _downKeys = new HashSet<int>();
    private static KirbyTaskbarBuddy _instance;

    private bool _isPaused = false;
    private bool _overlayEnabled = true;
    private int _scale = 2;

    private NotifyIcon _trayIcon;
    private MenuItem _overlayItem;
    private MenuItem _pauseItem;
    private MenuItem _startupItem;
    private MenuItem _scale2xItem;
    private MenuItem _scale3xItem;
    private MenuItem _scale4xItem;

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
        public Bitmap[] RawFrames64;
        public Bitmap[] ScaledFrames;
    }

    private readonly List<FormDef> _forms = new List<FormDef>();
    private readonly Dictionary<string, FormDef> _keyToForm = new Dictionary<string, FormDef>();
    private FormDef _currentForm;

    private double _kirbyX = -64;
    private double _animTime = 0.0;
    private int _lastRenderedFrameIdx = -1;
    private int _lastRenderedX = int.MinValue;
    private int _lastRenderedY = int.MinValue;
    private FormDef _lastRenderedForm = null;
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

    private string _stopSignalPath;

    public KirbyTaskbarBuddy()
    {
        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _stopSignalPath = Path.Combine(exeDir, ".stop");
        try { if (File.Exists(_stopSignalPath)) File.Delete(_stopSignalPath); } catch { }
        try
        {
            string prevExe = Path.Combine(exeDir, ".KirbyTaskbarBuddy.prev.exe");
            if (File.Exists(prevExe)) File.Delete(prevExe);
        }
        catch { }

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

        ContextMenu menu = new ContextMenu(new MenuItem[]
        {
            new MenuItem("Kirby Taskbar Buddy") { Enabled = false },
            new MenuItem("-"),
            _overlayItem,
            scaleMenu,
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
        });
        AddForm("ice", "l", "kirby-ice.png", new[] {
            new Rectangle(6, 213, 24, 34), new Rectangle(35, 213, 24, 34),
            new Rectangle(63, 213, 24, 34), new Rectangle(92, 213, 24, 34)
        });
        AddForm("bomb", "b", "kirby-bomb.png", new[] {
            new Rectangle(0, 101, 31, 29), new Rectangle(32, 101, 32, 29),
            new Rectangle(68, 101, 32, 29), new Rectangle(98, 101, 32, 29)
        });
        AddForm("beam", "e", "kirby-beam.png", new[] {
            new Rectangle(5, 101, 27, 37), new Rectangle(35, 101, 27, 37),
            new Rectangle(65, 101, 27, 37), new Rectangle(96, 101, 27, 37)
        });
        AddForm("cutter", "c", "kirby-cutter.png", new[] {
            new Rectangle(4, 126, 29, 35), new Rectangle(34, 126, 29, 35),
            new Rectangle(66, 126, 29, 35), new Rectangle(100, 126, 29, 35)
        });
        AddForm("fighter", "f", "kirby-fighter.png", new[] {
            new Rectangle(6, 95, 26, 24), new Rectangle(36, 95, 26, 24),
            new Rectangle(66, 95, 26, 24), new Rectangle(96, 95, 26, 24)
        });
        AddForm("fire", "d", "kirby-fire.png", new[] {
            new Rectangle(6, 309, 24, 34), new Rectangle(35, 309, 24, 34),
            new Rectangle(63, 309, 24, 34), new Rectangle(92, 309, 24, 34)
        });
        AddForm("hammer", "h", "kirby-hammer.png", new[] {
            new Rectangle(5, 164, 44, 49), new Rectangle(52, 164, 44, 49),
            new Rectangle(99, 164, 44, 49), new Rectangle(146, 164, 44, 49)
        });
        AddForm("jet", "j", "kirby-jet.png", new[] {
            new Rectangle(6, 114, 28, 29), new Rectangle(39, 114, 28, 29),
            new Rectangle(70, 114, 28, 29), new Rectangle(101, 114, 28, 29)
        });
        AddForm("mirror", "m", "kirby-mirror.png", new[] {
            new Rectangle(5, 117, 27, 37), new Rectangle(35, 117, 27, 37),
            new Rectangle(65, 117, 27, 37), new Rectangle(96, 117, 27, 37)
        });
        AddForm("ninja", "n", "kirby-ninja.png", new[] {
            new Rectangle(207, 45, 27, 32), new Rectangle(235, 45, 27, 32),
            new Rectangle(264, 45, 27, 32), new Rectangle(296, 45, 27, 32)
        });
        AddForm("paint", "p", "kirby-paint.png", new[] {
            new Rectangle(5, 113, 26, 28), new Rectangle(34, 113, 26, 28),
            new Rectangle(62, 113, 26, 28), new Rectangle(92, 113, 26, 28)
        });
        AddForm("parasol", "r", "kirby-parasol.png", new[] {
            new Rectangle(269, 92, 42, 49), new Rectangle(314, 92, 42, 49),
            new Rectangle(359, 92, 42, 49), new Rectangle(404, 92, 42, 49)
        });
        AddForm("plasma", "a", "kirby-plasma.png", new[] {
            new Rectangle(6, 357, 24, 34), new Rectangle(35, 357, 24, 34),
            new Rectangle(63, 357, 24, 34), new Rectangle(92, 357, 24, 34)
        });
        AddForm("stone", "s", "kirby-stone.png", new[] {
            new Rectangle(5, 123, 27, 32), new Rectangle(33, 123, 27, 32),
            new Rectangle(62, 123, 27, 32), new Rectangle(94, 123, 27, 32)
        });
        AddForm("suplex", "u", "kirby-suplex.png", new[] {
            new Rectangle(6, 95, 26, 24), new Rectangle(36, 95, 26, 24),
            new Rectangle(66, 95, 26, 24), new Rectangle(96, 95, 26, 24)
        });
        AddForm("sword", "w", "kirby-sword.png", new[] {
            new Rectangle(3, 101, 32, 30), new Rectangle(34, 101, 32, 30),
            new Rectangle(67, 101, 32, 30), new Rectangle(101, 101, 32, 30)
        });
        AddForm("wheel", "v", "kirby-wheel.png", new[] {
            new Rectangle(5, 121, 26, 28), new Rectangle(34, 121, 26, 28),
            new Rectangle(62, 121, 26, 28), new Rectangle(92, 121, 26, 28)
        });
        AddForm("wing", "g", "kirby-wing.png", new[] {
            new Rectangle(4, 127, 31, 32), new Rectangle(35, 127, 31, 32),
            new Rectangle(66, 127, 31, 32), new Rectangle(100, 127, 31, 32)
        });
        AddForm("yoyo", "y", "kirby-yoyo.png", new[] {
            new Rectangle(5, 121, 26, 28), new Rectangle(34, 121, 26, 28),
            new Rectangle(62, 121, 26, 28), new Rectangle(92, 121, 26, 28)
        });
    }

    private void AddForm(string name, string key, string file, Rectangle[] coords)
    {
        FormDef def = new FormDef { Name = name, Key = key, FileName = file, Coords = coords };
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
                    Rectangle r = form.Coords[i];
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
                                if (dx < feetMinX) feetMinX = dx;
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
                    base4[i] = tile64;
                }

                // 6-frame ping-pong walk cycle [0, 1, 2, 3, 2, 1]
                form.RawFrames64 = new Bitmap[] { base4[0], base4[1], base4[2], base4[3], base4[2], base4[1] };
            }
        }
        RebuildScaledFrames();
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
            form.ScaledFrames = new Bitmap[form.RawFrames64.Length];
            for (int i = 0; i < form.RawFrames64.Length; i++)
            {
                Bitmap scaled = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.SmoothingMode = SmoothingMode.None;
                    g.DrawImage(form.RawFrames64[i], new Rectangle(0, 0, targetSize, targetSize));
                }
                form.ScaledFrames[i] = scaled;
            }
        }
        _lastRenderedForm = null;
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

    private void OnTick(object sender, EventArgs e)
    {
        if (_stopSignalPath != null && File.Exists(_stopSignalPath))
        {
            try { File.Delete(_stopSignalPath); } catch { }
            OnExit(null, EventArgs.Empty);
            return;
        }

        long nowMs = _stopwatch.ElapsedMilliseconds;
        double dtSeconds = (nowMs - _lastTickMs) / 1000.0;
        _lastTickMs = nowMs;
        if (dtSeconds <= 0 || dtSeconds > 0.25) dtSeconds = 0.01667;

        if (!_overlayEnabled || IsFullscreenAppActive())
        {
            if (_overlay.Visible) _overlay.Hide();
            return;
        }

        if (!_overlay.Visible) _overlay.Show();

        double delta60 = dtSeconds * 60.0;
        _kirbyX += 1.5 * delta60;
        _animTime += 0.1 * delta60;

        Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
        int spriteSize = TILE_SIZE * _scale;
        int halfWidth = (TILE_SIZE / 2) * _scale;

        if (_kirbyX > workArea.Right + halfWidth)
        {
            _kirbyX = workArea.Left - halfWidth;
        }

        if (_currentForm == null || _currentForm.ScaledFrames == null) return;

        int frameCount = _currentForm.ScaledFrames.Length;
        int frameIdx = ((int)Math.Floor(_animTime)) % frameCount;
        if (frameIdx < 0) frameIdx += frameCount;

        int screenX = (int)Math.Round(_kirbyX) - halfWidth;
        int screenY = workArea.Bottom - spriteSize;

        _topmostRefreshCounter++;
        if (_topmostRefreshCounter >= 60)
        {
            _topmostRefreshCounter = 0;
            SetWindowPos(_overlay.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        if (screenX != _lastRenderedX || screenY != _lastRenderedY || frameIdx != _lastRenderedFrameIdx || _currentForm != _lastRenderedForm)
        {
            _overlay.UpdateSpriteFrame(_currentForm.ScaledFrames[frameIdx], screenX, screenY);
            _lastRenderedX = screenX;
            _lastRenderedY = screenY;
            _lastRenderedFrameIdx = frameIdx;
            _lastRenderedForm = _currentForm;
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
                return key != null && key.GetValue(APP_NAME) != null;
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
        if (nCode >= 0 && _instance != null && !_instance._isPaused)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);

            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                if (_downKeys.Add(vkCode))
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
