using System;
using System.Windows.Forms;

namespace PeekThrough
{
    internal static class Program
    {
        private static KeyboardHook _hook;
        private static GhostLogic _logic;

        [STAThread]
        static void Main()
        {
            // Ensure single instance
            using (var mutex = new System.Threading.Mutex(false, "PeekThroughGhostModeApp"))
            {
                if (!mutex.WaitOne(0, false))
                {
                    // Already running
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                _logic = new GhostLogic();
                _hook = new KeyboardHook(_logic);

                _hook.OnLWinDown += _logic.OnKeyDown;
                _hook.OnLWinUp += _logic.OnKeyUp;
                _hook.OnOtherKeyPressedBeforeWin += _logic.BlockGhostMode;

                // Create a dummy ApplicationContext to run the loop without a main form visible at start
                Application.Run();

                _hook.Dispose();
                _logic.Dispose();
            }
        }
    }
}
