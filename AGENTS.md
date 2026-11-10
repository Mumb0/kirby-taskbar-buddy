# Kirby Taskbar Buddy — Agent Architecture & File Map

Always consult this file before adding, moving, or modifying code so you know where existing types, helpers, and constants live and never duplicate functionality.
Keep this file updated whenever files or classes are added, moved, or renamed.

---

## 1. Architectural Rules & Build Constraints

1. **C# 5 Language Compatibility (`<LangVersion>5</LangVersion>`)**:
   - [`KirbyTaskbarBuddy.csproj`](KirbyTaskbarBuddy.csproj) enforces `<LangVersion>5</LangVersion>` so [`build.bat`](build.bat) can compile a standalone `.exe` using Windows' built-in `.NET Framework 4.0` compiler (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).
   - **Never use C# 6+ syntax**: no `?.` null-conditional operator, no `nameof(...)`, no `$""` string interpolation, no expression-bodied members (`=>`), no auto-property initializers, and no inline `out var`.
2. **Single Root Namespace (`namespace KirbyTaskbarBuddy`)**:
   - All production files across `src/Core/`, `src/Catalog/`, `src/Actors/`, `src/Rendering/`, and `src/Platform/` declare `namespace KirbyTaskbarBuddy` so types resolve across folders without extra `using` directives.
3. **Zero Allocations on the 60 Hz Tick Loop**:
   - Pre-allocate scratch buffers (`int[]`), reusable `SpriteTile` instances, and `StringBuilder` buffers at startup. Never allocate bitmaps, arrays, or closures inside `OnTick` or per-frame rendering methods.
4. **Pure Static / Testable Logic**:
   - Keep physics, state-machine transitions, coordinate math, and spritesheet slicing free of WinForms `Timer` or `NotifyIcon` side effects so xUnit tests can exercise them deterministically.

---

## 2. Directory & File Map (`src/`)

### Entry Point
- [`src/Program.cs`](src/Program.cs) — `Program.Main`: DPI awareness (`SetProcessDPIAware`), single-instance `Mutex`, exception logging (`crash.log`), `--clean-sprites` CLI entrypoint, and `Application.Run(new KirbyTaskbarBuddyContext())`.

### `src/Core/` — Shared Constants, Settings & Diagnostics
- [`src/Core/CanvasConstants.cs`](src/Core/CanvasConstants.cs) — `CanvasConstants`:
  - `TILE_SIZE = 80` (80×80 canvas size)
  - `PIVOT_X = 40`, `PIVOT_Y = 54` (Kirby's foot anchor at `(40, 54)`)
  - `INHALE_MOUTH_X = 14`, `INHALE_MOUTH_Y = 39` (inhale mouth target offset in 1× sprite pixels)
- [`src/Core/AppSettings.cs`](src/Core/AppSettings.cs) — `AppSettings`:
  - Runtime settings (`Scale`, `IdleSleepSeconds`, `CombatEnabled`, `OverlayEnabled`, `IsPaused`), `SleepTimeoutValues`, `SleepTimeoutLabels`, `NormalizeScale(int)`, `NormalizeSleepTimeout(double)`
- [`src/Core/Diagnostics.cs`](src/Core/Diagnostics.cs) — `Diagnostics`:
  - `LogWarning(string context, Exception ex)` — appends non-fatal warnings to `crash.log`
  - `TrimMemory()` — triggers GC compaction + `SetProcessWorkingSetSize`

### `src/Catalog/` — Sprite Catalog, Clips, Forms, Enemies & Key Bindings
- [`src/Catalog/AnimClip.cs`](src/Catalog/AnimClip.cs):
  - `PivotMode` enum (`BottomCenter`, `Center`, `Custom`)
  - `AnimClip` class: `Speed`, `Loop`, `Pivot`, `CustomPivotX`, `CustomPivotY`, `RawFrames`, `Frames`, `HasFrames(int minCount = 1)`, `GetFrame(double animTime)` (instance + static), `GetVfxFrame(double animTime)` (instance + static)
- [`src/Catalog/FormDef.cs`](src/Catalog/FormDef.cs):
  - `SwallowParticleLayer` struct (`Rectangles`, `StartFrame`, `EndFrame`, `Offsets`, `Pivot`, `CustomPivotX`, `CustomPivotY`)
  - `FormDef` class (`Name`, `Key`, `Walk`, `Jump`, `Stand`, `Guard`, `Inhale`, `Mouthful`, `Hit`, `Swallow`, `SwallowAbility`, `SwallowParticles`, etc.)
  - `EnemyDef` class (`Name`, `CopyAbility`, `WalkSpeed`, `Walk`, `Inhaled`, `WalkRects`, `InhaledRects`)
- [`src/Catalog/FormDefinitions.cs`](src/Catalog/FormDefinitions.cs) — `FormDefinitions`:
  - `SleepState` and `SwallowState` enums
  - Shared sprite rects & sequences: `NormalTrayIconRect`, `MirrorGuardBodyRect`, `MirrorGuardBubbleRect`, `SLEEP_PIVOT_X`, `SLEEP_PIVOT_Y`, `HaltStandSeq`, `FallAsleepSeq`, `SleepLoopSeq`, `WakeUpSeq`, `PostWakeStandSeq`, `SwallowNoAbilitySeq`, `SwallowAbilitySeq`, `HitSeq`
  - Catalog builders & lookup helpers: `PopulateForms`, `PopulateEnemies`, `ResolveNormalForm`, `ResolveSwallowedForm`
- [`src/Catalog/KeyBindings.cs`](src/Catalog/KeyBindings.cs) — `KeyBindings`:
  - `VkCodeToName(int vkCode)` — maps Win32 virtual-key codes (`0`–`9`, `Numpad0`–`Numpad9`, `A`–`Z`, Spacebar) to form key strings
  - `ResolveNextForm(IDictionary<string, FormDef> keyToForm, FormDef currentForm, string keyName)` — resolves next form or toggles back to Normal (`"0"`)

### `src/Actors/` — Kirby, Enemies, Physics & Combat State Machines
- [`src/Actors/JumpPhysicsState.cs`](src/Actors/JumpPhysicsState.cs) — `JumpPhysicsState` struct:
  - Constants: `WALK_SPEED = 0.75`, `JUMP_INIT_VEL = 4.2`, `JUMP_GRAVITY = 0.18`
  - State & methods: `IsJumping`, `JumpY`, `JumpVelY`, `StartJump()`, `Step`, `GetFrameIndex()`, `ClampDeltaTime`, `IsMouseNearKirby`, `StepJumpPhysics`, `GetJumpFrameIndex`
- [`src/Actors/HitRecoilState.cs`](src/Actors/HitRecoilState.cs) — `HitRecoilState` struct:
  - Constants: `ENEMY_CONTACT_DISTANCE = 16.0`, `HIT_PUSHBACK_FRAMES = 8`, `HIT_PUSHBACK_SPEED = 1.5`, `ENEMY_HIT_FRAMES = 24`
  - State & methods: `IsHit`, `AnimTime`, `Reset()`, `AdvanceKirby`, `StepEnemy`, `CanTakeEnemyHit`, `TryTriggerEnemyHit`, `AdvanceHitState`, `StepHitEnemyPhysics`
- [`src/Actors/SleepController.cs`](src/Actors/SleepController.cs) — `SleepController`:
  - Multi-stage sleep state machine (`BeginSleep`, `Wake`, `Advance`, `WakeFromSleep`, `AdvanceSleepState`)
- [`src/Actors/SwallowController.cs`](src/Actors/SwallowController.cs) — `SwallowController`:
  - Constants: `INHALE_RANGE = 80.0`, `INHALE_PULL_SPEED = 2.0`, `INHALE_TOUCH_DISTANCE = 8.0`
  - State & methods: `State`, `AnimTime`, `SwallowedEnemy`, `Reset()`, `BeginInhale()`, `BeginMouthful`, `Advance`, `CanStartInhale`, `AdvanceSwallowState`
- [`src/Actors/EnemyActor.cs`](src/Actors/EnemyActor.cs) — `EnemyActor`:
  - Active enemy state (`Def`, `X`, `AnimTime`, `HitRecoil`), `Spawn`, `Clear`, `Step`, `GetCurrentFrame`, `StepEnemyPhysics`, `StepInhaledEnemyPhysics`
- [`src/Actors/EnemySpawner.cs`](src/Actors/EnemySpawner.cs) — `EnemySpawner`:
  - Constants: `SPAWN_MIN_SECONDS = 5.0`, `SPAWN_MAX_SECONDS = 10.0`
  - Manages random spawn timer and owns `EnemyActor` (`EnsureActiveOrTickTimer`, `ClearActiveEnemy`, `NextSpawnInterval`)
- [`src/Actors/KirbyActor.cs`](src/Actors/KirbyActor.cs) — `KirbyActor`:
  - Owns Kirby's position (`X`), `CurrentForm`, `AnimTime`, `Jump`, `HitRecoil`, `Sleep`, `Swallow`, `InhaleVfx`, `ActiveVfx`, `HandleKeySwitch`, `ResetCombatState`
- [`src/Actors/TaskbarWorld.cs`](src/Actors/TaskbarWorld.cs) — `TaskbarWorld`:
  - Scene coordinator updating `KirbyActor`, `EnemySpawner`, combat interactions, and rendering onto `OverlayStage` (`Tick`, `CheckIdleSleep`, `DisableCombat`)

### `src/Rendering/` — Spritesheet Slicing, Layered Windows & Visual Effects
- [`src/Rendering/SpriteTile.cs`](src/Rendering/SpriteTile.cs) — `SpriteTile`:
  - `PremulPixels` (`int[]` in `0xAARRGGBB` premultiplied BGRA), `MinX`, `MinY`, `Width`, `Height`
- [`src/Rendering/FastSheet.cs`](src/Rendering/FastSheet.cs) — `FastSheet`:
  - Locks PNG into flat `Pixels` array once, detects `BgColor` at `(0, 0)`
- [`src/Rendering/SpritesheetCleaner.cs`](src/Rendering/SpritesheetCleaner.cs) — `SpritesheetCleaner`:
  - `StripSpritesheetBackgrounds(string assetsDir)` and CLI `Main(string[] args)` used by [`clean-sprites.bat`](clean-sprites.bat)
- [`src/Rendering/SpriteLoader.cs`](src/Rendering/SpriteLoader.cs) — `SpriteLoader`:
  - `LoadAllSpritesheets`, `ExtractClip`, `ExtractComposedClip`, `ExtractSleepClip`, `ExtractMirrorGuardClip`, `ExtractSwallowClip`, `ExtractRawTile`, `CreateTrayIcon`
- [`src/Rendering/ISpriteLayer.cs`](src/Rendering/ISpriteLayer.cs) — `ISpriteLayer`:
  - Interface for per-pixel alpha overlay layers: `IsCleared`, `UpdateSpriteFrame`, `RenderAtTaskbar`, `ClearOverlay`
- [`src/Rendering/KirbyOverlayWindow.cs`](src/Rendering/KirbyOverlayWindow.cs) — `KirbyOverlayWindow : Form, ISpriteLayer`:
  - Win32 layered window (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE`) with dirty-checked `RenderAtTaskbar`, cached 32bpp DIB section, and `UpdateLayeredWindow` blitting
- [`src/Rendering/OverlayStage.cs`](src/Rendering/OverlayStage.cs) — `OverlayStage : IDisposable`:
  - Owns the 3 back-to-front `KirbyOverlayWindow` layers (`EnemyLayer`, `KirbyLayer`, `VfxLayer`), `ClearAll(int scale)`, `RefreshTopmostOrder()`, `OwnsWindowHandle(IntPtr)`, and `BeginInvokeOnUiThread(Action)`
- [`src/Rendering/IVfxEffect.cs`](src/Rendering/IVfxEffect.cs):
  - `IVfxEffect` interface (`OffsetX`, `OffsetY`, `Step(double delta60)`, `Render()`)
  - `SpriteClipVfxEffect : IVfxEffect` — plays sprite-sheet `AnimClip` visual effects anchored to Kirby
- [`src/Rendering/InhaleParticleEffect.cs`](src/Rendering/InhaleParticleEffect.cs):
  - `InhaleParticle` struct (`Dx`, `Dy`, `BlackOnTop`)
  - `InhaleParticleEffect : IVfxEffect` — procedural 4-particle inhale wind lines (`PARTICLE_COUNT`, `PARTICLE_SPEED`, `SpawnParticle`, `InitParticles`, `StepParticles`, `RenderParticlesTile`, double-buffered `SpriteTile` rendering)

### `src/Platform/` — Win32 P/Invoke, OS Hooks, Registry, Tray UI & App Context
- [`src/Platform/NativeMethods.cs`](src/Platform/NativeMethods.cs) — `NativeMethods`:
  - Pure Win32 (`user32.dll`, `gdi32.dll`, `kernel32.dll`) P/Invoke signatures, structs (`POINT`, `SIZE`, `BLENDFUNCTION`, `BITMAPINFO`, `RECT`, `LASTINPUTINFO`), and window style constants
- [`src/Platform/RegistrySettingsService.cs`](src/Platform/RegistrySettingsService.cs) — `RegistrySettingsService`:
  - `LoadSettings(AppSettings)`, `SaveSettings(AppSettings)`, `IsStartupEnabled()`, `SetStartup(bool)`, `TryParseRegistryInt`, `TryParseRegistryDouble`
- [`src/Platform/GlobalKeyboardHook.cs`](src/Platform/GlobalKeyboardHook.cs) — `GlobalKeyboardHook : IDisposable`:
  - Instance-scoped `WH_KEYBOARD_LL` hook with key-down auto-repeat deduplication (`ProcessKeyMessage`)
- [`src/Platform/IdleActivityMonitor.cs`](src/Platform/IdleActivityMonitor.cs) — `IdleActivityMonitor`:
  - Tracks mouse movement and `GetLastInputInfo` ticks (`PollActivity`, `RecordActivity`, `GetIdleSeconds`)
- [`src/Platform/DesktopEnvironmentMonitor.cs`](src/Platform/DesktopEnvironmentMonitor.cs) — `DesktopEnvironmentMonitor`:
  - Throttled 15-tick `Screen.PrimaryScreen.WorkingArea` and fullscreen app detection (`StepTick`, `IsFullscreenAppActive`, `IsShellOrOverlayWindowClass`, `IsNonFullscreenOverlayExStyle`)
- [`src/Platform/TrayMenuController.cs`](src/Platform/TrayMenuController.cs) — `TrayMenuController : IDisposable`:
  - System tray `NotifyIcon` and `ContextMenu` builder, state synchronization (`SyncState`), and menu action events
- [`src/Platform/KirbyTaskbarBuddyContext.cs`](src/Platform/KirbyTaskbarBuddyContext.cs) — `KirbyTaskbarBuddyContext : ApplicationContext`:
  - Thin ~215-line application orchestrator wiring `TrayMenuController`, `OverlayStage`, `GlobalKeyboardHook`, `IdleActivityMonitor`, `DesktopEnvironmentMonitor`, and `TaskbarWorld`

---

## 3. Test Suite Map (`tests/KirbyTaskbarBuddy.Tests/`)

- [`tests/KirbyTaskbarBuddy.Tests/FormDefinitionsTests.cs`](tests/KirbyTaskbarBuddy.Tests/FormDefinitionsTests.cs) — Validates all 19 copy abilities, enemy definitions (`Waddle Dee`, `Waddle Doo`), and sprite rectangle builders (`Row`, `Strip`).
- [`tests/KirbyTaskbarBuddy.Tests/SpritesheetIntegrityTests.cs`](tests/KirbyTaskbarBuddy.Tests/SpritesheetIntegrityTests.cs) — Verifies embedded PNG spritesheets load cleanly and every configured rectangle is within bounds and non-empty.
- [`tests/KirbyTaskbarBuddy.Tests/SpriteLoaderAlignmentTests.cs`](tests/KirbyTaskbarBuddy.Tests/SpriteLoaderAlignmentTests.cs) — Tests `SpriteLoader` pivot anchoring (`BottomCenter`, `Center`, `Custom`), `ExtractComposedClip`, `ExtractMirrorGuardClip`, `ExtractSwallowClip`, and `CreateTrayIcon`.
- [`tests/KirbyTaskbarBuddy.Tests/KeyMappingTests.cs`](tests/KirbyTaskbarBuddy.Tests/KeyMappingTests.cs) — Tests `KeyBindings.VkCodeToName` and `KeyBindings.ResolveNextForm` ability toggling.
- [`tests/KirbyTaskbarBuddy.Tests/SleepControllerTests.cs`](tests/KirbyTaskbarBuddy.Tests/SleepControllerTests.cs) — Tests `SleepController` multi-stage sleep and wake-up transitions (`Halting` -> `FallingAsleep` -> `Sleeping` -> `WakingUp` -> `PostWakeStanding` -> `Awake`).
- [`tests/KirbyTaskbarBuddy.Tests/JumpAndMovementPhysicsTests.cs`](tests/KirbyTaskbarBuddy.Tests/JumpAndMovementPhysicsTests.cs) — Tests `JumpPhysicsState` (`ClampDeltaTime`, `StepJumpPhysics`, `GetJumpFrameIndex`, `IsMouseNearKirby`) and `AnimClip` (`GetFrame`, `GetVfxFrame`).
- [`tests/KirbyTaskbarBuddy.Tests/CombatAndEnemyTests.cs`](tests/KirbyTaskbarBuddy.Tests/CombatAndEnemyTests.cs) — Tests `EnemySpawner`, `EnemyActor`, `SwallowController`, `HitRecoilState`, and `InhaleParticleEffect`.
- [`tests/KirbyTaskbarBuddy.Tests/SettingsAndEnvironmentTests.cs`](tests/KirbyTaskbarBuddy.Tests/SettingsAndEnvironmentTests.cs) — Tests `AppSettings`, `RegistrySettingsService`, `DesktopEnvironmentMonitor`, `GlobalKeyboardHook`, `IdleActivityMonitor`, `KirbyOverlayWindow`, and `OverlayStage`.

---

## 4. Building & Running Tests

- **Build Test Project**:
  ```powershell
  dotnet build tests\KirbyTaskbarBuddy.Tests\KirbyTaskbarBuddy.Tests.csproj --no-restore -p:UseSharedCompilation=false -v minimal
  ```
- **Run All xUnit `[Fact]` Tests (32-bit PowerShell in-process runner)**:
  ```powershell
  & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -Command '
    $asm = [System.Reflection.Assembly]::LoadFrom("D:\NonWork\Projects\kirby-taskbar-buddy\tests\KirbyTaskbarBuddy.Tests\bin\Debug\net48\KirbyTaskbarBuddy.Tests.dll")
    $passed = 0; $failed = 0
    foreach ($type in $asm.GetTypes()) {
      if ($type.IsPublic -and -not $type.IsAbstract) {
        foreach ($m in $type.GetMethods()) {
          $hasFact = $m.GetCustomAttributes($false) | Where-Object { $_.GetType().Name -eq "FactAttribute" }
          if ($hasFact) {
            $inst = [Activator]::CreateInstance($type)
            try { $m.Invoke($inst, $null); $passed++ }
            catch { Write-Host "FAIL: $($type.Name).$($m.Name) -> $($_.Exception.InnerException.Message)"; $failed++ }
          }
        }
      }
    }
    Write-Host "Passed: $passed, Failed: $failed"
    if ($failed -gt 0) { exit 1 }
  '
  ```
- **Standalone `.exe` Build Verification (`build.bat` / `csc.exe`)**:
  ```powershell
  & "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x86 /optimize+ /out:KirbyTaskbarBuddy.test.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /recurse:src\*.cs
  Remove-Item KirbyTaskbarBuddy.test.exe -Force
  ```
