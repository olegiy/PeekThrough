using System.Collections.Generic;

namespace PeekThrough
{
    internal static class ActivationKeyCatalog
    {
        private static readonly Dictionary<int, string> KeyDisplayNames = new Dictionary<int, string>
        {
            { NativeMethods.VK_LWIN, "Left Win" },
            { NativeMethods.VK_RWIN, "Right Win" },
            { NativeMethods.VK_LCONTROL, "Left Ctrl" },
            { NativeMethods.VK_RCONTROL, "Right Ctrl" },
            { NativeMethods.VK_LMENU, "Left Alt" },
            { NativeMethods.VK_RMENU, "Right Alt" },
            { NativeMethods.VK_LSHIFT, "Left Shift" },
            { NativeMethods.VK_RSHIFT, "Right Shift" },
            { NativeMethods.VK_CAPITAL, "Caps Lock" },
            { NativeMethods.VK_TAB, "Tab" },
            { NativeMethods.VK_SPACE, "Space" },
            { NativeMethods.VK_ESCAPE, "Escape" },
            { NativeMethods.VK_OEM_3, "Tilde (`~)" },
            { NativeMethods.VK_INSERT, "Insert" },
            { NativeMethods.VK_DELETE, "Delete" },
            { NativeMethods.VK_HOME, "Home" },
            { NativeMethods.VK_END, "End" },
            { NativeMethods.VK_PRIOR, "Page Up" },
            { NativeMethods.VK_NEXT, "Page Down" },
        };

        private static readonly int[] Keys = new[]
        {
            NativeMethods.VK_LWIN, NativeMethods.VK_RWIN,
            NativeMethods.VK_CAPITAL, NativeMethods.VK_TAB,
            NativeMethods.VK_SPACE, NativeMethods.VK_ESCAPE,
            NativeMethods.VK_OEM_3, NativeMethods.VK_INSERT,
            NativeMethods.VK_DELETE, NativeMethods.VK_HOME,
            NativeMethods.VK_END, NativeMethods.VK_PRIOR,
            NativeMethods.VK_NEXT, 0x30, 0x31, 0x32, 0x33,
            0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
            0x70, 0x71, 0x72, 0x73, 0x74, 0x75,
            0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B,
        };

        public static IReadOnlyList<int> AvailableKeys
        {
            get { return Keys; }
        }

        public static string GetDisplayName(int vkCode)
        {
            string name;
            return KeyDisplayNames.TryGetValue(vkCode, out name)
                ? name
                : string.Format("Key 0x{0:X2}", vkCode);
        }
    }
}
