using System;
using System.Threading;

namespace GrimService.Controllers
{
    public class ConsoleController
    {
        private readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);

        public ConsoleController()
        {
            Console.WriteLine("Init: Starting console...");


            StartServer();


            Thread consoleThread = new Thread(ConsoleThread);
            consoleThread.IsBackground = true;
            consoleThread.Start();
        }

        private void StartServer()
        {
        }

        private void ConsoleThread()
        {
            Console.WriteLine("Init: Console service started.");

            while (true)
            {
                Console.Write("> ");

                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                string command = input.Trim().ToLower();

                if (command == "help")
                {
                    Console.WriteLine("Server: available commands");
                    Console.WriteLine("Server: help (showing all commands)");


                }
                else
                {
                    Console.WriteLine("Unknown command. Type 'help' to show all commands.");
                }
            }
        }

        public void WaitForExit()
        {
            _exitEvent.WaitOne();
        }
    }
}
