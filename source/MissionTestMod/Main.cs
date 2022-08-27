using MissionTestMod.Server;
using Missions.Config;
using System;
using System.Threading.Tasks;

namespace MissionTestMod
{
    internal class Program
    {
        static int TicksPerSecond = 120;
        static MissionTestServer testServer;
        static Task stopTask;
        static Task pollTask;
        static void Main(string[] args)
        {
            NetworkConfiguration config = new NetworkConfiguration();
            testServer = new MissionTestServer(config);

            Console.WriteLine($"Server started on: {config.WanAddress}, Port: {config.WanPort}");
            Console.WriteLine($"Type STOP to stop the server.");


            stopTask = Task.Factory.StartNew(WaitForStop);
            pollTask = Task.Factory.StartNew(PollServer);

            stopTask.Wait();
            pollTask.Wait();
        }

        static async void WaitForStop()
        {
            while(Console.ReadLine().ToLower() != "stop") { await Task.Delay(100); }
        }

        static async void PollServer()
        {
            while(stopTask.Status == TaskStatus.Running)
            {
                testServer.Update();
                await Task.Delay(1000 / TicksPerSecond);
            }
        }
    }
}
