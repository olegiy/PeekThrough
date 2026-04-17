using System.Collections.Generic;

namespace PeekThrough.Models
{
    /// <summary>
    /// Root settings container (v2 format - JSON)
    /// </summary>
    internal class Settings
    {
        public int Version { get; set; }
        public ActivationSettings Activation { get; set; }
        public ProfileSettings Profiles { get; set; }
        public HotkeySettings Hotkeys { get; set; }

        public Settings()
        {
            Version = 2;
            Activation = new ActivationSettings();
            Profiles = new ProfileSettings();
            Hotkeys = new HotkeySettings();
        }
    }

    internal class ActivationSettings
    {
        public string Type { get; set; }
        public int KeyCode { get; set; }
        public int MouseButton { get; set; }

        public ActivationSettings()
        {
            Type = "keyboard";
            KeyCode = NativeMethods.VK_LWIN;
            MouseButton = NativeMethods.VK_MBUTTON;
        }
    }

    internal class ProfileSettings
    {
        public List<ProfileData> List { get; set; }
        public string ActiveId { get; set; }

        public ProfileSettings()
        {
            List = new List<ProfileData>
            {
                new ProfileData { Id = "min", Name = "Minimum", Opacity = 38 },
                new ProfileData { Id = "med", Name = "Medium", Opacity = 128 },
                new ProfileData { Id = "max", Name = "Maximum", Opacity = 204 }
            };
            ActiveId = "min";
        }
    }

    internal class ProfileData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public byte Opacity { get; set; }
    }

    internal class HotkeySettings
    {
        public HotkeyDefinition NextProfile { get; set; }
        public HotkeyDefinition PrevProfile { get; set; }

        public HotkeySettings()
        {
            NextProfile = new HotkeyDefinition
            {
                Ctrl = true,
                Shift = true,
                Key = "Up"
            };
            PrevProfile = new HotkeyDefinition
            {
                Ctrl = true,
                Shift = true,
                Key = "Down"
            };
        }
    }

    internal class HotkeyDefinition
    {
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public string Key { get; set; }
    }
}
