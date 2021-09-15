using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace LoggerModule
{
    class Program
    {
        private static NamedPipeClientStream _pipeClient;
        private static StreamReader _streamReader;

        static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            _pipeClient = new NamedPipeClientStream(".", Console.Title = args[0], PipeDirection.In);
            _streamReader = new StreamReader(_pipeClient);

            _pipeClient.Connect();

            while (true)
            {
                string temp;
                while ((temp = _streamReader.ReadLine()) != null)
                {
                    string[] message = temp.Split(',');
                    Console.ForegroundColor = (ConsoleColor)int.Parse(message[0]);
                    Console.BackgroundColor = (ConsoleColor)int.Parse(message[1]);
                    Console.WriteLine(Encoding.UTF8.GetString(Convert.FromBase64String(message[2])));
                }
            }
        }
    }
}
