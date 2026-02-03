# 👻 Ghost Window (PeekThrough)

A lightweight, professional Windows utility that enhances multitasking by allowing you to "see through" windows and interact with content behind them without switching applications.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078d7.svg)](https://www.microsoft.com/windows)
[![.NET Framework: 4.0](https://img.shields.io/badge/.NET_Framework-4.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net40)

## 🌟 Overview

**Ghost Window** (internally known as PeekThrough) is a system utility designed for power users, developers, and researchers. It solves the common friction of constant window switching (Alt+Tab) by providing a temporary "ghost mode" for any window.

When activated, the window under your cursor becomes semi-transparent and "click-through," allowing you to read data, compare documents, or even click buttons in the windows located directly behind it.

## 🚀 Key Features

- **Global Hotkey Integration**: Uses the native Windows key (`Win`) for a seamless experience.
- **Dynamic Transparency**: Instantly toggles 70% transparency (customizable in source).
- **Click-Through Functionality**: Mouse clicks pass through the "ghosted" window to the applications beneath.
- **Multi-Window Support**: Hold `Win` and hover over multiple windows to "ghost" them sequentially.
- **Intelligent Filtering**: Automatically ignores system elements like the Taskbar and Desktop.
- **Minimal Footprint**: Lightweight C# implementation with zero idle CPU usage.
- **Audio-Visual Feedback**: Professional sound signals and on-screen tooltips indicate mode status.

## 🛠️ Technical Stack

- **Language**: C#
- **Framework**: .NET Framework 4.0 (compatible with Windows 7, 8, 10, and 11)
- **API**: Extensive use of **WinAPI (P/Invoke)** for:
  - Low-level Keyboard Hooks (`WH_KEYBOARD_LL`)
  - Window Style Manipulation (`WS_EX_LAYERED`, `WS_EX_TRANSPARENT`)
  - Input Simulation

## 📖 Usage Guide

Ghost Window is designed to stay out of your way until you need it.

1. **Activate Ghost Mode**: Press and hold the **Windows Key (`LWin`)** for more than 0.5 seconds while hovering over a window.
   - The window will become transparent.
   - A tooltip `👻 Ghost Mode` will appear.
   - You will hear a confirmation beep.
2. **Interact Through Windows**: While holding `Win`, you can click on any content visible *behind* the ghosted window.
3. **Deactivate**: Simply release the **Windows Key**. The window(s) will instantly return to their original state.
4. **Standard Windows Key**: A quick tap of the `Win` key (less than 0.5s) still opens the Start Menu as usual.

## 📥 Installation

### For End-Users (Binary)
1. Download the latest `PeekThrough.exe` from the [Releases](https://github.com/olegiy/PeekThrough/releases) page.
2. Run the executable. No installation is required.
3. (Optional) Add a shortcut to your `Startup` folder to launch it with Windows.

### For Developers (Build from Source)
1. Clone the repository:
   ```bash
   git clone https://github.com/olegiy/PeekThrough.git
   ```
2. Open the project folder.
3. Build using the provided `compile.bat` or use the C# compiler directly:
   ```bash
   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:PeekThrough.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Program.cs NativeMethods.cs KeyboardHook.cs GhostLogic.cs DebugLogger.cs
   ```
4. Alternatively, open the source files in **Visual Studio** and build as a Windows Forms Application.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
1. Fork the repository.
2. Create your feature branch (`git checkout -b feature/AmazingFeature`).
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
*Created by [olegiy](https://github.com/olegiy)*
