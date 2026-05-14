using System.Collections.Generic;

namespace GhostThrough
{
    internal static class ActivationKeyCatalog
    {
        internal sealed class ActivationKeyChoice
        {
            public int KeyCode { get; private set; }
            public ActivationKeyBehavior Behavior { get; private set; }
            public string DisplayName { get; private set; }

            public ActivationKeyChoice(int keyCode, ActivationKeyBehavior behavior, string displayName)
            {
                KeyCode = keyCode;
                Behavior = behavior;
                DisplayName = displayName;
            }
        }

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
            { 0x30, "0" },
            { 0x31, "1" },
            { 0x32, "2" },
            { 0x33, "3" },
            { 0x34, "4" },
            { 0x35, "5" },
            { 0x36, "6" },
            { 0x37, "7" },
            { 0x38, "8" },
            { 0x39, "9" },
            { 0x70, "F1" },
            { 0x71, "F2" },
            { 0x72, "F3" },
            { 0x73, "F4" },
            { 0x74, "F5" },
            { 0x75, "F6" },
            { 0x76, "F7" },
            { 0x77, "F8" },
            { 0x78, "F9" },
            { 0x79, "F10" },
            { 0x7A, "F11" },
            { 0x7B, "F12" },
        };

        private static readonly ActivationKeyChoice[] Choices = new[]
        {
            new ActivationKeyChoice(NativeMethods.VK_LWIN, ActivationKeyBehavior.Standard, "Win standard"),
            new ActivationKeyChoice(NativeMethods.VK_LWIN, ActivationKeyBehavior.WinReverse, "Win reverse"),
            new ActivationKeyChoice(NativeMethods.VK_RWIN, ActivationKeyBehavior.Standard, "Right Win"),
            new ActivationKeyChoice(NativeMethods.VK_CAPITAL, ActivationKeyBehavior.Standard, "Caps Lock"),
            new ActivationKeyChoice(NativeMethods.VK_TAB, ActivationKeyBehavior.Standard, "Tab"),
            new ActivationKeyChoice(NativeMethods.VK_SPACE, ActivationKeyBehavior.Standard, "Space"),
            new ActivationKeyChoice(NativeMethods.VK_ESCAPE, ActivationKeyBehavior.Standard, "Escape"),
            new ActivationKeyChoice(NativeMethods.VK_OEM_3, ActivationKeyBehavior.Standard, "Tilde (`~)"),
            new ActivationKeyChoice(NativeMethods.VK_INSERT, ActivationKeyBehavior.Standard, "Insert"),
            new ActivationKeyChoice(NativeMethods.VK_DELETE, ActivationKeyBehavior.Standard, "Delete"),
            new ActivationKeyChoice(NativeMethods.VK_HOME, ActivationKeyBehavior.Standard, "Home"),
            new ActivationKeyChoice(NativeMethods.VK_END, ActivationKeyBehavior.Standard, "End"),
            new ActivationKeyChoice(NativeMethods.VK_PRIOR, ActivationKeyBehavior.Standard, "Page Up"),
            new ActivationKeyChoice(NativeMethods.VK_NEXT, ActivationKeyBehavior.Standard, "Page Down"),
            new ActivationKeyChoice(0x30, ActivationKeyBehavior.Standard, "0"),
            new ActivationKeyChoice(0x31, ActivationKeyBehavior.Standard, "1"),
            new ActivationKeyChoice(0x32, ActivationKeyBehavior.Standard, "2"),
            new ActivationKeyChoice(0x33, ActivationKeyBehavior.Standard, "3"),
            new ActivationKeyChoice(0x34, ActivationKeyBehavior.Standard, "4"),
            new ActivationKeyChoice(0x35, ActivationKeyBehavior.Standard, "5"),
            new ActivationKeyChoice(0x36, ActivationKeyBehavior.Standard, "6"),
            new ActivationKeyChoice(0x37, ActivationKeyBehavior.Standard, "7"),
            new ActivationKeyChoice(0x38, ActivationKeyBehavior.Standard, "8"),
            new ActivationKeyChoice(0x39, ActivationKeyBehavior.Standard, "9"),
            new ActivationKeyChoice(0x70, ActivationKeyBehavior.Standard, "F1"),
            new ActivationKeyChoice(0x71, ActivationKeyBehavior.Standard, "F2"),
            new ActivationKeyChoice(0x72, ActivationKeyBehavior.Standard, "F3"),
            new ActivationKeyChoice(0x73, ActivationKeyBehavior.Standard, "F4"),
            new ActivationKeyChoice(0x74, ActivationKeyBehavior.Standard, "F5"),
            new ActivationKeyChoice(0x75, ActivationKeyBehavior.Standard, "F6"),
            new ActivationKeyChoice(0x76, ActivationKeyBehavior.Standard, "F7"),
            new ActivationKeyChoice(0x77, ActivationKeyBehavior.Standard, "F8"),
            new ActivationKeyChoice(0x78, ActivationKeyBehavior.Standard, "F9"),
            new ActivationKeyChoice(0x79, ActivationKeyBehavior.Standard, "F10"),
            new ActivationKeyChoice(0x7A, ActivationKeyBehavior.Standard, "F11"),
            new ActivationKeyChoice(0x7B, ActivationKeyBehavior.Standard, "F12"),
        };

        public static IReadOnlyList<ActivationKeyChoice> AvailableChoices
        {
            get { return Choices; }
        }

        public static IReadOnlyList<int> AvailableKeys
        {
            get
            {
                var keys = new List<int>();
                foreach (ActivationKeyChoice choice in Choices)
                {
                    if (!keys.Contains(choice.KeyCode))
                        keys.Add(choice.KeyCode);
                }
                return keys;
            }
        }

        public static bool IsSupportedKey(int vkCode)
        {
            foreach (ActivationKeyChoice choice in Choices)
            {
                if (choice.KeyCode == vkCode)
                    return true;
            }

            return false;
        }

        public static string GetDisplayName(int vkCode)
        {
            string name;
            return KeyDisplayNames.TryGetValue(vkCode, out name)
                ? name
                : string.Format("Key 0x{0:X2}", vkCode);
        }

        public static string GetDisplayName(int vkCode, ActivationKeyBehavior behavior)
        {
            foreach (ActivationKeyChoice choice in Choices)
            {
                if (choice.KeyCode == vkCode && choice.Behavior == behavior)
                    return choice.DisplayName;
            }

            return GetDisplayName(vkCode);
        }
    }
}
