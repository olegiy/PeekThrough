using System;

namespace PeekThrough
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
    }
}
