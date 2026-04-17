# 👻 Ghost Window (PeekThrough)

A lightweight, professional Windows utility that enhances multitasking by allowing you to "see through" windows and interact with content behind them without switching applications.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078d7.svg)](https://www.microsoft.com/windows)
[![.NET Framework: 4.0](https://img.shields.io/badge/.NET_Framework-4.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net40)

## 🌟 Overview

**Ghost Window** (internally known as PeekThrough) is a system utility designed for power users, developers, and researchers. It solves the common friction of constant window switching (Alt+Tab) by providing a temporary "ghost mode" for any window.

When activated, the window under your cursor becomes semi-transparent and "click-through," allowing you to read data, compare documents, or even click buttons in the windows located directly behind it.

## 🚀 Key Features

- **Multiple Activation Methods**: Choose between keyboard (`Win` key) or mouse button (middle, right, X1, X2) for activation.
- **Dynamic Transparency**: Instantly toggles 70% transparency (customizable in source).
- **Click-Through Functionality**: Mouse clicks pass through the "ghosted" window to the applications beneath.
- **Multi-Window Support**: Hold activation button and hover over multiple windows to "ghost" them sequentially.
- **Intelligent Filtering**: Automatically ignores system elements like the Taskbar and Desktop.
- **Minimal Footprint**: Lightweight C# implementation with zero idle CPU usage.
- **Audio-Visual Feedback**: Professional sound signals and on-screen tooltips indicate mode status.
- **Customizable Settings**: Change activation method through system tray menu.

## 🛠️ Technical Stack

- **Language**: C#
- **Framework**: .NET Framework 4.0 (compatible with Windows 7, 8, 10, and 11)
- **API**: Extensive use of **WinAPI (P/Invoke)** for:
  - Low-level Keyboard Hooks (`WH_KEYBOARD_LL`)
  - Window Style Manipulation (`WS_EX_LAYERED`, `WS_EX_TRANSPARENT`)
  - Input Simulation

## 📖 Usage Guide

Ghost Window is designed to stay out of your way until you need it.

1. **Choose Activation Method**: Right-click the system tray icon and select your preferred activation method:
   - **Keyboard**: Hold the **Windows Key (`LWin`)** for more than 1 second while hovering over a window.
   - **Mouse**: Hold the selected mouse button (middle, right, X1, or X2) for more than 1 second while hovering over a window.
2. **Activate Ghost Mode**:
   - The window will become transparent.
   - A tooltip `👻 Ghost Mode` will appear.
   - You will hear a confirmation beep.
3. **Interact Through Windows**: While holding the activation button, you can click on any content visible *behind* the ghosted window.
4. **Deactivate**: Simply release the activation button. The window(s) will instantly return to their original state.
5. **Switch Activation Method**: Right-click the system tray icon, select "Change Activation Method" and choose your preferred activation method.

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
