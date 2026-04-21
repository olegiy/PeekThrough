using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

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
    }
}
