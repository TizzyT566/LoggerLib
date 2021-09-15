using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;

namespace System
{
    public static class Log
    {
        private static readonly ConcurrentDictionary<string, LogProxy> _loggers;
        private static readonly int _pid;
        private static bool _wild = false;

        public static bool Available { get; }

        public static int MaxOutputs { get; set; } = 16;

        static Log()
        {
            // Close all loggers via this event
            AppDomain.CurrentDomain.ProcessExit += Exit;

            if (Available = File.Exists("LoggerModule.exe"))
            {
                _pid = Process.GetCurrentProcess().Id;
                _loggers = new ConcurrentDictionary<string, LogProxy>();
            }
            else
            {
                throw new Exception("LoggerModule not available.");
            }
        }

        public static void EnabledLogger(string subject)
        {
            if (subject == "*")
            {
                _wild = true;
            }
            else
            {
                LogProxy lp = new LogProxy(subject);
                if (_loggers.TryAdd(subject, lp))
                {
                    lp.StartPipe();
                }
                else
                {
                    lp.Dispose();
                }
            }
        }

        public static void DisableLogger(string subject)
        {
            if (subject == "*")
            {
                _wild = false;
            }
            else
            {
                if (_loggers.TryRemove(subject, out LogProxy lp))
                    lp.Dispose();
            }
        }

        public static void WriteLine(string subject, string message, ConsoleColor foreColor = ConsoleColor.Gray, ConsoleColor backColor = ConsoleColor.Black)
        {
            if (!_loggers.TryGetValue(subject, out LogProxy p) && _wild)
            {
                if (_wild)
                {
                    EnabledLogger(subject);
                    p?.WriteLine(message, (int)foreColor, (int)backColor);
                }
            }
            else
            {
                p?.WriteLine(message, (int)foreColor, (int)backColor);
            }
        }

        public static void Exit(object sender, EventArgs e)
        {
            string[] keys = _loggers.Keys.ToArray();

            foreach (string key in keys)
            {
                if (_loggers.TryRemove(key, out LogProxy p))
                    p.Dispose();
            }
        }

        internal class LogProxy : IDisposable
        {
            public readonly string _pipeName;

            public NamedPipeServerStream _pipeServer;
            public StreamWriter _streamWriter;
            public Process _process;

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

            private int _lock = 0;
            public void WriteLine(string message, int foreColor, int backColor)
            {
                try
                {
                    while (Interlocked.CompareExchange(ref _lock, 1, 0) == 1) ;
                    _streamWriter.WriteLine($"{foreColor},{backColor},{Convert.ToBase64String(Encoding.UTF8.GetBytes(message))}");
                    Interlocked.Exchange(ref _lock, 0);
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