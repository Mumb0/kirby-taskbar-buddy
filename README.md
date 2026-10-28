# <p align="center"><img src="assets/banner.png" alt="Kirby Taskbar Buddy" width="100%"></p>

<p align="center">
  <strong>A cheerful, lightweight desktop companion that strolls along your Windows taskbar!</strong><br>
  <em>Reacts to your typing with copy abilities, dodges your cursor, and leaps when you press space!</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows" alt="Windows" />
  <img src="https://img.shields.io/badge/Architecture-x64%20Single--File-FF69B4?style=for-the-badge" alt="Single File" />
  <img src="https://img.shields.io/badge/Telemetry-100%25%20Offline%20%26%20Private-success?style=for-the-badge" alt="Privacy" />
  <img src="https://img.shields.io/badge/Poyo-100%25-ffb6c1?style=for-the-badge" alt="Poyo" />
</p>

---

<p align="center">
  <img src="assets/abilities-showcase.png" alt="Kirby Abilities Showcase" width="640">
</p>

## ✨ What is Kirby Taskbar Buddy?

**Kirby Taskbar Buddy** is a nostalgic desktop pet built for Windows. Kirby hangs out right above your taskbar on your primary monitor, keeping you company throughout your workday, study sessions, or gaming breaks.

* **🖱️ Transparent & Click-Through:** Uses a hardware-accelerated layered overlay (`WS_EX_TRANSPARENT`). Kirby never steals window focus, interrupts your typing, or blocks your taskbar clicks.
* **🎮 Fullscreen Auto-Hide:** Automatically hides whenever you play games, watch fullscreen movies, or give a presentation.
* **🚀 Completely Standalone:** All retro sprites are embedded directly into a single `.exe`. No installer, dependencies, or unzipping required.

---

## 🌟 Interactive Features

### ⌨️ Reactive Copy Abilities
As you type in **any** application (chatting with friends, writing documents, or coding), Kirby reacts to your letters by dynamically transforming into **19 classic copy abilities**! Pressing the same ability key again returns him to Normal Kirby.

### 🦘 Spacebar Somersault
Whenever you tap <kbd>Space</kbd>, Kirby launches into the air with authentic gravity and somersault jump physics!

### 🛡️ Cursor Danger Guard
Move your mouse cursor close to Kirby while he is on the ground, and he'll instantly trigger his defensive shield pose! Mirror Kirby even summons his iconic rainbow hexagonal forcefield.

---

## 🎨 Copy Ability Cheat Sheet

<div align="center">

| | Ability | Key | | Ability | Key |
| :---: | :--- | :---: | :---: | :--- | :---: |
| <img src="assets/kirby-normal.png" width="32"> | **Normal** | <kbd>0</kbd> | <img src="assets/kirby-mirror.png" width="32"> | **Mirror** | <kbd>m</kbd> |
| <img src="assets/kirby-sword.png" width="32"> | **Sword** | <kbd>w</kbd> | <img src="assets/kirby-ninja.png" width="32"> | **Ninja** | <kbd>n</kbd> |
| <img src="assets/kirby-fire.png" width="32"> | **Fire** | <kbd>d</kbd> | <img src="assets/kirby-paint.png" width="32"> | **Paint** | <kbd>p</kbd> |
| <img src="assets/kirby-beam.png" width="32"> | **Beam** | <kbd>e</kbd> | <img src="assets/kirby-parasol.png" width="32"> | **Parasol** | <kbd>r</kbd> |
| <img src="assets/kirby-cutter.png" width="32"> | **Cutter** | <kbd>c</kbd> | <img src="assets/kirby-plasma.png" width="32"> | **Plasma** | <kbd>a</kbd> |
| <img src="assets/kirby-fighter.png" width="32"> | **Fighter** | <kbd>f</kbd> | <img src="assets/kirby-stone.png" width="32"> | **Stone** | <kbd>s</kbd> |
| <img src="assets/kirby-hammer.png" width="32"> | **Hammer** | <kbd>h</kbd> | <img src="assets/kirby-suplex.png" width="32"> | **Suplex** | <kbd>u</kbd> |
| <img src="assets/kirby-ice.png" width="32"> | **Ice** | <kbd>l</kbd> | <img src="assets/kirby-wheel.png" width="32"> | **Wheel** | <kbd>v</kbd> |
| <img src="assets/kirby-jet.png" width="32"> | **Jet** | <kbd>j</kbd> | <img src="assets/kirby-wing.png" width="32"> | **Wing** | <kbd>g</kbd> |
| <img src="assets/kirby-bomb.png" width="32"> | **Bomb** | <kbd>b</kbd> | <img src="assets/kirby-yoyo.png" width="32"> | **Yo-Yo** | <kbd>y</kbd> |

</div>

<p align="center">
  <em>...plus tap <kbd>Space</kbd> anywhere to make Kirby jump!</em>
</p>

---

## ⚙️ System Tray Controls

Look for Kirby in your Windows system tray (near the clock). Right-click his icon to access settings:

* **Kirby Size:** Switch between **2x (Default)**, **3x (Large)**, and **4x (Extra Large)** pixel-perfect scaling.
* **Show Foreground Taskbar Kirby:** Toggle Kirby's visibility on/off.
* **Pause Key Switching:** Keep Kirby walking without changing abilities as you type.
* **Start with Windows:** Set Kirby to automatically greet you upon logging into Windows.
* **Exit:** Gracefully closes the buddy.

---

## 🔒 Privacy & Security Guarantee

Because Kirby reacts to your keystrokes, Windows uses a low-level keyboard hook (`WH_KEYBOARD_LL`). We believe in complete transparency:

* 🛡️ **100% Local & Offline:** The application does not contain any networking libraries, HTTP connections, or telemetry. It cannot communicate with the internet.
* 🛡️ **No Keystroke Logging:** Keys are matched in-memory strictly to select animation states. Nothing is ever saved to disk, cached, or recorded.
* 🛡️ **Zero Administrative Rights Required:** Runs entirely in standard user space.

---

## 📥 Installation

1. Go to the [**Releases**](https://github.com/Mumb0/kirby-taskbar-buddy/releases) section on GitHub.
2. Download `KirbyTaskbarBuddy.exe`.
3. Move it to any folder you like (e.g., `Documents` or a dedicated apps folder) and double-click to run!

---

## 🛠️ Building from Source

To compile the single-file executable yourself:

```cmd
git clone https://github.com/Mumb0/kirby-taskbar-buddy.git
cd kirby-taskbar-buddy
build.bat
```

The `build.bat` script uses Windows' built-in .NET C# compiler (`csc.exe`) to embed the sprites and produce a standalone `KirbyTaskbarBuddy.exe`.

---

## 💖 Disclaimer & Credits

* Kirby and all related characters and sprite assets are trademarks of **Nintendo** and **HAL Laboratory, Inc.**
* This is an unofficial, non-commercial fan-made companion created purely for desktop enjoyment.
