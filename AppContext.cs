using System;
using System.Linq;
using GhostThrough.Models;
using Microsoft.Win32;

namespace GhostThrough
{
    internal sealed class AppContext : IDisposable
    {
        public Settings Settings { get; private set; }
        public SettingsManager SettingsManager { get; private set; }
        public GhostController Controller { get; private set; }
        public KeyboardHook KeyboardHook { get; private set; }
        public MouseHook MouseHook { get; private set; }
        public ProfileManager ProfileManager { get; private set; }

        private bool _shutdown;

        private AppContext()
        {
        }

        public static AppContext Create(string settingsPath)
        {
            var settingsManager = new SettingsManager(settingsPath);
            var settings = settingsManager.LoadSettings();
            var profileManager = new ProfileManager(
                settings.Profiles.ActiveId,
                settings.Profiles.List.Select(p => new Profile(p.Id, p.Name, p.Opacity)).ToList());

            var activationType = settings.Activation.Type.ToActivationInputType();
            var activationMode = settings.Activation.Mode.ToActivationMode();

            var controller = new GhostController(activationType, profileManager, settings.Activation.ActivationDelayMs, activationMode);
            controller.ActivationKeyCode = GhostController.NormalizeActivationKeyCode(settings.Activation.KeyCode);
            DebugLogger.SetLevel(settings.Logging.Level);

            var appContext = new AppContext
            {
                Settings = settings,
                SettingsManager = settingsManager,
                ProfileManager = profileManager,
                Controller = controller,
                KeyboardHook = new KeyboardHook(controller),
                MouseHook = new MouseHook(controller, settings.Activation.MouseButton)
            };

            appContext.WireEvents();
            return appContext;
        }

        private void WireEvents()
        {
            if (ProfileManager != null)
                ProfileManager.OnProfileChanged += OnProfileChanged;

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        private void UnwireEvents()
        {
            if (ProfileManager != null)
                ProfileManager.OnProfileChanged -= OnProfileChanged;

            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }

        private void OnProfileChanged(Profile profile)
        {
            if (_shutdown)
                return;

            Save();
        }

        public void ReconfigureLogLevel(string logLevel)
        {
            string normalizedLevel = DebugLogger.NormalizeLogLevel(logLevel);
            Settings.Logging.Level = normalizedLevel;
            DebugLogger.SetLevel(normalizedLevel);

            Save();
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (_shutdown || KeyboardHook == null)
                return;

            if (e.Mode == PowerModes.Resume)
                KeyboardHook.RefreshHook("power resume");
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (_shutdown || KeyboardHook == null)
                return;

            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect)
                KeyboardHook.RefreshHook(string.Format("session switch: {0}", e.Reason));
        }

        public void Reconfigure(ActivationInputType activationType, int activationKeyCode, int mouseButton)
        {
            Reconfigure(activationType, activationKeyCode, mouseButton, Settings.Activation.ActivationDelayMs);
        }

        public void Reconfigure(ActivationInputType activationType, int activationKeyCode, int mouseButton, int activationDelayMs)
        {
            if (Controller.IsGhostModeActive)
                Controller.DeactivateGhostMode();

            Controller.OnOtherInputBeforeActivation();

            activationKeyCode = GhostController.NormalizeActivationKeyCode(activationKeyCode);

            Controller.CurrentActivationType = activationType;
            Controller.ActivationKeyCode = activationKeyCode;
            Controller.ActivationDelayMs = activationDelayMs;
            Controller.CurrentActivationMode = Settings.Activation.Mode.ToActivationMode();
            MouseHook.SetSelectedMouseButton(mouseButton);

            Settings.Activation.Type = activationType.ToSettingsValue();
            Settings.Activation.KeyCode = activationKeyCode;
            Settings.Activation.MouseButton = mouseButton;
            Settings.Activation.ActivationDelayMs = Controller.ActivationDelayMs;

            Save();
        }

        public void ReconfigureActivationMode(ActivationMode activationMode)
        {
            if (Controller.IsGhostModeActive)
                Controller.DeactivateGhostMode();

            Controller.OnOtherInputBeforeActivation();
            Controller.CurrentActivationMode = activationMode;
            Settings.Activation.Mode = activationMode.ToSettingsValue();

            Save();
        }

        public void ReconfigureActivationDelay(int activationDelayMs)
        {
            if (Controller.IsGhostModeActive)
                Controller.DeactivateGhostMode();

            Controller.OnOtherInputBeforeActivation();
            Controller.ActivationDelayMs = activationDelayMs;
            Settings.Activation.ActivationDelayMs = Controller.ActivationDelayMs;

            Save();
        }

        public void Save()
        {
            Settings.Profiles.ActiveId = ProfileManager.ActiveProfile.Id;
            SettingsManager.SaveSettings(Settings);
        }

        public void Shutdown()
        {
            if (_shutdown)
                return;

            _shutdown = true;
            UnwireEvents();
            Save();

            if (MouseHook != null)
                MouseHook.Dispose();
            if (KeyboardHook != null)
                KeyboardHook.Dispose();
            if (Controller != null)
                Controller.Dispose();

            DebugLogger.Flush();
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
