using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using GhostThrough.Models;

namespace GhostThrough.Tests
{
    internal static class KeyboardHookRegressionTest
    {
        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private readonly Queue<SendOrPostCallback> _callbacks = new Queue<SendOrPostCallback>();
            private readonly Queue<object> _states = new Queue<object>();

            public override void Post(SendOrPostCallback d, object state)
            {
                _callbacks.Enqueue(d);
                _states.Enqueue(state);
            }

            public void Flush()
            {
                while (_callbacks.Count > 0)
                {
                    SendOrPostCallback callback = _callbacks.Dequeue();
                    object state = _states.Dequeue();
                    callback(state);
                }
            }
        }

        private static int Main()
        {
            DebugLogger.ClearLog();

            try
            {
                ShouldConvertActivationTypeStrings();
                ShouldExposeKnownActivationKeys();
                ShouldExposeControllerThroughActivationHost();
                ShouldOnlyDeactivateGhostModeOncePerRequest();
                ShouldClearActivationStateEvenWithoutTrackedGhostWindow();
                ShouldNormalizeInvalidActivationSettingsOnLoad();
                ShouldSanitizeInvalidProfilesOnLoad();
                ShouldNormalizeProfileActiveIdOnLoad();
                ShouldNormalizeInvalidActivationSettingsDuringV1Migration();
                ShouldNotNotifyWhenSettingSameActiveProfile();

                ShouldTreatKeyAsPressedImmediatelyAfterActivationKeyDown();
                ShouldRejectModifierActivationKeysToAvoidShortcutBlocking();
                ShouldFlushQueuedLogEntries();
                Console.WriteLine("PASS");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void ShouldTreatKeyAsPressedImmediatelyAfterActivationKeyDown()
        {
            var controller = new GhostController(ActivationInputType.Keyboard, new ProfileManager());
            var syncContext = new QueuedSynchronizationContext();
            var hook = CreateKeyboardHookForTest(controller, syncContext);

            try
            {
                hook.OnActivationKeyDown += controller.OnKeyDown;
                hook.OnActivationKeyUp += controller.OnKeyUp;

                InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, NativeMethods.VK_LWIN);
                InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, 0x41);

                bool keyPressedAfterActivation = (bool)GetPrivateField(hook, "_keyPressedAfterActivation");
                if (!keyPressedAfterActivation)
                {
                    throw new InvalidOperationException(
                        "FAIL: KeyboardHook did not mark a key as pressed after activation key down while the posted activation handler had not run yet.");
                }

                syncContext.Flush();
            }
            finally
            {
                controller.Dispose();
                hook.Dispose();
            }
        }

        private static void ShouldOnlyDeactivateGhostModeOncePerRequest()
        {
            DebugLogger.ClearLog();

            var controller = new GhostController(ActivationInputType.Keyboard, new ProfileManager());

            try
            {
                object activationState = GetPrivateField(controller, "_activationState");
                SetPrivateField(activationState, "_ghostModeActive", true);
                SetPrivateField(controller, "_currentTargetHwnd", new IntPtr(1));

                controller.DeactivateGhostMode();
                DebugLogger.Flush();

                string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ghostthrough_debug.log");
                string content = System.IO.File.ReadAllText(logPath);
                int occurrences = CountOccurrences(content, "=== GhostController.DeactivateGhostMode ===");

                if (occurrences != 1)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: GhostController deactivation ran {0} times for a single deactivation request.", occurrences));
                }
            }
            finally
            {
                controller.Dispose();
            }
        }

        private static void ShouldClearActivationStateEvenWithoutTrackedGhostWindow()
        {
            var controller = new GhostController(ActivationInputType.Keyboard, new ProfileManager());

            try
            {
                object activationState = GetPrivateField(controller, "_activationState");
                SetPrivateField(activationState, "_ghostModeActive", true);
                SetPrivateField(controller, "_currentTargetHwnd", IntPtr.Zero);

                controller.DeactivateGhostMode();

                bool ghostModeActive = (bool)GetPrivateField(activationState, "_ghostModeActive");
                if (ghostModeActive)
                {
                    throw new InvalidOperationException(
                        "FAIL: GhostController left activation state active when deactivation was requested without a tracked ghost window.");
                }
            }
            finally
            {
                controller.Dispose();
            }
        }

        private static void ShouldNormalizeInvalidActivationSettingsOnLoad()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughSettingsTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var settings = new Settings();
                settings.Activation.Type = "invalid-type";
                settings.Activation.KeyCode = NativeMethods.VK_LCONTROL;
                settings.Activation.MouseButton = NativeMethods.VK_LBUTTON;
                File.WriteAllText(settingsPath, serializer.Serialize(settings));

                var manager = new SettingsManager(settingsPath);
                Settings loaded = manager.LoadSettings();

                if (loaded.Activation.Type != "keyboard")
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize invalid activation type to keyboard.");
                }

                if (loaded.Activation.KeyCode != NativeMethods.VK_LWIN)
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize invalid activation key to Left Win.");
                }

                if (loaded.Activation.MouseButton != NativeMethods.VK_MBUTTON)
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize invalid mouse button to Middle Button.");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }
            }
        }

        private static void ShouldNotNotifyWhenSettingSameActiveProfile()
        {
            var manager = new ProfileManager();
            int notifications = 0;

            manager.OnProfileChanged += profile => notifications++;

            string activeId = manager.ActiveProfile.Id;
            manager.SetActiveProfile(activeId);
            manager.SetActiveProfileByIndex(0);

            if (notifications != 0)
            {
                throw new InvalidOperationException(
                    string.Format("FAIL: ProfileManager raised {0} redundant change notifications for the already active profile.", notifications));
            }
        }

        private static void ShouldSanitizeInvalidProfilesOnLoad()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughProfileSettingsTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var settings = new Settings();
                settings.Profiles.List = new List<ProfileData>
                {
                    null,
                    new ProfileData { Id = null, Name = null, Opacity = 128 },
                    new ProfileData { Id = "dup", Name = "A", Opacity = 26 },
                    new ProfileData { Id = "dup", Name = "B", Opacity = 51 }
                };
                settings.Profiles.ActiveId = "missing";
                File.WriteAllText(settingsPath, serializer.Serialize(settings));

                var manager = new SettingsManager(settingsPath);
                Settings loaded = manager.LoadSettings();

                if (loaded.Profiles.List == null || loaded.Profiles.List.Count != 3)
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not sanitize the invalid profile list correctly.");
                }

                if (loaded.Profiles.List.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Name)))
                {
                    throw new InvalidOperationException("FAIL: SettingsManager left invalid profile entries after sanitization.");
                }

                int uniqueIds = loaded.Profiles.List.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                if (uniqueIds != loaded.Profiles.List.Count)
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not make duplicate profile IDs unique.");
                }

                if (!loaded.Profiles.List.Any(p => p.Id == loaded.Profiles.ActiveId))
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize ActiveId to an existing profile.");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }
            }
        }

        private static void ShouldNormalizeProfileActiveIdOnLoad()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughActiveProfileSettingsTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var settings = new Settings();
                settings.Profiles.List = new List<ProfileData>
                {
                    new ProfileData { Id = "p10", Name = "10%", Opacity = 26 },
                    new ProfileData { Id = "p20", Name = "20%", Opacity = 51 }
                };
                settings.Profiles.ActiveId = "  P20  ";
                File.WriteAllText(settingsPath, serializer.Serialize(settings));

                var manager = new SettingsManager(settingsPath);
                Settings loaded = manager.LoadSettings();

                if (loaded.Profiles.ActiveId != "p20")
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize ActiveId whitespace/casing to the matching profile ID.");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }
            }
        }

        private static void ShouldNormalizeInvalidActivationSettingsDuringV1Migration()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughLegacySettingsTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                File.WriteAllText(
                    settingsPath,
                    string.Join(
                        Environment.NewLine,
                        "ActivationKeyCode:162",
                        "ActivationType:9",
                        "MouseButton:1"));

                var manager = new SettingsManager(settingsPath);
                Settings loaded = manager.LoadSettings();

                if (loaded.Activation.Type != "keyboard")
                {
                    throw new InvalidOperationException("FAIL: V1 migration did not normalize invalid activation type to keyboard.");
                }

                if (loaded.Activation.KeyCode != NativeMethods.VK_LWIN)
                {
                    throw new InvalidOperationException("FAIL: V1 migration did not normalize invalid activation key to Left Win.");
                }

                if (loaded.Activation.MouseButton != NativeMethods.VK_MBUTTON)
                {
                    throw new InvalidOperationException("FAIL: V1 migration did not normalize invalid mouse button to Middle Button.");
                }

                if (!File.Exists(settingsPath + ".bak"))
                {
                    throw new InvalidOperationException("FAIL: V1 migration did not create a backup .bak file.");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }
            }
        }


        private static void ShouldConvertActivationTypeStrings()
        {
            if ("keyboard".ToActivationInputType() != ActivationInputType.Keyboard)
            {
                throw new InvalidOperationException("FAIL: keyboard string did not map to ActivationInputType.Keyboard.");
            }

            if ("mouse".ToActivationInputType() != ActivationInputType.Mouse)
            {
                throw new InvalidOperationException("FAIL: mouse string did not map to ActivationInputType.Mouse.");
            }

            if (ActivationInputType.Keyboard.ToSettingsValue() != "keyboard")
            {
                throw new InvalidOperationException("FAIL: ActivationInputType.Keyboard did not serialize to keyboard.");
            }
        }

        private static void ShouldExposeKnownActivationKeys()
        {
            if (!ActivationKeyCatalog.AvailableKeys.Contains(NativeMethods.VK_LWIN))
            {
                throw new InvalidOperationException("FAIL: ActivationKeyCatalog does not expose Left Win.");
            }

            if (ActivationKeyCatalog.GetDisplayName(NativeMethods.VK_ESCAPE) != "Escape")
            {
                throw new InvalidOperationException("FAIL: ActivationKeyCatalog display name for Escape changed unexpectedly.");
            }
        }

        private static void ShouldRejectModifierActivationKeysToAvoidShortcutBlocking()
        {
            var controller = new GhostController(ActivationInputType.Keyboard, new ProfileManager());

            try
            {
                controller.ActivationKeyCode = NativeMethods.VK_LCONTROL;

                if (controller.ActivationKeyCode != NativeMethods.VK_LWIN)
                {
                    throw new InvalidOperationException(
                        "FAIL: GhostController accepted Ctrl as activation key. Modifier activation keys must be rejected to avoid blocking Ctrl shortcuts in other apps.");
                }
            }
            finally
            {
                controller.Dispose();
            }
        }

        private static void ShouldExposeControllerThroughActivationHost()
        {
            IActivationHost host = new GhostController(ActivationInputType.Keyboard, new ProfileManager());

            try
            {
                if (host.ActivationKeyCode != NativeMethods.VK_LWIN)
                {
                    throw new InvalidOperationException("FAIL: IActivationHost did not expose the default activation key.");
                }
            }
            finally
            {
                ((GhostController)host).Dispose();
            }
        }

        private static void ShouldFlushQueuedLogEntries()
        {
            DebugLogger.ClearLog();
            DebugLogger.Log("test-log-entry");
            DebugLogger.Flush();

            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ghostthrough_debug.log");
            string content = System.IO.File.ReadAllText(logPath);

            if (!content.Contains("test-log-entry"))
            {
                throw new InvalidOperationException("FAIL: DebugLogger.Flush did not persist queued entries.");
            }
        }

        private static KeyboardHook CreateKeyboardHookForTest(IActivationHost host, SynchronizationContext syncContext)
        {
            var hook = (KeyboardHook)FormatterServices.GetUninitializedObject(typeof(KeyboardHook));

            SetPrivateField(hook, "_syncContext", syncContext);
            SetPrivateField(hook, "_activationHost", host);
            SetPrivateField(hook, "_pressedKeys", new HashSet<int>());
            SetPrivateField(hook, "_hookID", IntPtr.Zero);
            SetPrivateField(hook, "_disposed", false);

            return hook;
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, methodName);

            method.Invoke(target, args);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(target.GetType().FullName, fieldName);

            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(target.GetType().FullName, fieldName);

            field.SetValue(target, value);
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
