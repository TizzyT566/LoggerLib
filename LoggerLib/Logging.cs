using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;

namespace System
{
    public static class Logging
    {
        private static readonly ConcurrentDictionary<string, LogProxy> _loggers;
        private static readonly int _pid;
        private static bool _wild = false;

        public static bool Available { get; }

        public static bool EnableLogging { get; set; } = false;

        static Logging()
        {
            AppDomain.CurrentDomain.ProcessExit += Exit;

            if (Available = File.Exists("LoggerModule.exe"))
            {
                _pid = Process.GetCurrentProcess().Id;
                _loggers = new ConcurrentDictionary<string, LogProxy>();
            }
        }
        public static void EnableLogger(string subject)
        {
            if (!Available || !EnableLogging)
                return;

            if (subject == "*")
                _wild = true;
            else
            {
                if (_loggers.ContainsKey(subject))
                    return;
                LogProxy lp = new LogProxy(subject);
                if (_loggers.TryAdd(subject, lp))
                    lp.StartPipe();
                else
                    lp.Dispose();
            }
        }

        public static void DisableLogger(string subject)
        {
            if (!Available || !EnableLogging)
                return;

            if (subject == "*")
                _wild = false;
            else if (_loggers.TryRemove(subject, out LogProxy lp))
                lp.Dispose();
        }

        public static void ToggleLogger(string subject)
        {
            if (!Available || !EnableLogging)
                return;

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
                        lp.StartPipe();
                    else
                        lp.Dispose();
                }
            }
        }

        public static bool Contains(string subject) => _loggers.ContainsKey(subject);

        public static void Log(string subject, string message) => Log(subject, message, ConsoleColor.Gray, ConsoleColor.Black, -1);
        public static void Log(string subject, string message, long ticks) => Log(subject, message, ConsoleColor.Gray, ConsoleColor.Black, ticks);
        public static void Log(string subject, string message, ConsoleColor foreColor, long ticks) => Log(subject, message, foreColor, ConsoleColor.Black, ticks);
        public static void Log(string subject, string message, ConsoleColor foreColor, ConsoleColor backColor, long ticks)
        {
            if (!Available || !EnableLogging)
                return;

            if (!_loggers.TryGetValue(subject, out LogProxy p))
            {
                if (_wild)
                {
                    LogProxy lp = new LogProxy(subject);
                    p = _loggers.GetOrAdd(subject, key => lp);
                    if (p != lp)
                        lp.Dispose();
                    p.StartPipe();
                }
            }

            if (p != null)
            {
                if (ticks == -1)
                {
                    p.Post(message, (int)foreColor, (int)backColor);
                    p._prevTime = Stopwatch.GetTimestamp();
                }
                else
                {
                    long crntTime = Stopwatch.GetTimestamp();
                    if (crntTime >= p._prevTime + ticks)
                    {
                        p.Post(message, (int)foreColor, (int)backColor);
                        p._prevTime = crntTime;
                    }
                }
            }
        }

        private static void Exit(object sender, EventArgs e)
        {
            string[] keys = _loggers.Keys.ToArray();

            foreach (string key in keys)
                if (_loggers.TryRemove(key, out LogProxy p))
                    p.Dispose();
        }

        internal class LogProxy : IDisposable
        {
            public readonly string _pipeName;

            public NamedPipeServerStream _pipeServer;
            public StreamWriter _streamWriter;
            public Process _process;

            public long _prevTime = 0;

            public LogProxy(string subject) => _pipeName = $"PID_{_pid}-{subject}";

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
                        UseShellExecute = true,
                        Arguments = _pipeName,
                    },
                    EnableRaisingEvents = true
                };
                _process.Exited += StartPipe;
                _process.Start();

                _pipeServer = new NamedPipeServerStream(_pipeName, PipeDirection.Out);
                _pipeServer.WaitForConnection();

                _streamWriter = new StreamWriter(_pipeServer)
                {
                    AutoFlush = true
                };
            }

            public void StartPipe(object sender = null, EventArgs e = null)
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
                    {
                        ClosePrevPipe();
                    }
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