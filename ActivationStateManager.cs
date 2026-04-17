using System;
using System.Windows.Forms;

namespace PeekThrough
{
    internal enum ActivationInputType
    {
        Keyboard,
        Mouse
    }

    /// <summary>
    /// Tracks activation key/mouse state, manages timers, fires activation events
    /// </summary>
    internal class ActivationStateManager : IDisposable
    {
        // Events
        public event Action OnGhostModeShouldActivate;
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

        // Timers
        private Timer _activationTimer;
        private Timer _suppressTimer;

        // Constants
        private const int GHOST_MODE_ACTIVATION_DELAY_MS = 1000;
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

        public bool TimerFired
        {
            get { return _timerFired; }
        }

        public ActivationStateManager(ActivationInputType activationType)
        {
            _activationType = activationType;
            InitializeTimers();
        }

        private void InitializeTimers()
        {
            _activationTimer = new Timer();
            _activationTimer.Interval = GHOST_MODE_ACTIVATION_DELAY_MS;
            _activationTimer.Tick += OnActivationTimerTick;

            _suppressTimer = new Timer();
            _suppressTimer.Interval = SUPPRESS_AFTER_DEACTIVATE_MS;
            _suppressTimer.Tick += OnSuppressTimerTick;
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
                    // Toggle mode: short press deactivates
                    if (!_timerFired)
                    {
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
                    _timerFired = true;
                    DebugLogger.LogState(string.Format("ActivationStateManager: Timer fired, activating ({0})", _activationType), _isActivationKeyDown, _ghostModeActive, ShouldSuppressActivationKey, _timerFired);
                    _ghostModeActive = true;
                    if (OnGhostModeShouldActivate != null)
                        OnGhostModeShouldActivate();
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
