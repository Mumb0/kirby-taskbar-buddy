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
   - Pre-allocate scratch buffers (`int[]`), reusable `SpriteTile` instances, and array pools at startup. Never allocate bitmaps, arrays, or closures inside `OnTick` or per-frame rendering methods.
4. **Pure Static / Testable Logic**:
   - Keep physics, state-machine transitions, coordinate math, and spritesheet slicing free of WinForms `Timer` or `NotifyIcon` side effects so xUnit tests can exercise them deterministically.

---

## 2. Directory & File Map (`src/`)

### Entry Point
- [`src/Program.cs`](src/Program.cs) — `Program.Main`: DPI awareness (`SetProcessDPIAware`), single-instance `Mutex`, exception logging (`crash.log`), and `Application.Run(new KirbyTaskbarBuddyContext())`.
- [`src/KirbyTaskbarBuddyContext.cs`](src/KirbyTaskbarBuddyContext.cs) — Main `ApplicationContext` orchestrating the 60 Hz tick loop, tray icon, global keyboard hook, registry settings, and actor/combat state machines (being decomposed across Phases 3–4).

### `src/Core/` — Shared Constants & Diagnostics
- [`src/Core/CanvasConstants.cs`](src/Core/CanvasConstants.cs) — `CanvasConstants`:
  - `TILE_SIZE = 80` (80×80 canvas size)
  - `PIVOT_X = 40`, `PIVOT_Y = 54` (Kirby's foot anchor at `(40, 54)`)
  - `INHALE_MOUTH_X = 14`, `INHALE_MOUTH_Y = 39` (inhale mouth target offset in 1× sprite pixels)
- [`src/Core/Diagnostics.cs`](src/Core/Diagnostics.cs) — `Diagnostics`:
  - `LogWarning(string context, Exception ex)` — appends non-fatal warnings to `crash.log`
  - `TrimMemory()` — triggers GC compaction + `SetProcessWorkingSetSize`

### `src/Catalog/` — Sprite Catalog, Clips, Forms, Enemies & Key Bindings
- [`src/Catalog/AnimClip.cs`](src/Catalog/AnimClip.cs):
  - `PivotMode` enum (`BottomCenter`, `Center`, `Custom`)
  - `AnimClip` class: `Speed`, `Loop`, `Pivot`, `CustomPivotX`, `CustomPivotY`, `RawFrames`, `Frames`, `HasFrames(int minCount = 1)`, `GetFrame(double animTime)` (instance + static), `GetVfxFrame(double animTime)` (instance + static)
- [`src/Catalog/FormDef.cs`](src/Catalog/FormDef.cs):
  - `SwallowParticleLayer` struct (`Rectangles`, `StartFrame`, `EndFrame`, `Offsets`, `Pivot`, `CustomPivotX`, `CustomPivotY`)
  - `FormDef` class (`Name`, `Key`, `Walk`, `Jump`, `Stand`, `Guard`, `Inhale`, `MouthfulWalk`, `MouthfulJump`, `Hit`, `Swallow`, `AbilityAttack`, `AbilityVfx`, `AbilityVfxOffsetX`, `AbilityVfxOffsetY`)
  - `EnemyDef` class (`Name`, `GrantedFormName`, `WalkSpeed`, `Walk`, `Inhaled`)
- [`src/Catalog/FormDefinitions.cs`](src/Catalog/FormDefinitions.cs) — `FormDefinitions`:
  - Shared sprite rects & sequences: `NormalTrayIconRect`, `MirrorGuardBodyRect`, `MirrorGuardBubbleRect`, `SLEEP_PIVOT_X`, `SLEEP_PIVOT_Y`, `SwallowEmptySeq`, `SwallowAbilitySeq`
  - Catalog builders: `BuildForms(FastSheet, out SpriteTile[] sleepFrames)`, `BuildEnemies(FastSheet)`
  - Lookup helpers: `ResolveNormalForm(IList<FormDef>, FormDef fallback)`, `ResolveSwallowedForm(IList<FormDef>, FormDef currentForm, EnemyDef swallowedEnemy)`
- [`src/Catalog/KeyBindings.cs`](src/Catalog/KeyBindings.cs) — `KeyBindings`:
  - `VkCodeToName(int vkCode)` — maps Win32 virtual-key codes (`0`–`9`, `Numpad0`–`Numpad9`, `A`–`Z`) to form key strings
  - `ResolveNextForm(IList<FormDef> forms, FormDef currentForm, string keyName)` — resolves next form (cycling forms sharing the same key)

### `src/Rendering/` — Spritesheet Slicing, Layered Windows & Visual Effects
- [`src/Rendering/SpriteTile.cs`](src/Rendering/SpriteTile.cs) — `SpriteTile`:
  - `PremulPixels` (`int[]` in `0xAARRGGBB` premultiplied BGRA), `MinX`, `MinY`, `Width`, `Height`
- [`src/Rendering/FastSheet.cs`](src/Rendering/FastSheet.cs) — `FastSheet`:
  - Locks PNG into flat `Pixels` array once, detects `BgColor` at `(0, 0)`
- [`src/Rendering/SpritesheetCleaner.cs`](src/Rendering/SpritesheetCleaner.cs) — `SpritesheetCleaner`:
  - `CleanSpritesheets(string assetsDir)` and CLI `Main(string[] args)` used by [`clean-sprites.bat`](clean-sprites.bat)
- [`src/Rendering/SpriteLoader.cs`](src/Rendering/SpriteLoader.cs) — `SpriteLoader`:
  - `ExtractClip`, `ExtractComposedClip`, `ExtractSleepClip`, `ExtractMirrorGuardClip`, `ExtractSwallowClip`, `ExtractRawTile`, `CreateTrayIcon`
- [`src/Rendering/ISpriteLayer.cs`](src/Rendering/ISpriteLayer.cs) — `ISpriteLayer`:
  - Interface for per-pixel alpha overlay layers: `IsCleared`, `UpdateSpriteFrame`, `RenderAtTaskbar`, `ClearOverlay`
- [`src/Rendering/KirbyOverlayWindow.cs`](src/Rendering/KirbyOverlayWindow.cs) — `KirbyOverlayWindow : Form, ISpriteLayer`:
  - Win32 layered window (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE`) with dirty-checked `RenderAtTaskbar`, cached 32bpp DIB section, and `UpdateLayeredWindow` blitting
- [`src/Rendering/OverlayStage.cs`](src/Rendering/OverlayStage.cs) — `OverlayStage : IDisposable`:
  - Owns the 3 back-to-front `KirbyOverlayWindow` layers (`EnemyLayer`, `KirbyLayer`, `VfxLayer`), `ClearAll(int scale)`, `RefreshTopmostOrder()`, `OwnsWindowHandle(IntPtr)`, and `BeginInvokeOnUiThread(EventHandler)`
- [`src/Rendering/IVfxEffect.cs`](src/Rendering/IVfxEffect.cs):
  - `IVfxEffect` interface (`OffsetX`, `OffsetY`, `Step(double delta60)`, `Render()`)
  - `SpriteClipVfxEffect : IVfxEffect` — plays sprite-sheet `AnimClip` visual effects (e.g., copy ability attacks) anchored to Kirby
- [`src/Rendering/InhaleParticleEffect.cs`](src/Rendering/InhaleParticleEffect.cs):
  - `InhaleParticle` struct (`Dx`, `Dy`, `BlackOnTop`)
  - `InhaleParticleEffect : IVfxEffect` — procedural 4-particle inhale wind lines (`PARTICLE_COUNT`, `PARTICLE_SPEED`, `SpawnParticle`, `InitParticles`, `StepParticles`, `RenderParticlesTile`, double-buffered `SpriteTile` rendering)

### `src/Platform/` — Win32 P/Invoke & OS Integration
- [`src/Platform/NativeMethods.cs`](src/Platform/NativeMethods.cs) — `NativeMethods`:
  - Pure Win32 (`user32.dll`, `gdi32.dll`, `kernel32.dll`) P/Invoke signatures, structs (`POINT`, `SIZE`, `BLENDFUNCTION`, `BITMAPINFO`, `RECT`, `LASTINPUTINFO`), and window style constants

---

## 3. Building & Running Tests

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
