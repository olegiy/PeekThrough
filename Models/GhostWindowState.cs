using System;

namespace GhostThrough.Models
{
    /// <summary>
    /// Stores the state of a window in Ghost Mode for later restoration
    /// </summary>
    internal class GhostWindowState
    {
        public IntPtr Hwnd { get; set; }
        public int OriginalExStyle { get; set; }
        public bool WasAlreadyLayered { get; set; }
    }
}
