using IntroducationServer.Config;
using IntroducationServer.Server;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntroducationServer
{
    internal class Program
    {
        static int TicksPerSecond = 120;
        static MissionTestServer testServer;
        static Task pollTask;

        private static void Main(string[] args)
        {
            var config = new NetworkConfiguration();
            config.NATType = NATType.External;
            testServer = new MissionTestServer(config);

            if (config.NATType == NATType.Internal)
            {
                Console.WriteLine($"Server started on port: {config.LanPort}");
            }
            else
            {
                Console.WriteLine($"Server started on port: {config.WanPort}");
            }

            Console.WriteLine($"Type STOP to stop the server.");

            pollTask = Task.Factory.StartNew(PollServer);

            while (true) { Thread.Sleep(1000); };
        }

        static async void PollServer()
        {
            while (true)
            {
                testServer?.Update();
                await Task.Delay(1000 / TicksPerSecond);
            }
        }
    }
}
