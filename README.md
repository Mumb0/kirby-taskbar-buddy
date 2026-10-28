# <p align="center"><img src="assets/banner.png" alt="Kirby Taskbar Buddy" width="100%"></p>

<p align="center">
  <strong>A lightweight, portable desktop companion that strolls along your Windows taskbar.</strong><br>
  <em>Reacts to your typing with copy abilities, dodges your cursor, and leaps when you press space.</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?style=flat-square&logo=windows" alt="Windows" />
  <img src="https://img.shields.io/badge/Architecture-x32%20Single--File-FF69B4?style=flat-square" alt="Single File" />
  <img src="https://img.shields.io/badge/Telemetry-100%25%20Offline%20%26%20Private-2ea44f?style=flat-square" alt="Privacy" />
</p>

---

## <img src="assets/kirby-normal.png" width="32" height="32" valign="middle" alt=""> What is Kirby Taskbar Buddy?

Kirby Taskbar Buddy is a desktop companion application built for Windows. Kirby hangs out right above your taskbar on your primary monitor, keeping you company as you work, study, or game.

- **Transparent and Click-Through:** Built using a hardware-accelerated layered overlay (`WS_EX_TRANSPARENT`). Kirby never steals window focus, interrupts your typing, or blocks taskbar clicks.
- **Fullscreen Auto-Hide:** Automatically hides whenever you play games, watch fullscreen media, or enter presentations so he never gets in your way.
- **Completely Standalone:** All sprite assets are embedded directly into a single `.exe` binary. No installer, extra dependencies, or archive extractions needed.

---

## <img src="assets/kirby-fighter.png" width="32" height="32" valign="middle" alt=""> Features

### <img src="assets/kirby-sword.png" width="32" height="32" valign="middle" alt=""> Reactive Copy Abilities
As you type in any application (web browser, text editor, terminal, or chat), Kirby reacts to matching keys by dynamically transforming into one of **19 classic copy abilities**. Pressing the same ability key again returns him to Normal Kirby.

### <img src="assets/kirby-wing.png" width="32" height="32" valign="middle" alt=""> Spacebar Jump
Tapping <kbd>Space</kbd> anywhere on your system launches Kirby into the air with authentic gravity and somersault physics.

### <img src="assets/kirby-mirror.png" width="32" height="32" valign="middle" alt=""> Cursor Danger Guard
Move your mouse cursor close to Kirby while he is on the taskbar, and he will immediately raise a defensive shield. Mirror Kirby even summons a shimmering hexagonal barrier.

<p align="center">
  <img src="assets/abilities-showcase.png" alt="Kirby Abilities Showcase" style="max-width: 100%; height: auto;">
</p>

## <img src="assets/kirby-paint.png" width="32" height="32" valign="middle" alt=""> Copy Ability Cheat Sheet

Press any of the following keys anywhere on your system to transform Kirby:

<div align="center">

|                                   Icon                                    | Ability | Key | Icon | Ability | Key |
|:-------------------------------------------------------------------------:| :--- | :---: | :---: | :--- | :---: |
|  <img src="assets/kirby-normal.png" width="64" height="64" alt="Normal">  | Normal | <kbd>0</kbd> | <img src="assets/kirby-mirror.png" width="32" height="32" alt="Mirror"> | Mirror | <kbd>m</kbd> |
|    <img src="assets/kirby-beam.png" width="64" height="64" alt="Beam">    | Beam | <kbd>e</kbd> | <img src="assets/kirby-ninja.png" width="32" height="32" alt="Ninja"> | Ninja | <kbd>n</kbd> |
|    <img src="assets/kirby-bomb.png" width="64" height="64" alt="Bomb">    | Bomb | <kbd>b</kbd> | <img src="assets/kirby-paint.png" width="32" height="32" alt="Paint"> | Paint | <kbd>p</kbd> |
|  <img src="assets/kirby-cutter.png" width="64" height="64" alt="Cutter">  | Cutter | <kbd>c</kbd> | <img src="assets/kirby-parasol.png" width="32" height="32" alt="Parasol"> | Parasol | <kbd>r</kbd> |
| <img src="assets/kirby-fighter.png" width="64" height="64" alt="Fighter"> | Fighter | <kbd>f</kbd> | <img src="assets/kirby-plasma.png" width="32" height="32" alt="Plasma"> | Plasma | <kbd>a</kbd> |
|    <img src="assets/kirby-fire.png" width="64" height="64" alt="Fire">    | Fire | <kbd>d</kbd> | <img src="assets/kirby-stone.png" width="32" height="32" alt="Stone"> | Stone | <kbd>s</kbd> |
|  <img src="assets/kirby-hammer.png" width="64" height="64" alt="Hammer">  | Hammer | <kbd>h</kbd> | <img src="assets/kirby-suplex.png" width="32" height="32" alt="Suplex"> | Suplex | <kbd>u</kbd> |
|     <img src="assets/kirby-ice.png" width="64" height="64" alt="Ice">     | Ice | <kbd>l</kbd> | <img src="assets/kirby-sword.png" width="32" height="32" alt="Sword"> | Sword | <kbd>w</kbd> |
|     <img src="assets/kirby-jet.png" width="64" height="64" alt="Jet">     | Jet | <kbd>j</kbd> | <img src="assets/kirby-wheel.png" width="32" height="32" alt="Wheel"> | Wheel | <kbd>v</kbd> |
|    <img src="assets/kirby-wing.png" width="64" height="64" alt="Wing">    | Wing | <kbd>g</kbd> | <img src="assets/kirby-yoyo.png" width="32" height="32" alt="Yo-Yo"> | Yo-Yo | <kbd>y</kbd> |

</div>

<p align="center">
  <em>Press <kbd>Space</kbd> anywhere to make Kirby jump.</em>
</p>

---

## <img src="assets/kirby-jet.png" width="32" height="32" valign="middle" alt=""> System Tray Controls

Right-click the Kirby icon in your Windows system tray (near the clock) to access settings:

- **Kirby Size:** Switch between **2x (Default)**, **3x (Large)**, and **4x (Extra Large)** pixel-perfect scaling.
- **Show Foreground Taskbar Kirby:** Toggle Kirby's visibility on or off.
- **Pause Key Switching:** Keep Kirby walking without changing abilities as you type.
- **Start with Windows:** Configure Kirby to start automatically when logging into Windows.
- **Exit:** Gracefully closes the application.

---

## <img src="assets/kirby-stone.png" width="32" height="32" valign="middle" alt=""> Privacy and Security Guarantee

Because Kirby reacts to your keystrokes, the application registers a low-level Windows keyboard hook (`WH_KEYBOARD_LL`).

- **100% Local and Offline:** The application contains no networking code, sockets, or HTTP requests. It cannot connect to the internet.
- **No Keystroke Logging:** Keys are matched in-memory solely to switch animation states. No keystrokes are ever saved to disk, logged, or cached.
- **Standard User Permissions:** Runs entirely in unprivileged user space without requiring administrator rights.

---

## <img src="assets/kirby-parasol.png" width="32" height="32" valign="middle" alt=""> Installation

1. Go to the [Releases](https://github.com/Mumb0/kirby-taskbar-buddy/releases) page on GitHub.
2. Download `KirbyTaskbarBuddy.exe`.
3. Move the executable to any folder of your choice and double-click to run.

---

## <img src="assets/kirby-hammer.png" width="32" height="32" valign="middle" alt=""> Building from Source

To compile the standalone executable from source using Windows' built-in .NET C# compiler:

```cmd
git clone https://github.com/Mumb0/kirby-taskbar-buddy.git
cd kirby-taskbar-buddy
build.bat
```

The `build.bat` script automatically bundles all sprite resources into `KirbyTaskbarBuddy.exe`.

---

## <img src="assets/kirby-beam.png" width="32" height="32" valign="middle" alt=""> Disclaimer and Credits

- Kirby and related character designs are trademarks of Nintendo and HAL Laboratory, Inc.
- This is an unofficial, non-commercial fan-made desktop project.
