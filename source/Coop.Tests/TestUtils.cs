using Coop.Common;
using Coop.Network;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using Xunit;

namespace Coop.Tests
{
    static class TestUtils
    {
        public static ServerConfiguration GetTestingConfig()
        {
            ServerConfiguration config = new ServerConfiguration();
            config.lanPort = TestUtils.GetPort();
            config.uiMaxPlayerCount = 4;
            config.uiTickRate = 0;
            return config;
        }
        public static Server StartNewServer()
        {
            Server server = new Server();
            server.Start(GetTestingConfig());
            return server;
        }
        public static void UpdateUntil(Func<bool> condition, List<IUpdateable> updateables)
        {
            TimeSpan totalWaitTime = TimeSpan.Zero;
            TimeSpan waitTimeBetweenTries = TimeSpan.FromMilliseconds(10);
            while (true)
            {
                foreach (var updateable in updateables)
                {
                    updateable.Update(waitTimeBetweenTries);
                }
                if (condition())
                {
                    break;
                }
                else
                {
                    Thread.Sleep(waitTimeBetweenTries);
                    totalWaitTime += waitTimeBetweenTries;
                    Assert.True(totalWaitTime < TimeSpan.FromMilliseconds(500), "Maximum wait time reached. Abort.");
                }
            }
        }

        private static object UsedPortsLock = new object();
        private static HashSet<int> UsedPorts = new HashSet<int>();
        public static int GetPort()
        {
            lock (UsedPortsLock)
            {
                int iPort = FindAvailablePort(3000);
                UsedPorts.Add(iPort);
                if(iPort == 0)
                {
                    throw new Exception("Could not find any available ports.");
                }
                return iPort;
            }
        }
        private static int FindAvailablePort(int startingPort)
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            var tcpConnectionPorts = properties.GetActiveTcpConnections()
                                .Where(n => n.LocalEndPoint.Port >= startingPort)
                                .Select(n => n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            var tcpListenerPorts = properties.GetActiveTcpListeners()
                                .Where(n => n.Port >= startingPort)
                                .Select(n => n.Port);

            //getting active udp listeners
            var udpListenerPorts = properties.GetActiveUdpListeners()
                                .Where(n => n.Port >= startingPort)
                                .Select(n => n.Port);

            var port = Enumerable.Range(startingPort, ushort.MaxValue)
                .Where(i => !UsedPorts.Contains(i))
                .Where(i => !tcpConnectionPorts.Contains(i))
                .Where(i => !tcpListenerPorts.Contains(i))
                .Where(i => !udpListenerPorts.Contains(i))
                .FirstOrDefault();

            return port;
        }
    }
}
