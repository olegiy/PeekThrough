namespace GhostThrough
{
    internal interface IActivationHost
    {
        int ActivationKeyCode { get; }
        bool ShouldSuppressActivationKey { get; }
        bool IsGhostModeActive { get; }

        void OnActivationInputDown();
        void OnActivationInputUp();
        void OnOtherInputBeforeActivation();
        void OnKeyboardHandoffDuringActivationHold();
        bool ProcessHotkey(int vkCode, bool isDown, bool ctrl, bool shift, bool alt);
        void RequestDeactivate();
    }
}
