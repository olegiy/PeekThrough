namespace GhostThrough
{
    internal interface IActivationHost
    {
        int ActivationKeyCode { get; }
        ActivationKeyBehavior ActivationKeyBehavior { get; }
        bool ShouldUseReverseWinKeyBehavior { get; }
        bool ShouldSuppressActivationKey { get; }
        bool IsGhostModeActive { get; }

        void OnActivationInputDown();
        void OnActivationInputUp();
        void OnOtherInputBeforeActivation();
        void OnKeyboardHandoffDuringActivationHold();
        void OnReverseWinKeyDown();
        void OnReverseWinKeyUp();
        void OnReverseWinKeyPassThrough();
        bool ProcessHotkey(int vkCode, bool isDown, bool ctrl, bool shift, bool alt);
        void RequestDeactivate();
    }
}
