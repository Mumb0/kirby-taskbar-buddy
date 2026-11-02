using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KirbyTaskbarBuddy
{
    /// <summary>
    /// Core application controller managing the system tray menu, Windows startup registry entry,
    /// global keyboard hook, 60 Hz physics/animation loop, and idle sleep state machine.
    /// </summary>
    public class KirbyTaskbarBuddyContext : ApplicationContext
    {
        private const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "KirbyTaskbarBuddy";
        private const string LEGACY_APP_NAME = "KirbyWallpaperKeyBridge";

        // Movement and jump physics constants (calibrated in unscaled 1x pixels per 60Hz tick)
        internal const double WALK_SPEED = 0.75;
        internal const double JUMP_INIT_VEL = 4.2;
        internal const double JUMP_GRAVITY = 0.18;

        // Static reference to the hook delegate prevents the GC from collecting the callback while unmanaged code holds it.
        private static NativeMethods.LowLevelKeyboardProc _proc;
        private static IntPtr _hookID = IntPtr.Zero;

        // Tracks currently held Virtual-Key codes so OS key auto-repeat while holding a key does not spam ability switches.
        private static readonly HashSet<int> _downKeys = new HashSet<int>();
        private static KirbyTaskbarBuddyContext _instance;

        private bool _isPaused = false;
        private bool _overlayEnabled = true;
        private int _scale = 2;
        private double _idleSleepSeconds = 300.0;

        private NotifyIcon _trayIcon;
        private MenuItem _overlayItem;
        private MenuItem _pauseItem;
        private MenuItem _startupItem;
        private MenuItem _scale1xItem;
        private MenuItem _scale2xItem;
        private MenuItem _scale3xItem;
        private MenuItem _scale4xItem;
        private MenuItem[] _sleepMenuItems;
        private double[] _sleepTimeoutValues;

        private KirbyOverlayWindow _overlay;
        private System.Windows.Forms.Timer _animTimer;
        private Stopwatch _stopwatch;
        private long _lastTickMs;

        private readonly List<FormDef> _forms = new List<FormDef>();
        private readonly Dictionary<string, FormDef> _keyToForm = new Dictionary<string, FormDef>();
        private FormDef _currentForm;

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

        // Throttles fullscreen and screen working-area queries to once every 15 ticks (~240ms)
        // and reuses a single StringBuilder to keep the 60Hz tick loop completely allocation-free.
        private int _envCheckCounter = 0;
        private bool _isFullscreenCached = false;
        private Rectangle _cachedWorkArea;
        private readonly StringBuilder _classNameSb = new StringBuilder(64);
        private IntPtr _trayIconHandle = IntPtr.Zero;
        private Icon _loadedTrayIcon = null;

        public KirbyTaskbarBuddyContext()
        {
            _instance = this;

            FormDefinitions.PopulateForms(_forms, _keyToForm, out _currentForm);
            SpriteLoader.LoadAllSpritesheets(_forms, out _sleepFrames, out _loadedTrayIcon, out _trayIconHandle);

            _overlayItem = new MenuItem("Show Foreground Taskbar Kirby", OnToggleOverlay) { Checked = _overlayEnabled };
            _pauseItem = new MenuItem("Pause Key Switching", OnTogglePause);
            if (IsStartupEnabled()) SetStartup(true);
            _startupItem = new MenuItem("Start with Windows", OnToggleStartup) { Checked = IsStartupEnabled() };

            _scale1xItem = new MenuItem("1x (Small)", (s, e) => SetScale(1)) { Checked = (_scale == 1) };
            _scale2xItem = new MenuItem("2x (Default)", (s, e) => SetScale(2)) { Checked = (_scale == 2) };
            _scale3xItem = new MenuItem("3x (Large)", (s, e) => SetScale(3)) { Checked = (_scale == 3) };
            _scale4xItem = new MenuItem("4x (Extra Large)", (s, e) => SetScale(4)) { Checked = (_scale == 4) };
            MenuItem scaleMenu = new MenuItem("Kirby Size", new MenuItem[] { _scale1xItem, _scale2xItem, _scale3xItem, _scale4xItem });

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
                Icon = _loadedTrayIcon ?? SystemIcons.Application,
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
            NativeMethods.LASTINPUTINFO initLii = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO))
            };
            if (NativeMethods.GetLastInputInfo(ref initLii)) _lastSysInputTick = initLii.dwTime;

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += OnTick;
            _animTimer.Start();

            _proc = HookCallback;
            _hookID = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);

            NativeMethods.TrimMemory();
        }

        /// <summary>
        /// Marshals a clean shutdown onto the UI thread when signaled by a newly launched instance.
        /// </summary>
        public void RequestCleanExit()
        {
            if (_overlay != null && _overlay.IsHandleCreated)
            {
                _overlay.BeginInvoke(new Action(() => OnExit(null, EventArgs.Empty)));
            }
        }

        private void SetScale(int newScale)
        {
            if (_scale == newScale) return;
            _scale = newScale;
            _scale1xItem.Checked = (_scale == 1);
            _scale2xItem.Checked = (_scale == 2);
            _scale3xItem.Checked = (_scale == 3);
            _scale4xItem.Checked = (_scale == 4);
            _lastRenderedTile = null;
            NativeMethods.TrimMemory();
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
                WakeFromSleep(ref _sleepState, ref _sleepAnimTime);
            }
        }

        /// <summary>
        /// Resets the idle timer on keyboard/mouse activity and transitions Kirby out of sleep if napping.
        /// </summary>
        private void NotifyUserActivity()
        {
            if (_stopwatch != null)
            {
                _lastInputMs = _stopwatch.ElapsedMilliseconds;
            }
            WakeFromSleep(ref _sleepState, ref _sleepAnimTime);
        }

        /// <summary>
        /// Transitions Kirby out of sleep when user activity occurs or when the sleep timer is disabled.
        /// Interrupting <see cref="SleepState.Halting"/> returns directly to <see cref="SleepState.Awake"/>,
        /// whereas interrupting <see cref="SleepState.FallingAsleep"/> or <see cref="SleepState.Sleeping"/>
        /// triggers the startled <see cref="SleepState.WakingUp"/> animation.
        /// </summary>
        internal static void WakeFromSleep(ref SleepState sleepState, ref double sleepAnimTime)
        {
            if (sleepState == SleepState.Halting)
            {
                sleepState = SleepState.Awake;
                sleepAnimTime = 0.0;
            }
            else if (sleepState == SleepState.FallingAsleep || sleepState == SleepState.Sleeping)
            {
                sleepState = SleepState.WakingUp;
                sleepAnimTime = 0.0;
            }
        }

        /// <summary>
        /// Clamps frame delta time so system suspend/resume or debugger pauses do not teleport Kirby across the screen.
        /// </summary>
        internal static double ClampDeltaTime(double dtSeconds)
        {
            if (dtSeconds <= 0 || dtSeconds > 0.25) return 0.01667;
            return dtSeconds;
        }

        /// <summary>
        /// Advances the multi-stage sleep state machine by <paramref name="delta60"/> (60Hz-normalized ticks)
        /// and returns the <see cref="SpriteTile"/> to render, or <c>null</c> once Kirby returns to <see cref="SleepState.Awake"/>.
        /// </summary>
        internal static SpriteTile AdvanceSleepState(
            ref SleepState sleepState,
            ref double sleepAnimTime,
            double delta60,
            FormDef currentForm,
            SpriteTile[] sleepFrames)
        {
            if (sleepState == SleepState.Awake || sleepFrames == null || sleepFrames.Length < 8)
            {
                return null;
            }

            if (sleepState == SleepState.Halting)
            {
                sleepAnimTime += 0.05 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);
                if (seqIdx >= FormDefinitions.HaltStandSeq.Length || currentForm == null || currentForm.Stand == null || !currentForm.Stand.HasFrames(2))
                {
                    sleepState = SleepState.FallingAsleep;
                    sleepAnimTime = 0.0;
                    return sleepFrames[FormDefinitions.FallAsleepSeq[0]];
                }
                return currentForm.Stand.Frames[FormDefinitions.HaltStandSeq[seqIdx]];
            }
            if (sleepState == SleepState.FallingAsleep)
            {
                sleepAnimTime += 0.05 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);
                if (seqIdx >= FormDefinitions.FallAsleepSeq.Length)
                {
                    sleepState = SleepState.Sleeping;
                    sleepAnimTime = 0.0;
                    return sleepFrames[FormDefinitions.SleepLoopSeq[0]];
                }
                return sleepFrames[FormDefinitions.FallAsleepSeq[seqIdx]];
            }
            if (sleepState == SleepState.Sleeping)
            {
                sleepAnimTime += 0.045 * delta60;
                int seqIdx = ((int)Math.Floor(sleepAnimTime)) % FormDefinitions.SleepLoopSeq.Length;
                if (seqIdx < 0) seqIdx += FormDefinitions.SleepLoopSeq.Length;
                return sleepFrames[FormDefinitions.SleepLoopSeq[seqIdx]];
            }
            if (sleepState == SleepState.WakingUp)
            {
                sleepAnimTime += 0.09 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);
                if (seqIdx >= FormDefinitions.WakeUpSeq.Length)
                {
                    if (currentForm != null && currentForm.Stand != null && currentForm.Stand.HasFrames(2))
                    {
                        sleepState = SleepState.PostWakeStanding;
                        sleepAnimTime = 0.0;
                        return currentForm.Stand.Frames[FormDefinitions.PostWakeStandSeq[0]];
                    }
                    sleepState = SleepState.Awake;
                    sleepAnimTime = 0.0;
                    return null;
                }
                return sleepFrames[FormDefinitions.WakeUpSeq[seqIdx]];
            }
            if (sleepState == SleepState.PostWakeStanding)
            {
                sleepAnimTime += 0.06 * delta60;
                int seqIdx = (int)Math.Floor(sleepAnimTime);
                if (seqIdx >= FormDefinitions.PostWakeStandSeq.Length || currentForm == null || currentForm.Stand == null || !currentForm.Stand.HasFrames(2))
                {
                    sleepState = SleepState.Awake;
                    sleepAnimTime = 0.0;
                    return null;
                }
                return currentForm.Stand.Frames[FormDefinitions.PostWakeStandSeq[seqIdx]];
            }

            return null;
        }

        /// <summary>
        /// Checks whether the mouse pointer is within guard trigger range of Kirby's body on the taskbar
        /// (Kirby only guards when on the ground, never mid-jump).
        /// </summary>
        internal static bool IsMouseNearKirby(Point mouse, double kirbyX, int workAreaBottom, int scale, bool isJumping)
        {
            if (isJumping) return false;
            double kirbyCenterY = workAreaBottom - (16 * scale);
            double distX = Math.Abs(mouse.X - kirbyX);
            double distY = Math.Abs(mouse.Y - kirbyCenterY);
            return (distX <= 24 * scale) && (distY <= 28 * scale);
        }

        /// <summary>
        /// Advances Kirby's horizontal and vertical jump trajectory by <paramref name="delta60"/> ticks
        /// (scaling horizontal travel by <paramref name="scale"/>) and lands cleanly at <c>jumpY = 0</c> when touching the ground.
        /// </summary>
        internal static void StepJumpPhysics(ref bool isJumping, ref double kirbyX, ref double jumpY, ref double jumpVelY, double delta60, int scale = 2)
        {
            kirbyX += (WALK_SPEED * scale) * delta60;
            jumpY += jumpVelY * delta60;
            jumpVelY -= JUMP_GRAVITY * delta60;
            if (jumpY <= 0.0)
            {
                jumpY = 0.0;
                jumpVelY = 0.0;
                isJumping = false;
            }
        }

        /// <summary>
        /// Maps vertical jump velocity (<paramref name="jumpVelY"/>) to the 5 phases of Kirby's jump and mid-air somersault.
        /// </summary>
        internal static int GetJumpFrameIndex(double jumpVelY)
        {
            if (jumpVelY > 1.2) return 0;       // Rising launch
            if (jumpVelY > 0.0) return 1;       // Apex somersault 1
            if (jumpVelY > -1.2) return 2;      // Apex somersault 2
            if (jumpVelY > -2.6) return 3;      // Early fall recovery
            return 4;                           // Full fall descent
        }

        /// <summary>
        /// Main ~60 Hz update loop driving input idle detection, fullscreen auto-hide,
        /// sleep/wake state transitions, cursor guard proximity, jump physics, and frame rendering.
        /// </summary>
        private void OnTick(object sender, EventArgs e)
        {
            long nowMs = _stopwatch.ElapsedMilliseconds;
            double dtSeconds = ClampDeltaTime((nowMs - _lastTickMs) / 1000.0);
            _lastTickMs = nowMs;

            Point mouse = Cursor.Position;
            bool hasInput = false;
            if (mouse.X != _lastMousePos.X || mouse.Y != _lastMousePos.Y)
            {
                _lastMousePos = mouse;
                hasInput = true;
            }

            NativeMethods.LASTINPUTINFO lii = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO))
            };
            if (NativeMethods.GetLastInputInfo(ref lii) && lii.dwTime != _lastSysInputTick)
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
                    bool hasStandFrames = (_currentForm != null && _currentForm.Stand != null && _currentForm.Stand.HasFrames(2));
                    _sleepState = hasStandFrames ? SleepState.Halting : SleepState.FallingAsleep;
                    _sleepAnimTime = 0.0;
                }
            }

            // Refresh primary monitor working area and fullscreen detection every 15 ticks (~240ms).
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
            int halfWidth = (NativeMethods.TILE_SIZE / 2) * _scale;
            double delta60 = dtSeconds * 60.0;

            // Advance the multi-stage sleep state machine when Kirby is not in his normal Awake state.
            SpriteTile frameToRender = AdvanceSleepState(ref _sleepState, ref _sleepAnimTime, delta60, _currentForm, _sleepFrames);

            if (_sleepState == SleepState.Awake)
            {
                if (_currentForm == null || _currentForm.Walk == null || !_currentForm.Walk.HasFrames()) return;

                // Check if mouse pointer is close to Kirby's body on the taskbar (only guards when on the ground).
                bool isMouseNear = IsMouseNearKirby(mouse, _kirbyX, workArea.Bottom, _scale, _isJumping);

                bool useJump = _isJumping && _currentForm.Jump != null && _currentForm.Jump.HasFrames(5);
                bool useGuard = !useJump && isMouseNear && _currentForm.Guard != null && _currentForm.Guard.HasFrames();
                AnimClip activeClip = useJump ? _currentForm.Jump : (useGuard ? _currentForm.Guard : _currentForm.Walk);

                if (_isJumping)
                {
                    StepJumpPhysics(ref _isJumping, ref _kirbyX, ref _jumpY, ref _jumpVelY, delta60, _scale);
                }
                else if (!isMouseNear)
                {
                    _kirbyX += (WALK_SPEED * _scale) * delta60;
                    _animTime += activeClip.Speed * delta60;
                }
                else
                {
                    // Match SNES Kirby Super Star guard flash / shield shimmer speed (4 frames per state @ 60Hz = 15 FPS).
                    _animTime += activeClip.Speed * delta60;
                }

                // Wrap Kirby seamlessly from the right edge of the monitor back to the left edge.
                if (_kirbyX > workArea.Right + halfWidth)
                {
                    _kirbyX = workArea.Left - halfWidth;
                }

                SpriteTile[] activeFrames = activeClip.Frames;

                int frameIdx;
                if (useJump)
                {
                    frameIdx = GetJumpFrameIndex(_jumpVelY);
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
            int screenY = workArea.Bottom - (NativeMethods.PIVOT_Y + 1) * _scale - (int)Math.Round(_jumpY * _scale);

            // Re-assert HWND_TOPMOST once per second (~60 ticks) so newly opened topmost bars don't cover Kirby.
            _topmostRefreshCounter++;
            if (_topmostRefreshCounter >= 60)
            {
                _topmostRefreshCounter = 0;
                NativeMethods.SetWindowPos(_overlay.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            }

            if (screenX != _lastRenderedX || screenY != _lastRenderedY || frameToRender != _lastRenderedTile)
            {
                _overlay.UpdateSpriteFrame(frameToRender, _scale, screenX, screenY);
                _lastRenderedX = screenX;
                _lastRenderedY = screenY;
                _lastRenderedTile = frameToRender;
            }
        }

        /// <summary>
        /// Checks whether the foreground window covers the entire primary monitor (including the taskbar area)
        /// while ignoring the Windows desktop shell and taskbar classes.
        /// </summary>
        private bool IsFullscreenAppActive(Rectangle work)
        {
            try
            {
                IntPtr fg = NativeMethods.GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == _overlay.Handle) return false;

                _classNameSb.Length = 0;
                NativeMethods.GetClassName(fg, _classNameSb, _classNameSb.Capacity);
                string cls = _classNameSb.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                    return false;

                NativeMethods.RECT rect;
                if (NativeMethods.GetWindowRect(fg, out rect))
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

        /// <summary>
        /// Triggers a jump on Spacebar or toggles the matching copy ability (pressing the active ability key
        /// a second time reverts Kirby back to Normal form).
        /// </summary>
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

            FormDef nextForm = ResolveNextForm(_keyToForm, _currentForm, keyName);
            if (nextForm != null && nextForm != _currentForm)
            {
                _currentForm = nextForm;
            }
        }

        /// <summary>
        /// Resolves the next <see cref="FormDef"/> for a given ability key press.
        /// Pressing the currently active copy ability's key a second time reverts Kirby to Normal form (<c>"0"</c>).
        /// </summary>
        internal static FormDef ResolveNextForm(Dictionary<string, FormDef> keyToForm, FormDef currentForm, string keyName)
        {
            if (keyToForm == null || keyName == null) return currentForm;

            FormDef target;
            if (!keyToForm.TryGetValue(keyName, out target)) return currentForm;

            FormDef normalForm;
            keyToForm.TryGetValue("0", out normalForm);
            return (currentForm == target && target != normalForm) ? normalForm : target;
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
                NativeMethods.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            _trayIcon.Visible = false;
            if (_trayIconHandle != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
            }
            Application.Exit();
        }

        /// <summary>
        /// Global low-level keyboard hook callback (<c>WH_KEYBOARD_LL</c>).
        /// Wakes Kirby on any key press and switches abilities on initial key-down (ignoring key repeat
        /// and ignoring ability switches on the keystroke that wakes Kirby from sleep).
        /// </summary>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _instance != null)
            {
                int msg = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);

                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                {
                    bool wasSleeping = (_instance._sleepState != SleepState.Awake);
                    _instance.NotifyUserActivity();
                    if (_downKeys.Add(vkCode) && !_instance._isPaused && !wasSleeping)
                    {
                        string keyName = NativeMethods.VkCodeToName(vkCode);
                        if (keyName != null) _instance.HandleKeySwitch(keyName);
                    }
                }
                else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                {
                    _downKeys.Remove(vkCode);
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}
