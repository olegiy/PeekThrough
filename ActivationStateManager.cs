using System;
using System.Windows.Forms;

namespace GhostThrough
{
    internal enum ActivationInputType
    {
        Keyboard,
        Mouse
    }

    internal enum ActivationMode
    {
        Hold,
        Click
    }

    internal enum ActivationKeyBehavior
    {
        Standard,
        WinReverse
    }

    /// <summary>
    /// Tracks activation key/mouse state, manages timers, fires activation events
    /// </summary>
    internal class ActivationStateManager : IDisposable
    {
        // Events
        public event Func<bool> OnGhostModeShouldActivate;
        public event Action OnGhostModeShouldDeactivate;
        public event Action OnActivationBlocked;

        // State (thread-safe via lock)
        private readonly object _lockObject = new object();
        private bool _isActivationKeyDown;
        private bool _isMouseButtonDown;
        private bool _ghostModeActive;
        private bool _timerFired;
        private bool _suppressActivationKey;
        private ActivationInputType _activationType;
        private ActivationMode _activationMode;

        // Timers
        private Timer _activationTimer;
        private Timer _suppressTimer;
        private int _activationDelayMs;

        // Constants
        public const int MIN_ACTIVATION_DELAY_MS = 300;
        public const int MAX_ACTIVATION_DELAY_MS = 1500;
        public const int ACTIVATION_DELAY_STEP_MS = 100;
        public const int DEFAULT_ACTIVATION_DELAY_MS = 1000;
        private const int SUPPRESS_AFTER_DEACTIVATE_MS = 100;

        // Public state properties
        public bool IsGhostModeActive
        {
            get { lock (_lockObject) return _ghostModeActive; }
        }

        public bool IsActivationKeyPressed
        {
            get { lock (_lockObject) return _isActivationKeyDown; }
        }

        public bool IsMouseButtonPressed
        {
            get { lock (_lockObject) return _isMouseButtonDown; }
        }

        public bool ShouldSuppressActivationKey
        {
            get
            {
                lock (_lockObject)
                    return _ghostModeActive || _suppressActivationKey;
            }
        }

        public ActivationInputType CurrentActivationType
        {
            get { lock (_lockObject) return _activationType; }
            set { lock (_lockObject) _activationType = value; }
        }

        public ActivationMode CurrentActivationMode
        {
            get { lock (_lockObject) return _activationMode; }
            set { lock (_lockObject) _activationMode = value; }
        }

        public bool TimerFired
        {
            get { return _timerFired; }
        }

        public int ActivationDelayMs
        {
            get
            {
                lock (_lockObject)
                    return _activationDelayMs;
            }
            set
            {
                lock (_lockObject)
                {
                    _activationDelayMs = NormalizeActivationDelayMs(value);
                    if (_activationTimer != null)
                        _activationTimer.Interval = _activationDelayMs;
                }
            }
        }

        public ActivationStateManager(ActivationInputType activationType, int activationDelayMs = DEFAULT_ACTIVATION_DELAY_MS, ActivationMode activationMode = ActivationMode.Hold)
        {
            _activationType = activationType;
            _activationMode = activationMode;
            _activationDelayMs = NormalizeActivationDelayMs(activationDelayMs);
            InitializeTimers();
        }

        private void InitializeTimers()
        {
            _activationTimer = new Timer();
            _activationTimer.Interval = _activationDelayMs;
            _activationTimer.Tick += OnActivationTimerTick;

            _suppressTimer = new Timer();
            _suppressTimer.Interval = SUPPRESS_AFTER_DEACTIVATE_MS;
            _suppressTimer.Tick += OnSuppressTimerTick;
        }

        public static int NormalizeActivationDelayMs(int delayMs)
        {
            if (delayMs <= 0)
                return DEFAULT_ACTIVATION_DELAY_MS;

            if (delayMs < MIN_ACTIVATION_DELAY_MS)
                return MIN_ACTIVATION_DELAY_MS;

            if (delayMs > MAX_ACTIVATION_DELAY_MS)
                return MAX_ACTIVATION_DELAY_MS;

            double stepIndex = (delayMs - MIN_ACTIVATION_DELAY_MS) / (double)ACTIVATION_DELAY_STEP_MS;
            int roundedStepIndex = (int)Math.Round(stepIndex, MidpointRounding.AwayFromZero);
            int normalized = MIN_ACTIVATION_DELAY_MS + roundedStepIndex * ACTIVATION_DELAY_STEP_MS;

            if (normalized < MIN_ACTIVATION_DELAY_MS)
                return MIN_ACTIVATION_DELAY_MS;

            if (normalized > MAX_ACTIVATION_DELAY_MS)
                return MAX_ACTIVATION_DELAY_MS;

            return normalized;
        }

        public void OnActivationKeyDown()
        {
            if (_activationType != ActivationInputType.Keyboard)
                return;

            DebugLogger.Log("=== ActivationStateManager.OnActivationKeyDown START ===");

            lock (_lockObject)
            {
                if (_isActivationKeyDown)
                    return;

                _isActivationKeyDown = true;
                _timerFired = false;

                DebugLogger.LogState("ActivationStateManager.OnActivationKeyDown", _isActivationKeyDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);

                // Restart timer for activation or deactivation toggle
                _activationTimer.Stop();
                _activationTimer.Start();
            }
        }

        public void OnActivationKeyUp()
        {
            DebugLogger.Log("=== ActivationStateManager.OnActivationKeyUp START ===");

            lock (_lockObject)
            {
                DebugLogger.LogState("ActivationStateManager.OnActivationKeyUp ENTER", _isActivationKeyDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);

                _isActivationKeyDown = false;
                _activationTimer.Stop();

                if (_ghostModeActive)
                {
                    if (_activationMode == ActivationMode.Click)
                    {
                        DebugLogger.Log("ActivationStateManager: Click mode key released - deactivating");
                        DeactivateGhostMode();
                    }
                    else if (!_timerFired)
                    {
                        // Hold mode: short press while active deactivates.
                        DebugLogger.Log("ActivationStateManager: Short press while active - deactivating");
                        DeactivateGhostMode();
                    }
                    else
                    {
                        DebugLogger.Log("ActivationStateManager: Long press - just activated, staying active");
                    }
                }

                DebugLogger.LogState("ActivationStateManager.OnActivationKeyUp EXIT", _isActivationKeyDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);
            }
        }

        public void OnMouseButtonDown()
        {
            if (_activationType != ActivationInputType.Mouse)
                return;

            DebugLogger.Log("=== ActivationStateManager.OnMouseButtonDown START ===");

            lock (_lockObject)
            {
                if (_isMouseButtonDown)
                    return;

                _isMouseButtonDown = true;
                _timerFired = false;

                DebugLogger.LogState("ActivationStateManager.OnMouseButtonDown", _isMouseButtonDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);

                _activationTimer.Stop();
                _activationTimer.Start();
            }
        }

        public void OnMouseButtonUp()
        {
            if (_activationType != ActivationInputType.Mouse)
                return;

            DebugLogger.Log("=== ActivationStateManager.OnMouseButtonUp START ===");

            lock (_lockObject)
            {
                DebugLogger.LogState("ActivationStateManager.OnMouseButtonUp ENTER", _isMouseButtonDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);

                _isMouseButtonDown = false;
                _activationTimer.Stop();

                if (_ghostModeActive)
                {
                    DebugLogger.Log("ActivationStateManager: Mouse button released while active - deactivating");
                    DeactivateGhostMode();
                }

                DebugLogger.LogState("ActivationStateManager.OnMouseButtonUp EXIT", _isMouseButtonDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);
            }
        }

        public void BlockActivation()
        {
            DebugLogger.Log("=== ActivationStateManager.BlockActivation ===");

            lock (_lockObject)
            {
                _isActivationKeyDown = false;
                _isMouseButtonDown = false;
                _timerFired = false;
                _activationTimer.Stop();
                if (OnActivationBlocked != null)
                    OnActivationBlocked();
            }
        }

        public void ForceDeactivate()
        {
            DebugLogger.Log("=== ActivationStateManager.ForceDeactivate ===");
            DeactivateGhostMode();
        }

        public void CancelKeyboardActivationHoldForHandoff()
        {
            Action deactivateHandler = null;

            lock (_lockObject)
            {
                if (_activationType != ActivationInputType.Keyboard)
                    return;

                _activationTimer.Stop();
                _suppressTimer.Stop();

                bool shouldDeactivate = _ghostModeActive;

                _isActivationKeyDown = false;
                _timerFired = false;
                _suppressActivationKey = false;

                if (shouldDeactivate)
                {
                    _ghostModeActive = false;
                    deactivateHandler = OnGhostModeShouldDeactivate;
                }
            }

            if (deactivateHandler != null)
                deactivateHandler();
        }

        private void OnActivationTimerTick(object sender, EventArgs e)
        {
            DebugLogger.Log("=== ActivationStateManager.OnActivationTimerTick ===");

            lock (_lockObject)
            {
                _activationTimer.Stop();

                bool shouldActivate = false;
                if (_activationType == ActivationInputType.Keyboard)
                    shouldActivate = _isActivationKeyDown;
                else if (_activationType == ActivationInputType.Mouse)
                    shouldActivate = _isMouseButtonDown;

                if (shouldActivate && !_ghostModeActive)
                {
                    bool activated = false;
                    var activationHandler = OnGhostModeShouldActivate;
                    if (activationHandler != null)
                        activated = activationHandler();

                    if (activated)
                    {
                        _timerFired = true;
                        _ghostModeActive = true;
                        DebugLogger.LogState(string.Format("ActivationStateManager: Timer fired, activating ({0})", _activationType), _isActivationKeyDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);
                    }
                    else
                    {
                        if (_activationType == ActivationInputType.Keyboard)
                        {
                            _suppressActivationKey = true;
                            _suppressTimer.Stop();
                            _suppressTimer.Start();
                        }

                        DebugLogger.Log("ActivationStateManager: Activation rejected by controller");
                    }
                }
            }
        }

        private void OnSuppressTimerTick(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                _suppressTimer.Stop();
                _suppressActivationKey = false;
            }
        }

        private void DeactivateGhostMode()
        {
            lock (_lockObject)
            {
                if (!_ghostModeActive)
                    return;

                _isActivationKeyDown = false;
                _isMouseButtonDown = false;
                _timerFired = false;
                _activationTimer.Stop();
                _ghostModeActive = false;
                _suppressActivationKey = true;
                _suppressTimer.Start();
            }

            if (OnGhostModeShouldDeactivate != null)
                OnGhostModeShouldDeactivate();
        }

        public void Dispose()
        {
            if (_activationTimer != null)
            {
                _activationTimer.Stop();
                _activationTimer.Dispose();
            }
            if (_suppressTimer != null)
            {
                _suppressTimer.Stop();
                _suppressTimer.Dispose();
            }
        }
    }
}
