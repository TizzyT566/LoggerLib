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

        private static string _msg = null, _newMsg = null;

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
                return;

            _pipeClient = new NamedPipeClientStream(".", Console.Title = args[0], PipeDirection.In);
            _streamReader = new StreamReader(_pipeClient);

            await _pipeClient.ConnectAsync();

            ThreadPool.QueueUserWorkItem(_ =>
           {
               while (true)
               {
                   _ = Interlocked.Exchange(ref _msg, _streamReader.ReadLine());
                   Thread.Yield();
               }
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
