using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text;

namespace PeekThrough
{
    internal static class NativeMethods
    {
        // Hooking
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        
        // Window Info & Styles
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Point Point);
        
        // 32-bit versions
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLong64(IntPtr hWnd, int nIndex);
        
        // Unified GetWindowLongPtr for x86/x64
        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLong64(hWnd, nIndex);
            else
                return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }
        
        // 32-bit versions
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLong64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        // Unified SetWindowLongPtr for x86/x64
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLong64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
        
        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
        
        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        
        // Input Injection
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out Point lpPoint);
        
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Beep(int frequency, int duration);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);
        
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
        
        // Constants
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_MBUTTONUP = 0x0208;
        public const int WM_XBUTTONDOWN = 0x020B;
        public const int WM_XBUTTONUP = 0x020C;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;
        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;
        public const int VK_MBUTTON = 0x04;
        public const int VK_XBUTTON1 = 0x05;
        public const int VK_XBUTTON2 = 0x06;
        public const int VK_LMENU = 0xA4; // Left Alt
        public const int VK_RMENU = 0xA5; // Right Alt
        public const int VK_LSHIFT = 0xA0;
        public const int VK_RSHIFT = 0xA1;
        public const int VK_CAPITAL = 0x14;
        public const int VK_TAB = 0x09;
        public const int VK_SPACE = 0x20;
        public const int VK_INSERT = 0x2D;
        public const int VK_DELETE = 0x2E;
        public const int VK_HOME = 0x24;
        public const int VK_END = 0x23;
        public const int VK_PRIOR = 0x21; // Page Up
        public const int VK_NEXT = 0x22;  // Page Down
        public const int VK_UP = 0x26;
        public const int VK_DOWN = 0x28;
        public const int VK_OEM_3 = 0xC0; // Tilde/Backtick (`~)
        public const int XBUTTON1 = 0x0001;
        public const int XBUTTON2 = 0x0002;
        
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int LWA_ALPHA = 0x2;
        
        public const int GA_ROOT = 2; // GA_ROOT
        
        // Структура для low-level keyboard hook
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        // Структура для low-level mouse hook
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public Point pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size { get { return Marshal.SizeOf(typeof(INPUT)); } }
        }
        
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
        
        public const int INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        
        // Magic value to tag our own injected inputs
        public static readonly IntPtr INJECTED_BY_US = new IntPtr(0xDEADBEEF);
    }
}
