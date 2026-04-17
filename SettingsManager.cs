using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using PeekThrough.Models;

namespace PeekThrough
{
    /// <summary>
    /// Loads, saves, and migrates settings from v1 (pseudo-JSON) to v2 (real JSON)
    /// </summary>
    internal class SettingsManager
    {
        private readonly string _settingsPath;
        private readonly JavaScriptSerializer _serializer;

        public SettingsManager(string settingsPath)
        {
            _settingsPath = settingsPath;
            _serializer = new JavaScriptSerializer();
        }

        public Settings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    DebugLogger.Log("SettingsManager: No settings file found, using defaults");
                    return CreateDefaultSettings();
                }

                string content = File.ReadAllText(_settingsPath);

                // Check if it's v1 format (contains "ActivationKeyCode:" style lines)
                if (IsV1Format(content))
                {
                    DebugLogger.Log("SettingsManager: Detected v1 format, migrating...");
                    var v1 = ParseV1Format(content);
                    var v2 = ConvertToV2(v1);

                    // Backup old settings
                    string backupPath = _settingsPath + ".bak";
                    File.Move(_settingsPath, backupPath);
                    DebugLogger.Log(string.Format("SettingsManager: Backed up v1 settings to {0}", backupPath));

                    // Save v2 format
                    SaveSettings(v2);
                    return v2;
                }

                // Parse v2 JSON format
                var settings = _serializer.Deserialize<Settings>(content) ?? CreateDefaultSettings();
                bool settingsChanged = NormalizeProfiles(settings);
                DebugLogger.Log(string.Format("SettingsManager: Loaded v2 settings, active profile: {0}", settings.Profiles.ActiveId));
                if (settingsChanged)
                {
                    SaveSettings(settings);
                    DebugLogger.Log("SettingsManager: Updated opacity profile presets");
                }
                return settings;
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("SettingsManager ERROR: {0}", ex.Message));
                return CreateDefaultSettings();
            }
        }

        public void SaveSettings(Settings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = _serializer.Serialize(settings);
                File.WriteAllText(_settingsPath, json);
                DebugLogger.Log("SettingsManager: Settings saved");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("SettingsManager.Save ERROR: {0}", ex.Message));
            }
        }

        private bool IsV1Format(string content)
        {
            return content.Contains("ActivationKeyCode:") ||
                   content.Contains("ActivationType:") ||
                   content.Contains("MouseButton:");
        }

        private Dictionary<string, string> ParseV1Format(string content)
        {
            var result = new Dictionary<string, string>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    result[key] = value;
                }
            }

            return result;
        }

        private Settings ConvertToV2(Dictionary<string, string> v1)
        {
            var settings = CreateDefaultSettings();

            // Convert activation type
            string keyCodeStr;
            int keyCode;
            if (v1.TryGetValue("ActivationKeyCode", out keyCodeStr) && int.TryParse(keyCodeStr, out keyCode))
                settings.Activation.KeyCode = keyCode;

            string typeStr;
            int type;
            if (v1.TryGetValue("ActivationType", out typeStr) && int.TryParse(typeStr, out type))
                settings.Activation.Type = type == 0 ? "keyboard" : "mouse";

            string mouseStr;
            int mouseButton;
            if (v1.TryGetValue("MouseButton", out mouseStr) && int.TryParse(mouseStr, out mouseButton))
                settings.Activation.MouseButton = mouseButton;

            return settings;
        }

        private Settings CreateDefaultSettings()
        {
            return new Settings();
        }

        private bool NormalizeProfiles(Settings settings)
        {
            if (settings == null)
                return false;

            if (settings.Profiles == null)
            {
                settings.Profiles = new ProfileSettings();
                return true;
            }

            if (settings.Profiles.List == null || settings.Profiles.List.Count == 0)
            {
                settings.Profiles.List = OpacityProfilePresets.CreateDefaultProfileDataList();
                settings.Profiles.ActiveId = OpacityProfilePresets.DefaultActiveProfileId;
                return true;
            }

            bool isLegacyDefaultList =
                settings.Profiles.List.Count == 3 &&
                settings.Profiles.List.Any(p => p.Id == "min" && p.Opacity == 38) &&
                settings.Profiles.List.Any(p => p.Id == "med" && p.Opacity == 128) &&
                settings.Profiles.List.Any(p => p.Id == "max" && p.Opacity == 204);

            if (isLegacyDefaultList)
            {
                byte activeOpacity = settings.Profiles.List
                    .Where(p => p.Id == settings.Profiles.ActiveId)
                    .Select(p => p.Opacity)
                    .DefaultIfEmpty(settings.Profiles.List[0].Opacity)
                    .First();

                settings.Profiles.List = OpacityProfilePresets.CreateDefaultProfileDataList();
                settings.Profiles.ActiveId = OpacityProfilePresets.GetClosestProfileId(activeOpacity);
                return true;
            }

            if (!settings.Profiles.List.Any(p => p.Id == settings.Profiles.ActiveId))
            {
                settings.Profiles.ActiveId = settings.Profiles.List[0].Id;
                return true;
            }

            return false;
        }
    }
}
