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
                ShouldConvertActivationModeStrings();
                ShouldConvertActivationKeyBehaviorStrings();
                ShouldExposeKnownActivationKeys();
                ShouldExposeControllerThroughActivationHost();
                ShouldOnlyDeactivateGhostModeOncePerRequest();
                ShouldClearActivationStateEvenWithoutTrackedGhostWindow();
                ShouldDeactivateKeyboardClickModeOnKeyUpAfterActivation();
                ShouldNormalizeInvalidActivationSettingsOnLoad();
                ShouldNormalizeInvalidActivationKeyBehaviorOnLoad();
                ShouldPreserveActivationKeyBehaviorDuringRoundTrip();
                ShouldRoundTripSettingsThroughAtomicSave();
                ShouldSanitizeInvalidProfilesOnLoad();
                ShouldNormalizeProfileActiveIdOnLoad();
                ShouldNormalizeInvalidActivationSettingsDuringV1Migration();
                ShouldNotNotifyWhenSettingSameActiveProfile();
 
                ShouldTreatKeyAsPressedImmediatelyAfterActivationKeyDown();
                ShouldTriggerKeyboardHandoffOnFirstOtherKeyDuringHold();
                ShouldSkipActivationKeyUpAfterKeyboardHandoff();
                ShouldReplayCompleteWinShortcutOnKeyboardHandoffAfterSuppression();
                ShouldReplayWinShortcutWhenSuppressionStartsAfterActivationTimer();
                ShouldSuppressPhysicalKeyUpsAfterCompleteKeyboardHandoffReplay();
                ShouldDeactivateGhostModeImmediatelyOnKeyboardHandoff();
                ShouldCancelPendingKeyboardHoldWithoutDeactivationWhenGhostModeInactive();
                ShouldRejectModifierActivationKeysToAvoidShortcutBlocking();
                ShouldUseOneMinuteKeyboardHookWatchdogInterval();
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

        private static void ShouldTriggerKeyboardHandoffOnFirstOtherKeyDuringHold()
        {
            var host = new TestActivationHost();
            var syncContext = new QueuedSynchronizationContext();
            var hook = CreateKeyboardHookForTest(host, syncContext);

            try
            {
                InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, NativeMethods.VK_LWIN);
                InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, 0x41);
                InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, 0x42);

                bool keyboardHandoffTriggered = (bool)GetPrivateField(hook, "_keyboardHandoffTriggered");
                if (!keyboardHandoffTriggered)
                {
                    throw new InvalidOperationException("FAIL: KeyboardHook did not mark handoff as triggered after another key was pressed during the hold.");
                }

                syncContext.Flush();

                if (host.KeyboardHandoffCount != 1)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: Keyboard handoff callback fired {0} times instead of once for a single hold.", host.KeyboardHandoffCount));
                }
            }
            finally
            {
                hook.Dispose();
            }
        }

        private static void ShouldSkipActivationKeyUpAfterKeyboardHandoff()
        {
            var host = new TestActivationHost();
            var syncContext = new QueuedSynchronizationContext();
            var hook = CreateKeyboardHookForTest(host, syncContext);

            try
            {
                InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, NativeMethods.VK_LWIN);
                InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, 0x41);
                syncContext.Flush();

                InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYUP, IntPtr.Zero, NativeMethods.VK_LWIN);
                syncContext.Flush();

                if (host.ActivationKeyUpCount != 0)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: Activation key up handler fired {0} times after keyboard handoff.", host.ActivationKeyUpCount));
                }
            }
            finally
            {
                hook.Dispose();
            }
        }

        private static void ShouldReplayCompleteWinShortcutOnKeyboardHandoffAfterSuppression()
        {
            var host = new TestActivationHost { ShouldSuppressActivationKey = true };
            var syncContext = new QueuedSynchronizationContext();
            var hook = CreateKeyboardHookForTest(host, syncContext);
            int sendInputCalls = 0;
            int inputCount = 0;
            ushort[] replayedVirtualKeys = new ushort[4];
            uint[] replayedFlags = new uint[4];

            try
            {
                SetPrivateField(
                    hook,
                    "_sendInput",
                    new Func<uint, NativeMethods.INPUT[], int, uint>((count, inputs, size) =>
                    {
                        sendInputCalls++;
                        inputCount = (int)count;
                        if (inputs != null)
                        {
                            for (int i = 0; i < System.Math.Min((int)count, 4); i++)
                            {
                                replayedVirtualKeys[i] = inputs[i].U.ki.wVk;
                                replayedFlags[i] = inputs[i].U.ki.dwFlags;
                            }
                        }
                        return count;
                    }));

                InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, NativeMethods.VK_LWIN);
                InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, 0x48);
                syncContext.Flush();

                if (sendInputCalls != 1)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: Keyboard handoff called SendInput {0} times instead of once.", sendInputCalls));
                }

                if (inputCount != 4)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: Keyboard handoff sent {0} inputs instead of a complete Win+H replay (4).", inputCount));
                }

                if (replayedVirtualKeys[0] != NativeMethods.VK_LWIN)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: First injected key was vkCode={0} instead of Left Win.", replayedVirtualKeys[0]));
                }

                if (replayedFlags[0] != NativeMethods.KEYEVENTF_EXTENDEDKEY)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: First injected Left Win input did not use extended-key flags. flags={0}.", replayedFlags[0]));
                }

                if (replayedVirtualKeys[1] != 0x48)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: Second injected key was vkCode={0} instead of H (0x48).", replayedVirtualKeys[1]));
                }

                if (replayedVirtualKeys[2] != 0x48 || replayedFlags[2] != NativeMethods.KEYEVENTF_KEYUP)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: Third injected input was not H key-up. vkCode={0}, flags={1}.", replayedVirtualKeys[2], replayedFlags[2]));
                }

                uint expectedWinKeyUpFlags = NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP;
                if (replayedVirtualKeys[3] != NativeMethods.VK_LWIN || replayedFlags[3] != expectedWinKeyUpFlags)
                {
                    throw new InvalidOperationException(
                        string.Format("FAIL: Fourth injected input was not Left Win key-up. vkCode={0}, flags={1}.", replayedVirtualKeys[3], replayedFlags[3]));
                }
            }
            finally
            {
                hook.Dispose();
            }
        }

        private static void ShouldSuppressPhysicalKeyUpsAfterCompleteKeyboardHandoffReplay()
        {
            var host = new TestActivationHost { ShouldSuppressActivationKey = true };
            var syncContext = new QueuedSynchronizationContext();
            var hook = CreateKeyboardHookForTest(host, syncContext);

            try
            {
                SetPrivateField(
                    hook,
                    "_sendInput",
                    new Func<uint, NativeMethods.INPUT[], int, uint>((count, inputs, size) => count));

                InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, NativeMethods.VK_LWIN);
                InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, 0x48);

                var otherKeyUpResult = (IntPtr)InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYUP, IntPtr.Zero, 0x48);
                var activationKeyUpResult = (IntPtr)InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYUP, IntPtr.Zero, NativeMethods.VK_LWIN);

                if (otherKeyUpResult != (IntPtr)1)
                    throw new InvalidOperationException("FAIL: Physical H key-up was not suppressed after replaying complete Win+H.");

                if (activationKeyUpResult != (IntPtr)1)
                    throw new InvalidOperationException("FAIL: Physical Win key-up was not suppressed after replaying complete Win+H.");
            }
            finally
            {
                hook.Dispose();
            }
        }

        private static void ShouldReplayWinShortcutWhenSuppressionStartsAfterActivationTimer()
        {
            var host = new TestActivationHost { ShouldSuppressActivationKey = false };
            var syncContext = new QueuedSynchronizationContext();
            var hook = CreateKeyboardHookForTest(host, syncContext);
            int sendInputCalls = 0;

            try
            {
                SetPrivateField(
                    hook,
                    "_sendInput",
                    new Func<uint, NativeMethods.INPUT[], int, uint>((count, inputs, size) =>
                    {
                        sendInputCalls++;
                        return count;
                    }));

                InvokePrivateMethod(hook, "ProcessActivationKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, NativeMethods.VK_LWIN);
                host.ShouldSuppressActivationKey = true;

                var handoffResult = (IntPtr)InvokePrivateMethod(hook, "ProcessOtherKey", 0, (IntPtr)NativeMethods.WM_KEYDOWN, IntPtr.Zero, 0x48);
                syncContext.Flush();

                if (handoffResult != (IntPtr)1)
                    throw new InvalidOperationException("FAIL: H key-down was not suppressed when replaying Win+H after activation timer suppression started.");

                if (sendInputCalls != 1)
                    throw new InvalidOperationException(
                        string.Format("FAIL: Keyboard handoff replay ran {0} times when suppression started after activation timer.", sendInputCalls));
            }
            finally
            {
                hook.Dispose();
            }
        }

        private static void ShouldDeactivateGhostModeImmediatelyOnKeyboardHandoff()
        {
            var controller = new GhostController(ActivationInputType.Keyboard, new ProfileManager());

            try
            {
                object activationState = GetPrivateField(controller, "_activationState");
                SetPrivateField(activationState, "_isActivationKeyDown", true);
                SetPrivateField(activationState, "_ghostModeActive", true);
                SetPrivateField(activationState, "_timerFired", true);
                SetPrivateField(activationState, "_suppressActivationKey", true);
                SetPrivateField(controller, "_currentTargetHwnd", new IntPtr(123));

                controller.OnKeyboardHandoffDuringActivationHold();

                bool ghostModeActive = (bool)GetPrivateField(activationState, "_ghostModeActive");
                bool isActivationKeyDown = (bool)GetPrivateField(activationState, "_isActivationKeyDown");
                bool suppressActivationKey = (bool)GetPrivateField(activationState, "_suppressActivationKey");
                IntPtr currentTarget = (IntPtr)GetPrivateField(controller, "_currentTargetHwnd");

                if (ghostModeActive)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "FAIL: Keyboard handoff did not deactivate Ghost Mode immediately. ghostModeActive={0}, isActivationKeyDown={1}, suppressActivationKey={2}, currentTarget={3}",
                            ghostModeActive,
                            isActivationKeyDown,
                            suppressActivationKey,
                            currentTarget));
                }

                if (isActivationKeyDown)
                    throw new InvalidOperationException("FAIL: Keyboard handoff left activation key state pressed.");

                if (suppressActivationKey)
                    throw new InvalidOperationException("FAIL: Keyboard handoff left activation-key suppression enabled.");

                if (currentTarget != IntPtr.Zero)
                    throw new InvalidOperationException("FAIL: Keyboard handoff did not clear the tracked ghost window.");
            }
            finally
            {
                controller.Dispose();
            }
        }

        private static void ShouldCancelPendingKeyboardHoldWithoutDeactivationWhenGhostModeInactive()
        {
            var controller = new GhostController(ActivationInputType.Keyboard, new ProfileManager());

            try
            {
                object activationState = GetPrivateField(controller, "_activationState");
                SetPrivateField(activationState, "_isActivationKeyDown", true);
                SetPrivateField(activationState, "_ghostModeActive", false);
                SetPrivateField(activationState, "_timerFired", false);
                SetPrivateField(activationState, "_suppressActivationKey", true);

                controller.OnKeyboardHandoffDuringActivationHold();

                bool ghostModeActive = (bool)GetPrivateField(activationState, "_ghostModeActive");
                bool isActivationKeyDown = (bool)GetPrivateField(activationState, "_isActivationKeyDown");
                bool suppressActivationKey = (bool)GetPrivateField(activationState, "_suppressActivationKey");

                if (ghostModeActive)
                    throw new InvalidOperationException("FAIL: Keyboard handoff activated or kept Ghost Mode while it should stay inactive.");

                if (isActivationKeyDown)
                    throw new InvalidOperationException("FAIL: Keyboard handoff did not clear pending activation key state.");

                if (suppressActivationKey)
                    throw new InvalidOperationException("FAIL: Keyboard handoff left suppression enabled for an inactive hold.");
            }
            finally
            {
                controller.Dispose();
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
                var settings = new Settings();
                settings.Activation.Type = "invalid-type";
                settings.Activation.KeyCode = NativeMethods.VK_LCONTROL;
                settings.Activation.MouseButton = NativeMethods.VK_LBUTTON;
                settings.Activation.ActivationDelayMs = 42;
                settings.Activation.Mode = "invalid-mode";
                File.WriteAllText(settingsPath, JsonFileSerializer.Serialize(settings));

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

                if (loaded.Activation.ActivationDelayMs != ActivationStateManager.MIN_ACTIVATION_DELAY_MS)
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize activation delay to the minimum supported value.");
                }

                if (loaded.Activation.Mode != "hold")
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize invalid activation mode to hold.");
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

        private static void ShouldNormalizeInvalidActivationKeyBehaviorOnLoad()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughBehaviorSettingsTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                var settings = new Settings();
                settings.Activation.KeyCode = NativeMethods.VK_LWIN;
                settings.Activation.KeyBehavior = "unknown";
                File.WriteAllText(settingsPath, JsonFileSerializer.Serialize(settings));

                var manager = new SettingsManager(settingsPath);
                Settings loaded = manager.LoadSettings();

                if (loaded.Activation.KeyBehavior != "standard")
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not normalize invalid activation key behavior to standard.");
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

        private static void ShouldPreserveActivationKeyBehaviorDuringRoundTrip()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughBehaviorRoundTripTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                var original = new Settings();
                original.Activation.KeyCode = NativeMethods.VK_LWIN;
                original.Activation.KeyBehavior = "win-reverse";

                var manager = new SettingsManager(settingsPath);
                manager.SaveSettings(original);
                Settings loaded = manager.LoadSettings();

                if (loaded.Activation.KeyBehavior != "win-reverse")
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not preserve win-reverse activation key behavior.");
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

        private static void ShouldRoundTripSettingsThroughAtomicSave()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughRoundTripSettingsTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                var original = new Settings();
                original.Activation.Type = "mouse";
                original.Activation.KeyCode = NativeMethods.VK_SPACE;
                original.Activation.MouseButton = NativeMethods.VK_XBUTTON1;
                original.Activation.ActivationDelayMs = 1300;
                original.Activation.Mode = "click";
                original.Activation.KeyBehavior = "win-reverse";
                original.Profiles.List = new List<ProfileData>
                {
                    new ProfileData { Id = "custom_1", Name = "15%", Opacity = 38 },
                    new ProfileData { Id = "custom_2", Name = "75%", Opacity = 191 }
                };
                original.Profiles.ActiveId = "custom_2";
                original.Hotkeys.NextProfile.Key = "PageUp";
                original.Hotkeys.PrevProfile.Key = "PageDown";

                var manager = new SettingsManager(settingsPath);
                manager.SaveSettings(original);

                if (!File.Exists(settingsPath))
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not create settings.json during save.");
                }

                if (File.Exists(settingsPath + ".tmp"))
                {
                    throw new InvalidOperationException("FAIL: SettingsManager left a temporary .tmp file after atomic save.");
                }

                Settings loaded = manager.LoadSettings();

                if (loaded.Activation.Type != "mouse" ||
                    loaded.Activation.KeyCode != NativeMethods.VK_SPACE ||
                    loaded.Activation.MouseButton != NativeMethods.VK_XBUTTON1 ||
                    loaded.Activation.ActivationDelayMs != 1300 ||
                    loaded.Activation.Mode != "click" ||
                    loaded.Activation.KeyBehavior != "win-reverse")
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not preserve activation settings during round-trip save/load.");
                }

                if (loaded.Profiles.ActiveId != "custom_2" ||
                    loaded.Profiles.List == null ||
                    loaded.Profiles.List.Count != 2 ||
                    loaded.Profiles.List[1].Opacity != 191)
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not preserve profile settings during round-trip save/load.");
                }

                if (loaded.Hotkeys.NextProfile.Key != "PageUp" ||
                    loaded.Hotkeys.PrevProfile.Key != "PageDown")
                {
                    throw new InvalidOperationException("FAIL: SettingsManager did not preserve hotkey settings during round-trip save/load.");
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

        private static void ShouldSanitizeInvalidProfilesOnLoad()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GhostThroughProfileSettingsTest_" + Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(tempDir, "settings.json");

            Directory.CreateDirectory(tempDir);

            try
            {
                var settings = new Settings();
                settings.Profiles.List = new List<ProfileData>
                {
                    null,
                    new ProfileData { Id = null, Name = null, Opacity = 128 },
                    new ProfileData { Id = "dup", Name = "A", Opacity = 26 },
                    new ProfileData { Id = "dup", Name = "B", Opacity = 51 }
                };
                settings.Profiles.ActiveId = "missing";
                File.WriteAllText(settingsPath, JsonFileSerializer.Serialize(settings));

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
                var settings = new Settings();
                settings.Profiles.List = new List<ProfileData>
                {
                    new ProfileData { Id = "p10", Name = "10%", Opacity = 26 },
                    new ProfileData { Id = "p20", Name = "20%", Opacity = 51 }
                };
                settings.Profiles.ActiveId = "  P20  ";
                File.WriteAllText(settingsPath, JsonFileSerializer.Serialize(settings));

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

                if (loaded.Activation.ActivationDelayMs != ActivationStateManager.DEFAULT_ACTIVATION_DELAY_MS)
                {
                    throw new InvalidOperationException("FAIL: V1 migration did not assign the default activation delay.");
                }

                if (loaded.Activation.Mode != "hold")
                {
                    throw new InvalidOperationException("FAIL: V1 migration did not assign the default activation mode.");
                }

                if (!File.Exists(settingsPath + ".bak"))
                {
                    throw new InvalidOperationException("FAIL: V1 migration did not create a backup .bak file.");
                }

                string backupContent = File.ReadAllText(settingsPath + ".bak");
                if (!backupContent.Contains("ActivationKeyCode:162"))
                {
                    throw new InvalidOperationException("FAIL: V1 migration backup does not preserve the original legacy settings content.");
                }

                string migratedContent = File.ReadAllText(settingsPath);
                if (migratedContent.Contains("ActivationKeyCode:"))
                {
                    throw new InvalidOperationException("FAIL: V1 migration left legacy settings format in settings.json instead of writing JSON.");
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

        private static void ShouldConvertActivationModeStrings()
        {
            if ("hold".ToActivationMode() != ActivationMode.Hold)
            {
                throw new InvalidOperationException("FAIL: hold string did not map to ActivationMode.Hold.");
            }

            if ("click".ToActivationMode() != ActivationMode.Click)
            {
                throw new InvalidOperationException("FAIL: click string did not map to ActivationMode.Click.");
            }

            if (ActivationMode.Click.ToSettingsValue() != "click")
            {
                throw new InvalidOperationException("FAIL: ActivationMode.Click did not serialize to click.");
            }
        }

        private static void ShouldConvertActivationKeyBehaviorStrings()
        {
            if ("standard".ToActivationKeyBehavior() != ActivationKeyBehavior.Standard)
            {
                throw new InvalidOperationException("FAIL: standard string did not map to ActivationKeyBehavior.Standard.");
            }

            if ("win-reverse".ToActivationKeyBehavior() != ActivationKeyBehavior.WinReverse)
            {
                throw new InvalidOperationException("FAIL: win-reverse string did not map to ActivationKeyBehavior.WinReverse.");
            }

            if ("invalid".ToActivationKeyBehavior() != ActivationKeyBehavior.Standard)
            {
                throw new InvalidOperationException("FAIL: invalid behavior string did not normalize to ActivationKeyBehavior.Standard.");
            }

            if (ActivationKeyBehavior.WinReverse.ToSettingsValue() != "win-reverse")
            {
                throw new InvalidOperationException("FAIL: ActivationKeyBehavior.WinReverse did not serialize to win-reverse.");
            }
        }

        private static void ShouldDeactivateKeyboardClickModeOnKeyUpAfterActivation()
        {
            var manager = new ActivationStateManager(ActivationInputType.Keyboard, ActivationStateManager.DEFAULT_ACTIVATION_DELAY_MS, ActivationMode.Click);
            bool activated = false;
            bool deactivated = false;

            try
            {
                manager.OnGhostModeShouldActivate += () =>
                {
                    activated = true;
                    return true;
                };
                manager.OnGhostModeShouldDeactivate += () => deactivated = true;

                manager.OnActivationKeyDown();
                InvokePrivateMethod(manager, "OnActivationTimerTick", null, EventArgs.Empty);

                if (!activated || !manager.IsGhostModeActive)
                {
                    throw new InvalidOperationException("FAIL: Click mode did not activate after the keyboard hold timer fired.");
                }

                manager.OnActivationKeyUp();

                if (!deactivated || manager.IsGhostModeActive)
                {
                    throw new InvalidOperationException("FAIL: Click mode did not deactivate when the activation key was released.");
                }
            }
            finally
            {
                manager.Dispose();
            }
        }

        private static void ShouldExposeKnownActivationKeys()
        {
            if (!ActivationKeyCatalog.AvailableChoices.Any(choice => choice.KeyCode == NativeMethods.VK_LWIN && choice.Behavior == ActivationKeyBehavior.Standard))
            {
                throw new InvalidOperationException("FAIL: ActivationKeyCatalog does not expose Win standard.");
            }

            if (!ActivationKeyCatalog.AvailableChoices.Any(choice => choice.KeyCode == NativeMethods.VK_LWIN && choice.Behavior == ActivationKeyBehavior.WinReverse))
            {
                throw new InvalidOperationException("FAIL: ActivationKeyCatalog does not expose Win reverse.");
            }

            if (ActivationKeyCatalog.GetDisplayName(NativeMethods.VK_ESCAPE, ActivationKeyBehavior.Standard) != "Escape")
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

        private static void ShouldUseOneMinuteKeyboardHookWatchdogInterval()
        {
            FieldInfo field = typeof(KeyboardHook).GetField("HOOK_REFRESH_INTERVAL_MS", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(typeof(KeyboardHook).FullName, "HOOK_REFRESH_INTERVAL_MS");

            int interval = (int)field.GetRawConstantValue();
            if (interval != 60000)
            {
                throw new InvalidOperationException(
                    string.Format("FAIL: Keyboard hook watchdog interval is {0} ms instead of 60000 ms.", interval));
            }
        }

        private sealed class TestActivationHost : IActivationHost
        {
            public int ActivationKeyCode { get; set; } = NativeMethods.VK_LWIN;
            public ActivationKeyBehavior ActivationKeyBehavior { get; set; } = ActivationKeyBehavior.Standard;
            public bool ShouldUseReverseWinKeyBehavior { get; set; }
            public bool ShouldSuppressActivationKey { get; set; }
            public bool IsGhostModeActive { get; set; }
            public int KeyboardHandoffCount { get; private set; }
            public int ActivationKeyDownCount { get; private set; }
            public int ActivationKeyUpCount { get; private set; }
            public int BlockBeforeActivationCount { get; private set; }
            public int DeactivateRequestCount { get; private set; }

            public void OnActivationInputDown()
            {
                ActivationKeyDownCount++;
            }

            public void OnActivationInputUp()
            {
                ActivationKeyUpCount++;
            }

            public void OnOtherInputBeforeActivation()
            {
                BlockBeforeActivationCount++;
            }

            public void OnKeyboardHandoffDuringActivationHold()
            {
                KeyboardHandoffCount++;
            }

            public void OnReverseWinKeyDown()
            {
            }

            public void OnReverseWinKeyUp()
            {
            }

            public void OnReverseWinKeyPassThrough()
            {
            }

            public bool ProcessHotkey(int vkCode, bool isDown, bool ctrl, bool shift, bool alt)
            {
                return false;
            }

            public void RequestDeactivate()
            {
                DeactivateRequestCount++;
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

        private static object InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, methodName);

            return method.Invoke(target, args);
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
