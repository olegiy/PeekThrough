using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PeekThrough
{
    internal static class DebugLogger
    {
        private static readonly string LogPath;
        private static readonly object LockObject = new object();

        static DebugLogger()
        {
            LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "peekthrough_debug.log");
        }

        public static void Log(string message)
        {
            lock (LockObject)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                int threadId = Thread.CurrentThread.ManagedThreadId;
                string logLine = string.Format("[{0}] [T{1}] {2}{3}", timestamp, threadId, message, Environment.NewLine);
                
                try
                {
                    File.AppendAllText(LogPath, logLine, Encoding.UTF8);
                }
                catch
                {
                    // Silently fail if logging fails
                }
            }
        }

        public static void LogState(string context, bool isLWinDown, bool ghostModeActive, bool shouldSuppress, bool timerFired)
        {
            Log(string.Format("[{0}] State: isLWinDown={1}, ghostModeActive={2}, shouldSuppress={3}, timerFired={4}", 
                context, isLWinDown, ghostModeActive, shouldSuppress, timerFired));
        }

        public static void ClearLog()
        {
            lock (LockObject)
            {
                try
                {
                    if (File.Exists(LogPath))
                        File.Delete(LogPath);
                }
                catch { }
            }
        }
    }
}
