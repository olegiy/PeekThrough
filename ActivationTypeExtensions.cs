using System;

namespace GhostThrough
{
    internal static class ActivationTypeExtensions
    {
        public static ActivationInputType ToActivationInputType(this string value)
        {
            return string.Equals(value, "mouse", StringComparison.OrdinalIgnoreCase)
                ? ActivationInputType.Mouse
                : ActivationInputType.Keyboard;
        }

        public static string ToSettingsValue(this ActivationInputType value)
        {
            return value == ActivationInputType.Mouse ? "mouse" : "keyboard";
        }

        public static ActivationMode ToActivationMode(this string value)
        {
            return string.Equals(value, "click", StringComparison.OrdinalIgnoreCase)
                ? ActivationMode.Click
                : ActivationMode.Hold;
        }

        public static string ToSettingsValue(this ActivationMode value)
        {
            return value == ActivationMode.Click ? "click" : "hold";
        }

        public static ActivationKeyBehavior ToActivationKeyBehavior(this string value)
        {
            return string.Equals(value, "win-reverse", StringComparison.OrdinalIgnoreCase)
                ? ActivationKeyBehavior.WinReverse
                : ActivationKeyBehavior.Standard;
        }

        public static string ToSettingsValue(this ActivationKeyBehavior value)
        {
            return value == ActivationKeyBehavior.WinReverse ? "win-reverse" : "standard";
        }
    }
}
