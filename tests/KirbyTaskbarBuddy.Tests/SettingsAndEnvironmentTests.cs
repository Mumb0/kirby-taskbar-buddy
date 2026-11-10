using System.Drawing;
using Xunit;

namespace KirbyTaskbarBuddy.Tests
{
    public class SettingsAndEnvironmentTests
    {
        [Fact]
        public void NormalizeScale_And_NormalizeSleepTimeout_AcceptValidValuesAndFallbackOnInvalid()
        {
            Assert.Equal(1, AppSettings.NormalizeScale(1));
            Assert.Equal(2, AppSettings.NormalizeScale(2));
            Assert.Equal(3, AppSettings.NormalizeScale(3));
            Assert.Equal(4, AppSettings.NormalizeScale(4));
            Assert.Equal(2, AppSettings.NormalizeScale(0));
            Assert.Equal(2, AppSettings.NormalizeScale(5));
            Assert.Equal(2, AppSettings.NormalizeScale(-1));

            foreach (double valid in AppSettings.SleepTimeoutValues)
            {
                Assert.Equal(valid, AppSettings.NormalizeSleepTimeout(valid));
            }
            Assert.Equal(300.0, AppSettings.NormalizeSleepTimeout(0.0));
            Assert.Equal(300.0, AppSettings.NormalizeSleepTimeout(45.0));
            Assert.Equal(300.0, AppSettings.NormalizeSleepTimeout(-99.0));
        }

        [Fact]
        public void FullscreenFilterHelpers_IgnoreShellClassesAndOverlayStyles()
        {
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Progman"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("WorkerW"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Shell_TrayWnd"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Shell_SecondaryTrayWnd"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("Windows.UI.Core.CoreWindow"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("XamlExplorerHostIslandWindow"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("MultitaskingViewFrame"));
            Assert.True(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("ForegroundStaging"));

            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass(null));
            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass(""));
            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("UnityWndClass"));
            Assert.False(DesktopEnvironmentMonitor.IsShellOrOverlayWindowClass("UnrealWindow"));

            Assert.True(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_TRANSPARENT));
            Assert.True(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_NOACTIVATE));
            Assert.True(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_TOOLWINDOW));
            Assert.False(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(0));
            Assert.False(DesktopEnvironmentMonitor.IsNonFullscreenOverlayExStyle(NativeMethods.WS_EX_TOPMOST));
        }

        [Fact]
        public void TryParseRegistryHelpers_ParseValidValues_AndRejectCorruptOrNullWithoutThrowing()
        {
            int intResult;
            Assert.True(RegistrySettingsService.TryParseRegistryInt(3, out intResult));
            Assert.Equal(3, intResult);

            Assert.True(RegistrySettingsService.TryParseRegistryInt(4L, out intResult));
            Assert.Equal(4, intResult);

            Assert.True(RegistrySettingsService.TryParseRegistryInt("2", out intResult));
            Assert.Equal(2, intResult);

            Assert.False(RegistrySettingsService.TryParseRegistryInt(null, out intResult));
            Assert.False(RegistrySettingsService.TryParseRegistryInt("invalid", out intResult));
            Assert.False(RegistrySettingsService.TryParseRegistryInt(long.MaxValue, out intResult));
            Assert.False(RegistrySettingsService.TryParseRegistryInt(new byte[] { 1, 2 }, out intResult));

            double doubleResult;
            Assert.True(RegistrySettingsService.TryParseRegistryDouble(60, out doubleResult));
            Assert.Equal(60.0, doubleResult);

            Assert.True(RegistrySettingsService.TryParseRegistryDouble(180L, out doubleResult));
            Assert.Equal(180.0, doubleResult);

            Assert.True(RegistrySettingsService.TryParseRegistryDouble(300.0, out doubleResult));
            Assert.Equal(300.0, doubleResult);

            Assert.True(RegistrySettingsService.TryParseRegistryDouble("-1", out doubleResult));
            Assert.Equal(-1.0, doubleResult);

            Assert.False(RegistrySettingsService.TryParseRegistryDouble(null, out doubleResult));
            Assert.False(RegistrySettingsService.TryParseRegistryDouble("not-a-number", out doubleResult));
            Assert.False(RegistrySettingsService.TryParseRegistryDouble(new object(), out doubleResult));
        }

        [Fact]
        public void GlobalKeyboardHook_DeduplicatesAutoRepeatUntilKeyUp_AndIdleMonitorTracksElapsedSeconds()
        {
            int keyDownCount = 0;
            int initialDownCount = 0;

            using (GlobalKeyboardHook hook = new GlobalKeyboardHook(
                (vk, isInitial) =>
                {
                    keyDownCount++;
                    if (isInitial) initialDownCount++;
                },
                installOsHook: false))
            {
                hook.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, 0x31);
                hook.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, 0x31); // Auto-repeat while held
                Assert.Equal(2, keyDownCount);
                Assert.Equal(1, initialDownCount);

                hook.ProcessKeyMessage(NativeMethods.WM_KEYUP, 0x31);
                hook.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, 0x31); // Pressed again after release
                Assert.Equal(3, keyDownCount);
                Assert.Equal(2, initialDownCount);
            }

            IdleActivityMonitor monitor = new IdleActivityMonitor(1000, new Point(10, 10));
            Assert.Equal(4.5, monitor.GetIdleSeconds(5500), 5);
            monitor.RecordActivity(5000);
            Assert.Equal(0.5, monitor.GetIdleSeconds(5500), 5);
            Assert.True(monitor.PollActivity(new Point(25, 10)));
        }

        [Fact]
        public void KirbyOverlayWindow_ClearOverlayAndUpdateSpriteFrame_ToggleIsClearedStateWithoutShowHide()
        {
            using (KirbyOverlayWindow window = new KirbyOverlayWindow())
            {
                System.IntPtr handle = window.Handle;
                Assert.NotEqual(System.IntPtr.Zero, handle);
                Assert.False(window.IsCleared);

                window.ClearOverlay(2);
                Assert.True(window.IsCleared);

                // Calling ClearOverlay again while already cleared is an idempotent no-op.
                window.ClearOverlay(2);
                Assert.True(window.IsCleared);

                SpriteTile tile = CreateDummyTiles(1)[0];
                window.UpdateSpriteFrame(tile, 2, 100, 100);
                Assert.False(window.IsCleared);

                // RenderAtTaskbar with null tile clears the overlay; with a valid tile renders and clears IsCleared.
                Rectangle workArea = new Rectangle(0, 0, 1920, 1040);
                window.RenderAtTaskbar(null, 2, workArea, 200.0, 0.0);
                Assert.True(window.IsCleared);
                window.RenderAtTaskbar(tile, 2, workArea, 200.0, 0.0);
                Assert.False(window.IsCleared);
            }

            using (OverlayStage stage = new OverlayStage(2))
            {
                Assert.NotNull(stage.EnemyLayer);
                Assert.NotNull(stage.KirbyLayer);
                Assert.NotNull(stage.VfxLayer);
                stage.ClearAll(2);
                Assert.True(stage.EnemyLayer.IsCleared);
                Assert.True(stage.KirbyLayer.IsCleared);
                Assert.True(stage.VfxLayer.IsCleared);
            }
        }

        private static SpriteTile[] CreateDummyTiles(int count)
        {
            SpriteTile[] tiles = new SpriteTile[count];
            for (int i = 0; i < count; i++)
            {
                tiles[i] = new SpriteTile
                {
                    MinX = 30,
                    MinY = 30,
                    Width = 20,
                    Height = 25,
                    PremulPixels = new int[20 * 25]
                };
            }
            return tiles;
        }
    }
}
