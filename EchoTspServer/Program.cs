using System;
using System.Threading.Tasks;

namespace EchoServer
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            EchoServer server = new EchoServer(5000);

            // Start the server in a separate task
            var serverTask = Task.Run(() => server.StartAsync());

            string host = "127.0.0.1"; // Target IP
            int port = 60000;          // Target Port
            int intervalMilliseconds = 5000; // Send every 5 seconds

            using (var sender = new UdpTimedSender(host, port))
            {
                Console.WriteLine("Press any key to stop sending...");
                sender.StartSending(intervalMilliseconds);

                Console.WriteLine("Press 'q' to quit...");
                while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
                {
                    // Just wait until 'q' is pressed
                }

                sender.StopSending();
                server.Stop();
                Console.WriteLine("Sender stopped.");
            }
        }
    }
}