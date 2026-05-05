using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace GhostThrough
{
    internal static class DebugLogger
    {
        public const string LEVEL_DEBUG = "debug";
        public const string LEVEL_INFO = "info";
        private const long MAX_LOG_SIZE_BYTES = 1024 * 1024;
        private const int LOG_OVERLAP_BYTES = 50 * 1024;

        private static readonly string LogPath;
        private static readonly object FileLock = new object();
        private static readonly ConcurrentQueue<string> Queue = new ConcurrentQueue<string>();
        private static readonly ManualResetEventSlim Signal = new ManualResetEventSlim(false);
        private static readonly Thread WriterThread;
        private static readonly object LevelLock = new object();
        private static int _isWriting;
        private static string _logLevel;

        static DebugLogger()
        {
            LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ghostthrough_debug.log");
            WriterThread = new Thread(WriteLoop);
            WriterThread.IsBackground = true;
            WriterThread.Start();
        }

        public static string CurrentLevel
        {
            get
            {
                lock (LevelLock)
                    return NormalizeLogLevel(_logLevel);
            }
        }

        public static void SetLevel(string level)
        {
            string normalizedLevel = NormalizeLogLevel(level);
            lock (LevelLock)
                _logLevel = normalizedLevel;

            LogInfo(string.Format("DebugLogger: Log level set to {0}", normalizedLevel));
        }

        public static string NormalizeLogLevel(string level)
        {
            if (string.Equals(level, LEVEL_INFO, StringComparison.OrdinalIgnoreCase))
                return LEVEL_INFO;

            return LEVEL_DEBUG;
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
            string logLevel;
            lock (LevelLock)
                logLevel = _logLevel;

            if (string.IsNullOrEmpty(logLevel))
            {
                logLevel = Environment.GetEnvironmentVariable("GHOSTTHROUGH_LOG_LEVEL");
                if (string.IsNullOrEmpty(logLevel))
                    logLevel = Environment.GetEnvironmentVariable("PEEKTHROUGH_LOG_LEVEL");
            }

            return !string.Equals(logLevel, LEVEL_INFO, StringComparison.OrdinalIgnoreCase);
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
                        TrimLogIfNeeded();
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

        private static void TrimLogIfNeeded()
        {
            try
            {
                if (!File.Exists(LogPath))
                    return;

                var fileInfo = new FileInfo(LogPath);
                if (fileInfo.Length <= MAX_LOG_SIZE_BYTES)
                    return;

                byte[] content = File.ReadAllBytes(LogPath);
                int overlapLength = Math.Min(LOG_OVERLAP_BYTES, content.Length);
                string overlap = Encoding.UTF8.GetString(content, content.Length - overlapLength, overlapLength);
                int firstLineBreak = overlap.IndexOf('\n');
                if (firstLineBreak >= 0 && firstLineBreak + 1 < overlap.Length)
                    overlap = overlap.Substring(firstLineBreak + 1);

                string marker = string.Format(
                    "[{0}] Log trimmed after exceeding {1} bytes; kept last ~{2} bytes.{3}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    MAX_LOG_SIZE_BYTES,
                    LOG_OVERLAP_BYTES,
                    Environment.NewLine);

                File.WriteAllText(LogPath, marker + overlap, Encoding.UTF8);
            }
            catch
            {
                // Logging must never break hook processing.
            }
        }
    }
}
