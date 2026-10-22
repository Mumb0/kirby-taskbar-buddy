using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public class KirbyKeyBridge : ApplicationContext
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int PORT = 19855;
    private const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "KirbyWallpaperKeyBridge";

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static LowLevelKeyboardProc _proc;
    private static IntPtr _hookID = IntPtr.Zero;

    private static readonly HashSet<int> _downKeys = new HashSet<int>();
    private static readonly List<StreamWriter> _clients = new List<StreamWriter>();
    private static readonly object _clientsLock = new object();
    private static TcpListener _listener;
    private static bool _isPaused = false;

    private NotifyIcon _trayIcon;
    private MenuItem _pauseItem;
    private MenuItem _startupItem;

    [STAThread]
    public static void Main()
    {
        bool createdNew;
        using (Mutex mutex = new Mutex(true, "Local\\KirbyWallpaperKeyBridgeMutex", out createdNew))
        {
            if (!createdNew)
            {
                // Already running in tray
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new KirbyKeyBridge());
        }
    }

    public KirbyKeyBridge()
    {
        _pauseItem = new MenuItem("Pause Key Forwarding", OnTogglePause);
        _startupItem = new MenuItem("Start with Windows", OnToggleStartup)
        {
            Checked = IsStartupEnabled()
        };

        ContextMenu menu = new ContextMenu(new MenuItem[]
        {
            new MenuItem("Kirby Key Bridge (Port " + PORT + ")") { Enabled = false },
            new MenuItem("-"),
            _pauseItem,
            _startupItem,
            new MenuItem("-"),
            new MenuItem("Exit", OnExit)
        });

        _trayIcon = new NotifyIcon
        {
            Icon = LoadKirbyTrayIcon(),
            Text = "Kirby Wallpaper Key Bridge",
            ContextMenu = menu,
            Visible = true
        };

        // Start SSE Server
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, PORT);
            _listener.Start();
            Thread serverThread = new Thread(AcceptClients) { IsBackground = true };
            serverThread.Start();
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(3000, "Kirby Key Bridge", "Port " + PORT + " is already in use (close any open .bat window).\n" + ex.Message, ToolTipIcon.Warning);
        }

        // Install global low-level keyboard hook
        _proc = HookCallback;
        _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    private Icon LoadKirbyTrayIcon()
    {
        try
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string sheetPath = Path.Combine(exeDir, "kirby-normal.png");
            if (File.Exists(sheetPath))
            {
                using (Bitmap sheet = new Bitmap(sheetPath))
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
        catch { }
        return SystemIcons.Application;
    }

    private void OnTogglePause(object sender, EventArgs e)
    {
        _isPaused = !_isPaused;
        _pauseItem.Checked = _isPaused;
        _trayIcon.Text = _isPaused ? "Kirby Key Bridge (Paused)" : "Kirby Wallpaper Key Bridge";
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
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
        try { if (_listener != null) _listener.Stop(); } catch { }
        _trayIcon.Visible = false;
        Application.Exit();
    }

    private static void AcceptClients()
    {
        while (true)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
            catch
            {
                break;
            }
        }
    }

    private static void HandleClient(TcpClient client)
    {
        try
        {
            client.NoDelay = true;
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);

            string line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine())) { }

            StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            writer.Write(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/event-stream\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Connection: keep-alive\r\n" +
                "Access-Control-Allow-Origin: *\r\n\r\n"
            );
            writer.Write(": connected\n\n");

            lock (_clientsLock)
            {
                _clients.Add(writer);
            }
        }
        catch
        {
            client.Close();
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_isPaused)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);

            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                if (_downKeys.Add(vkCode))
                {
                    string keyName = VkCodeToName(vkCode);
                    BroadcastKey(keyName, vkCode);
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

    private static void BroadcastKey(string key, int vk)
    {
        string safeKey = key.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string payload = "data: {\"key\":\"" + safeKey + "\",\"vk\":" + vk + "}\n\n";

        lock (_clientsLock)
        {
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                try
                {
                    _clients[i].Write(payload);
                }
                catch
                {
                    _clients.RemoveAt(i);
                }
            }
        }
    }

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
