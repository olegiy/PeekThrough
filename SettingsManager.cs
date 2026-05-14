using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GhostThrough.Models;

namespace GhostThrough
{
    /// <summary>
    /// Loads, saves, and migrates settings from v1 (pseudo-JSON) to v2 (real JSON)
    /// </summary>
    internal class SettingsManager
    {
        private readonly string _settingsPath;

        public SettingsManager(string settingsPath)
        {
            _settingsPath = settingsPath;
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
                    NormalizeActivationSettings(v2);
                    NormalizeProfiles(v2);
                    NormalizeLoggingSettings(v2);

                    // Backup old settings
                    string backupPath = _settingsPath + ".bak";
                    File.Move(_settingsPath, backupPath);
                    DebugLogger.Log(string.Format("SettingsManager: Backed up v1 settings to {0}", backupPath));

                    // Save v2 format
                    SaveSettings(v2);
                    return v2;
                }

                // Parse v2 JSON format
                var settings = JsonFileSerializer.Deserialize<Settings>(content) ?? CreateDefaultSettings();
                bool settingsChanged = NormalizeActivationSettings(settings);
                settingsChanged = NormalizeProfiles(settings) || settingsChanged;
                settingsChanged = NormalizeLoggingSettings(settings) || settingsChanged;
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

                string json = JsonFileSerializer.Serialize(settings);
                WriteFileAtomically(_settingsPath, json);
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
                settings.Activation.Type = (type == 1 ? ActivationInputType.Mouse : ActivationInputType.Keyboard).ToSettingsValue();

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

        private bool NormalizeActivationSettings(Settings settings)
        {
            if (settings == null)
                return false;

            bool changed = false;

            if (settings.Activation == null)
            {
                settings.Activation = new ActivationSettings();
                return true;
            }

            string normalizedType = settings.Activation.Type.ToActivationInputType().ToSettingsValue();
            if (!string.Equals(settings.Activation.Type, normalizedType, StringComparison.OrdinalIgnoreCase))
            {
                settings.Activation.Type = normalizedType;
                changed = true;
            }

            int normalizedKeyCode = GhostController.NormalizeActivationKeyCode(settings.Activation.KeyCode);
            if (!ActivationKeyCatalog.IsSupportedKey(normalizedKeyCode))
            {
                normalizedKeyCode = NativeMethods.VK_LWIN;
            }

            if (settings.Activation.KeyCode != normalizedKeyCode)
            {
                settings.Activation.KeyCode = normalizedKeyCode;
                changed = true;
            }

            int normalizedMouseButton = NormalizeMouseButton(settings.Activation.MouseButton);
            if (settings.Activation.MouseButton != normalizedMouseButton)
            {
                settings.Activation.MouseButton = normalizedMouseButton;
                changed = true;
            }

            int normalizedActivationDelayMs = ActivationStateManager.NormalizeActivationDelayMs(settings.Activation.ActivationDelayMs);
            if (settings.Activation.ActivationDelayMs != normalizedActivationDelayMs)
            {
                settings.Activation.ActivationDelayMs = normalizedActivationDelayMs;
                changed = true;
            }

            string normalizedMode = settings.Activation.Mode.ToActivationMode().ToSettingsValue();
            if (!string.Equals(settings.Activation.Mode, normalizedMode, StringComparison.OrdinalIgnoreCase))
            {
                settings.Activation.Mode = normalizedMode;
                changed = true;
            }

            string normalizedKeyBehavior = settings.Activation.KeyBehavior.ToActivationKeyBehavior().ToSettingsValue();
            if (!string.Equals(settings.Activation.KeyBehavior, normalizedKeyBehavior, StringComparison.OrdinalIgnoreCase))
            {
                settings.Activation.KeyBehavior = normalizedKeyBehavior;
                changed = true;
            }

            return changed;
        }

        private bool NormalizeProfiles(Settings settings)
        {
            if (settings == null)
                return false;

            bool changed = false;

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

            if (SanitizeProfileList(settings.Profiles.List))
                changed = true;

            if (settings.Profiles.List.Count == 0)
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

            string activeId = settings.Profiles.ActiveId == null
                ? null
                : settings.Profiles.ActiveId.Trim();
            ProfileData activeProfile = settings.Profiles.List.FirstOrDefault(
                p => string.Equals(p.Id, activeId, StringComparison.OrdinalIgnoreCase));
            if (activeProfile == null)
            {
                settings.Profiles.ActiveId = settings.Profiles.List[0].Id;
                changed = true;
            }
            else if (!string.Equals(settings.Profiles.ActiveId, activeProfile.Id, StringComparison.Ordinal))
            {
                settings.Profiles.ActiveId = activeProfile.Id;
                changed = true;
            }

            return changed;

        }

        private bool NormalizeLoggingSettings(Settings settings)
        {
            if (settings == null)
                return false;

            if (settings.Logging == null)
            {
                settings.Logging = new LoggingSettings();
                return true;
            }

            string normalizedLevel = DebugLogger.NormalizeLogLevel(settings.Logging.Level);
            if (!string.Equals(settings.Logging.Level, normalizedLevel, StringComparison.OrdinalIgnoreCase))
            {
                settings.Logging.Level = normalizedLevel;
                return true;
            }

            return false;
        }

        private static int NormalizeMouseButton(int mouseButton)
        {
            switch (mouseButton)
            {
                case NativeMethods.VK_MBUTTON:
                case NativeMethods.VK_RBUTTON:
                case NativeMethods.VK_XBUTTON1:
                case NativeMethods.VK_XBUTTON2:
                    return mouseButton;
                default:
                    return NativeMethods.VK_MBUTTON;
            }
        }

        private static bool SanitizeProfileList(List<ProfileData> profiles)
        {
            bool changed = false;
            var sanitized = new List<ProfileData>();
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var profile in profiles)
            {
                if (profile == null)
                {
                    changed = true;
                    continue;
                }

                string id = string.IsNullOrWhiteSpace(profile.Id)
                    ? OpacityProfilePresets.GetProfileId(profile.Opacity)
                    : profile.Id.Trim();
                string name = string.IsNullOrWhiteSpace(profile.Name)
                    ? OpacityProfilePresets.GetProfileName(profile.Opacity)
                    : profile.Name.Trim();

                if (!string.Equals(profile.Id, id, StringComparison.Ordinal) ||
                    !string.Equals(profile.Name, name, StringComparison.Ordinal))
                {
                    changed = true;
                }

                string uniqueId = id;
                int suffix = 2;
                while (!usedIds.Add(uniqueId))
                {
                    uniqueId = string.Format("{0}_{1}", id, suffix);
                    suffix++;
                }

                if (!string.Equals(uniqueId, id, StringComparison.Ordinal))
                    changed = true;

                sanitized.Add(new ProfileData
                {
                    Id = uniqueId,
                    Name = name,
                    Opacity = profile.Opacity
                });
            }

            if (changed)
            {
                profiles.Clear();
                profiles.AddRange(sanitized);
            }

            return changed;
        }

        private static void WriteFileAtomically(string targetPath, string content)
        {
            string tempPath = targetPath + ".tmp";
            File.WriteAllText(tempPath, content);

            try
            {
                if (File.Exists(targetPath))
                {
                    File.Replace(tempPath, targetPath, null);
                }
                else
                {
                    File.Move(tempPath, targetPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
