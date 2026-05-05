using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GhostThrough.Models
{
    /// <summary>
    /// Root settings container (v2 format - JSON)
    /// </summary>
    [DataContract]
    internal class Settings
    {
        [DataMember(Order = 1)]
        public int Version { get; set; }
        [DataMember(Order = 2)]
        public ActivationSettings Activation { get; set; }
        [DataMember(Order = 3)]
        public ProfileSettings Profiles { get; set; }
        [DataMember(Order = 4)]
        public HotkeySettings Hotkeys { get; set; }
        [DataMember(Order = 5)]
        public LoggingSettings Logging { get; set; }

        public Settings()
        {
            Version = 2;
            Activation = new ActivationSettings();
            Profiles = new ProfileSettings();
            Hotkeys = new HotkeySettings();
            Logging = new LoggingSettings();
        }
    }

    [DataContract]
    internal class ActivationSettings
    {
        [DataMember(Order = 1)]
        public string Type { get; set; }
        [DataMember(Order = 2)]
        public int KeyCode { get; set; }
        [DataMember(Order = 3)]
        public int MouseButton { get; set; }
        [DataMember(Order = 4)]
        public int ActivationDelayMs { get; set; }
        [DataMember(Order = 5)]
        public string Mode { get; set; }

        public ActivationSettings()
        {
            Type = "keyboard";
            KeyCode = NativeMethods.VK_LWIN;
            MouseButton = NativeMethods.VK_MBUTTON;
            ActivationDelayMs = ActivationStateManager.DEFAULT_ACTIVATION_DELAY_MS;
            Mode = "hold";
        }
    }

    [DataContract]
    internal class ProfileSettings
    {
        [DataMember(Order = 1)]
        public List<ProfileData> List { get; set; }
        [DataMember(Order = 2)]
        public string ActiveId { get; set; }

        public ProfileSettings()
        {
            List = OpacityProfilePresets.CreateDefaultProfileDataList();
            ActiveId = OpacityProfilePresets.DefaultActiveProfileId;
        }
    }

    [DataContract]
    internal class ProfileData
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }
        [DataMember(Order = 2)]
        public string Name { get; set; }
        [DataMember(Order = 3)]
        public byte Opacity { get; set; }
    }

    [DataContract]
    internal class HotkeySettings
    {
        [DataMember(Order = 1)]
        public HotkeyDefinition NextProfile { get; set; }
        [DataMember(Order = 2)]
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

    [DataContract]
    internal class HotkeyDefinition
    {
        [DataMember(Order = 1)]
        public bool Ctrl { get; set; }
        [DataMember(Order = 2)]
        public bool Shift { get; set; }
        [DataMember(Order = 3)]
        public bool Alt { get; set; }
        [DataMember(Order = 4)]
        public string Key { get; set; }
    }

    [DataContract]
    internal class LoggingSettings
    {
        [DataMember(Order = 1)]
        public string Level { get; set; }

        public LoggingSettings()
        {
            Level = DebugLogger.LEVEL_DEBUG;
        }
    }
}
