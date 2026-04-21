using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace PeekThrough
{
    internal static class DebugLogger
    {
        private static readonly string LogPath;
        private static readonly object FileLock = new object();
        private static readonly ConcurrentQueue<string> Queue = new ConcurrentQueue<string>();
        private static readonly ManualResetEventSlim Signal = new ManualResetEventSlim(false);
        private static readonly Thread WriterThread;
        private static int _isWriting;

        static DebugLogger()
        {
            LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "peekthrough_debug.log");
            WriterThread = new Thread(WriteLoop);
            WriterThread.IsBackground = true;
            WriterThread.Start();
        }

        public static void Log(string message)
        {
            if (!ShouldLogDebugMessages())
                return;

            EnqueueLogLine(message);
        }

        public static void LogInfo(string message)
        {
            EnqueueLogLine(message);
        }

        public static void LogState(string context, bool isLWinDown, bool ghostModeActive, bool shouldSuppress, bool timerFired)
        {
            Log(string.Format("[{0}] State: isLWinDown={1}, ghostModeActive={2}, shouldSuppress={3}, timerFired={4}", 
                context, isLWinDown, ghostModeActive, shouldSuppress, timerFired));
        }

        public static void Flush()
        {
            while (!Queue.IsEmpty || Interlocked.CompareExchange(ref _isWriting, 0, 0) == 1)
            {
                Signal.Set();
                Thread.Sleep(10);
            }
        }

        public static void ClearLog()
        {
            Flush();

            lock (FileLock)
            {
                try
                {
                    if (File.Exists(LogPath))
                        File.Delete(LogPath);
                }
                catch { }
            }
        }

        private static bool ShouldLogDebugMessages()
        {
            return !string.Equals(Environment.GetEnvironmentVariable("PEEKTHROUGH_LOG_LEVEL"), "INFO", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnqueueLogLine(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            int threadId = Thread.CurrentThread.ManagedThreadId;
            string logLine = string.Format("[{0}] [T{1}] {2}{3}", timestamp, threadId, message, Environment.NewLine);

            Queue.Enqueue(logLine);
            Signal.Set();
        }

        private static void WriteLoop()
        {
            while (true)
            {
                Signal.Wait();
                Signal.Reset();
                DrainQueue();
            }
        }

        private static void DrainQueue()
        {
            string logLine;
            while (Queue.TryDequeue(out logLine))
            {
                Interlocked.Exchange(ref _isWriting, 1);
                try
                {
                    lock (FileLock)
                    {
                        File.AppendAllText(LogPath, logLine, Encoding.UTF8);
                    }
                }
                catch
                {
                    // Silently fail if logging fails
                }
                finally
                {
                    Interlocked.Exchange(ref _isWriting, 0);
                }
            }
        }
    }
}
