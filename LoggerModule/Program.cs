using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoggerModule
{
    class Program
    {
        private static NamedPipeClientStream _pipeClient;
        private static StreamReader _streamReader;

        private static string _msg = null;

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
                return;

            _pipeClient = new NamedPipeClientStream(".", Console.Title = args[0], PipeDirection.In);
            _streamReader = new StreamReader(_pipeClient);

            await _pipeClient.ConnectAsync();

            _ = Task.Run(async () =>
            {
                string temp;
                while (true)
                {
                    while ((temp = _streamReader.ReadLine()) != null)
                    {
                        Interlocked.Exchange(ref _msg, temp);
                        await Task.Yield();
                    }
                }
            });

            while (true)
            {
                string newMsg = null;
                SpinWait.SpinUntil(() => (newMsg = Interlocked.Exchange(ref _msg, null)) != null);
                try
                {
                    string[] parts = newMsg.Split(',');
                    Console.ForegroundColor = (ConsoleColor)int.Parse(parts[0]);
                    Console.BackgroundColor = (ConsoleColor)int.Parse(parts[1]);
                    Console.WriteLine(Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])));
                }
                catch (Exception) { }
            }
        }
    }
}
