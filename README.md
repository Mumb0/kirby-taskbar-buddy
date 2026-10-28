# <p align="center"><img src="assets/banner.png" alt="Kirby Taskbar Buddy" width="100%"></p>

<p align="center">
  <strong>A lightweight, portable desktop companion that strolls along your Windows taskbar.</strong><br>
  <em>Reacts to your typing with copy abilities, dodges your cursor, and leaps when you press space.</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?style=flat-square&logo=windows" alt="Windows" />
  <img src="https://img.shields.io/badge/Architecture-x64%20Single--File-FF69B4?style=flat-square" alt="Single File" />
  <img src="https://img.shields.io/badge/Telemetry-100%25%20Offline%20%26%20Private-2ea44f?style=flat-square" alt="Privacy" />
</p>

---

## <img src="assets/kirby-normal.png" height="28" alt=""> What is Kirby Taskbar Buddy?

Kirby Taskbar Buddy is a desktop companion application built for Windows. Kirby hangs out right above your taskbar on your primary monitor, keeping you company as you work, study, or game.

- **Transparent and Click-Through:** Built using a hardware-accelerated layered overlay (`WS_EX_TRANSPARENT`). Kirby never steals window focus, interrupts your typing, or blocks taskbar clicks.
- **Fullscreen Auto-Hide:** Automatically hides whenever you play games, watch fullscreen media, or enter presentations so he never gets in your way.
- **Completely Standalone:** All sprite assets are embedded directly into a single `.exe` binary. No installer, extra dependencies, or archive extractions needed.

---

## <img src="assets/kirby-fighter.png" height="28" alt=""> Features

### <img src="assets/kirby-sword.png" height="22" alt=""> Reactive Copy Abilities
As you type in any application (web browser, text editor, terminal, or chat), Kirby reacts to matching keys by dynamically transforming into one of **19 classic copy abilities**. Pressing the same ability key again returns him to Normal Kirby.

### <img src="assets/kirby-wing.png" height="22" alt=""> Spacebar Jump
Tapping <kbd>Space</kbd> anywhere on your system launches Kirby into the air with authentic gravity and somersault physics.

### <img src="assets/kirby-mirror.png" height="22" alt=""> Cursor Danger Guard
Move your mouse cursor close to Kirby while he is on the taskbar, and he will immediately raise a defensive shield. Mirror Kirby even summons a shimmering hexagonal barrier.

<p align="center">
  <img src="assets/abilities-showcase.png" alt="Kirby Abilities Showcase" style="max-width: 100%; height: auto;">
</p>

---

## <img src="assets/kirby-paint.png" height="28" alt=""> Copy Ability Cheat Sheet

Press any of the following keys anywhere on your system to transform Kirby:

<div align="center">

| Icon | Ability | Key | Icon | Ability | Key |
| :---: | :--- | :---: | :---: | :--- | :---: |
| <img src="assets/kirby-normal.png" height="28" alt="Normal"> | Normal | <kbd>0</kbd> | <img src="assets/kirby-mirror.png" height="28" alt="Mirror"> | Mirror | <kbd>m</kbd> |
| <img src="assets/kirby-beam.png" height="28" alt="Beam"> | Beam | <kbd>e</kbd> | <img src="assets/kirby-ninja.png" height="28" alt="Ninja"> | Ninja | <kbd>n</kbd> |
| <img src="assets/kirby-bomb.png" height="28" alt="Bomb"> | Bomb | <kbd>b</kbd> | <img src="assets/kirby-paint.png" height="28" alt="Paint"> | Paint | <kbd>p</kbd> |
| <img src="assets/kirby-cutter.png" height="28" alt="Cutter"> | Cutter | <kbd>c</kbd> | <img src="assets/kirby-parasol.png" height="28" alt="Parasol"> | Parasol | <kbd>r</kbd> |
| <img src="assets/kirby-fighter.png" height="28" alt="Fighter"> | Fighter | <kbd>f</kbd> | <img src="assets/kirby-plasma.png" height="28" alt="Plasma"> | Plasma | <kbd>a</kbd> |
| <img src="assets/kirby-fire.png" height="28" alt="Fire"> | Fire | <kbd>d</kbd> | <img src="assets/kirby-stone.png" height="28" alt="Stone"> | Stone | <kbd>s</kbd> |
| <img src="assets/kirby-hammer.png" height="28" alt="Hammer"> | Hammer | <kbd>h</kbd> | <img src="assets/kirby-suplex.png" height="28" alt="Suplex"> | Suplex | <kbd>u</kbd> |
| <img src="assets/kirby-ice.png" height="28" alt="Ice"> | Ice | <kbd>l</kbd> | <img src="assets/kirby-sword.png" height="28" alt="Sword"> | Sword | <kbd>w</kbd> |
| <img src="assets/kirby-jet.png" height="28" alt="Jet"> | Jet | <kbd>j</kbd> | <img src="assets/kirby-wheel.png" height="28" alt="Wheel"> | Wheel | <kbd>v</kbd> |
| <img src="assets/kirby-wing.png" height="28" alt="Wing"> | Wing | <kbd>g</kbd> | <img src="assets/kirby-yoyo.png" height="28" alt="Yo-Yo"> | Yo-Yo | <kbd>y</kbd> |

</div>

<p align="center">
  <em>Press <kbd>Space</kbd> anywhere to make Kirby jump.</em>
</p>

---

## <img src="assets/kirby-jet.png" height="28" alt=""> System Tray Controls

Right-click the Kirby icon in your Windows system tray (near the clock) to access settings:

- **Kirby Size:** Switch between **2x (Default)**, **3x (Large)**, and **4x (Extra Large)** pixel-perfect scaling.
- **Show Foreground Taskbar Kirby:** Toggle Kirby's visibility on or off.
- **Pause Key Switching:** Keep Kirby walking without changing abilities as you type.
- **Start with Windows:** Configure Kirby to start automatically when logging into Windows.
- **Exit:** Gracefully closes the application.

---

## <img src="assets/kirby-stone.png" height="28" alt=""> Privacy and Security Guarantee

Because Kirby reacts to your keystrokes, the application registers a low-level Windows keyboard hook (`WH_KEYBOARD_LL`).

- **100% Local and Offline:** The application contains no networking code, sockets, or HTTP requests. It cannot connect to the internet.
- **No Keystroke Logging:** Keys are matched in-memory solely to switch animation states. No keystrokes are ever saved to disk, logged, or cached.
- **Standard User Permissions:** Runs entirely in unprivileged user space without requiring administrator rights.

---

## <img src="assets/kirby-parasol.png" height="28" alt=""> Installation

1. Go to the [Releases](https://github.com/Mumb0/kirby-taskbar-buddy/releases) page on GitHub.
2. Download `KirbyTaskbarBuddy.exe`.
3. Move the executable to any folder of your choice and double-click to run.

---

## <img src="assets/kirby-hammer.png" height="28" alt=""> Building from Source

To compile the standalone executable from source using Windows' built-in .NET C# compiler:

```cmd
git clone https://github.com/Mumb0/kirby-taskbar-buddy.git
cd kirby-taskbar-buddy
build.bat
```

The `build.bat` script automatically bundles all sprite resources into `KirbyTaskbarBuddy.exe`.

---

## <img src="assets/kirby-beam.png" height="28" alt=""> Disclaimer and Credits

- Kirby and related character designs are trademarks of Nintendo and HAL Laboratory, Inc.
- This is an unofficial, non-commercial fan-made desktop project.
