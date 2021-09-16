using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;

namespace System
{
    public static class Logging
    {
        private static readonly ConcurrentDictionary<string, LogProxy> _loggers;
        private static readonly int _pid;
        private static bool _wild = false, _enabled = false;

        public static bool Available { get; }

        static Logging()
        {
            AppDomain.CurrentDomain.ProcessExit += Clear;

            if (Available = File.Exists("LoggerModule.exe"))
            {
                _pid = Process.GetCurrentProcess().Id;
                _loggers = new ConcurrentDictionary<string, LogProxy>();
            }
        }
        public static void EnableLogger(string subject)
        {
            if (Available && _enabled)
            {
                if (subject == "*")
                    _wild = true;
                else
                {
                    if (_loggers.ContainsKey(subject))
                        return;
                    LogProxy lp = new LogProxy(subject);
                    if (_loggers.TryAdd(subject, lp))
                        lp.StartLogger();
                    else
                        lp.Dispose();
                }
            }
        }

        public static void DisableLogger(string subject)
        {
            if (Available && _enabled)
            {
                if (subject == "*")
                    _wild = false;
                else if (_loggers.TryRemove(subject, out LogProxy lp))
                    lp.Dispose();
            }
        }

        public static void ToggleLogger(string subject)
        {
            if (Available && _enabled)
            {
                if (subject == "*")
                    _wild = !_wild;
                else
                {
                    if (_loggers.TryRemove(subject, out LogProxy lp))
                        lp.Dispose();
                    else
                    {
                        lp = new LogProxy(subject);
                        if (_loggers.TryAdd(subject, lp))
                            lp.StartLogger();
                        else
                            lp.Dispose();
                    }
                }
            }
        }

        public static bool Contains(string subject) => _loggers.ContainsKey(subject);

        public static void Log(string subject, string message, Action misc = null) =>
            Post(subject, message, 7, 0, 0, misc);
        public static void Log(string subject, string message, long ticks, Action misc = null) =>
            Post(subject, message, 7, 0, ticks, misc);
        public static void Log(string subject, string message, ConsoleColor foreColor, long ticks = 0, Action misc = null) =>
            Post(subject, message, (int)foreColor, (int)ConsoleColor.Black, ticks, misc);
        public static void Log(string subject, string message, ConsoleColor foreColor, ConsoleColor backColor, long ticks = 0, Action misc = null) =>
            Post(subject, message, (int)foreColor, (int)backColor, ticks, misc);

        public static void LogLine(string subject, string message, Action misc = null) =>
            Post(subject, $"{message}\n", 7, 0, 0, misc);
        public static void LogLine(string subject, string message, long ticks, Action misc = null) =>
            Post(subject, $"{message}\n", 7, 0, ticks, misc);
        public static void LogLine(string subject, string message, ConsoleColor foreColor, long ticks = 0, Action misc = null) =>
            Post(subject, $"{message}\n", (int)foreColor, 0, ticks, misc);
        public static void LogLine(string subject, string message, ConsoleColor foreColor, ConsoleColor backColor, long ticks = 0, Action misc = null) =>
            Post(subject, $"{message}\n", (int)foreColor, (int)backColor, ticks, misc);

        private static void Post(string subject, string message, int foreColor, int backColor, long ticks, Action misc)
        {
            if (Available && _enabled)
            {
                if (!_loggers.TryGetValue(subject, out LogProxy p))
                {
                    if (_wild)
                    {
                        LogProxy lp = new LogProxy(subject);
                        p = _loggers.GetOrAdd(subject, key => lp);
                        if (p == lp) p.StartLogger();
                        else lp.Dispose();
                    }
                    else return;
                }

                if (ticks < 1)
                {
                    p.Post(message, foreColor, backColor);
                    misc?.Invoke();
                }
                else
                {
                    long crntTime = Stopwatch.GetTimestamp();
                    if (crntTime >= p._prevTime + ticks)
                    {
                        p.Post(message, foreColor, backColor);
                        p._prevTime = crntTime;
                        misc?.Invoke();
                    }
                }
            }
        }

        public static void StartLogging() => _enabled = true;

        public static void StopLogging(bool clear = false)
        {
            _enabled = false;
            if (clear)
                Clear();
        }

        private static void Clear(object sender = null, EventArgs e = null)
        {
            string[] keys = _loggers.Keys.ToArray();
            foreach (string key in keys)
                if (_loggers.TryRemove(key, out LogProxy p))
                    p.Dispose();
        }

        internal class LogProxy : IDisposable
        {
            public readonly string _subject;

            public NamedPipeServerStream _pipeServer;
            public StreamWriter _streamWriter;
            public Process _process;

            public long _prevTime = 0;

            public LogProxy(string subject) => _subject = subject;

            private void ClosePrevPipe()
            {
                try
                {
                    _streamWriter?.Dispose();
                    _streamWriter = null;
                }
                catch (Exception) { }
                try
                {
                    _pipeServer?.Dispose();
                    _pipeServer = null;
                }
                catch (Exception) { }
                try
                {
                    _process?.Kill();
                }
                catch (Exception) { }
                try
                {
                    _process?.Dispose();
                    _process = null;
                }
                catch (Exception) { }
            }

            private void StartNewPipe()
            {
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "LoggerModule.exe",
                        Arguments = $"{_pid} {_subject}",
                    },
                    EnableRaisingEvents = true
                };
                _process.Exited += StartLogger;
                _process.Start();

                _pipeServer = new NamedPipeServerStream($"{_pid}-{_subject}", PipeDirection.Out);
                _pipeServer.WaitForConnection();

                _streamWriter = new StreamWriter(_pipeServer)
                {
                    AutoFlush = true
                };
            }

            public void StartLogger(object sender = null, EventArgs e = null)
            {
                ClosePrevPipe();
                StartNewPipe();
            }

            public void Post(string message, int foreColor, int backColor)
            {
                try
                {
                    _streamWriter.WriteLine($"{foreColor},{backColor},{Convert.ToBase64String(Encoding.UTF8.GetBytes(message))}");
                }
                catch (Exception) { }
            }

            private bool disposedValue;
            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                        ClosePrevPipe();
                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}