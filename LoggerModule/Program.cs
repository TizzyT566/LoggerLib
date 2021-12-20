using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoggerModule
{
    public class Program
    {
        [DllImport("kernel32")]
        static extern bool AllocConsole();

        private static NamedPipeClientStream _pipeClient;
        private static StreamReader _streamReader;
        private static Process _parent;

        private static string _msg = null, _newMsg = null;

        static async Task Main(string[] args)
        {
            if (args.Length != 2)
                return;

            try
            {
                AllocConsole();

                _parent = Process.GetProcessById(int.Parse(args[0]));
                _parent.EnableRaisingEvents = true;
                _parent.Exited += (object sender, EventArgs e) => Environment.Exit(0);

                Console.Title = $"{_parent.ProcessName}: {args[1]}";

                _pipeClient = new NamedPipeClientStream(".", $"{args[0]}-{args[1]}", PipeDirection.In);
                _streamReader = new StreamReader(_pipeClient);

                await _pipeClient.ConnectAsync();
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }

            _ = Task.Run(async () =>
            {
                while (true)
                    _ = Interlocked.Exchange(ref _msg, await _streamReader.ReadLineAsync());
            });

            while (true)
            {
                try
                {
                    SpinWait.SpinUntil(() => (_newMsg = Interlocked.Exchange(ref _msg, null)) != null);
                    string[] parts = _newMsg.Split(',');
                    Console.ForegroundColor = (ConsoleColor)int.Parse(parts[0]);
                    Console.BackgroundColor = (ConsoleColor)int.Parse(parts[1]);
                    Console.Write(Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])));
                }
                catch (Exception) { }
            }
        }
    }
}
