using System.IO;
using Autofac;
using Coop.Core.Debugging.Logger;
using Xunit;

namespace Coop.Tests.Debugging
{
    public class LoggerTest
    {
        [Fact]
        public void Server_FilePathIsCorrect()
        {
            var container = Bootstrap.InitializeAsServer();
            using var logService = container.Resolve<ILogger>();
            
            Assert.Contains("server.log", logService.GetLogFilePath());
        }

        [Fact]
        public void Client_FilePathIsCorrect()
        {
            var container = Bootstrap.InitializeAsClient();
            using var logService = container.Resolve<ILogger>();
            
            Assert.Contains("client.log", logService.GetLogFilePath());
        }

        [Fact]
        public void Client_FileExist()
        {
            var container = Bootstrap.InitializeAsClient();
            using var logService = container.Resolve<ILogger>();

            var logFilePath = logService.GetLogFilePath();
            logService.Dispose();
            Assert.True(File.Exists(logFilePath), $"Looking for file ${logFilePath}");
        }

        [Fact]
        public void Server_FileExist()
        {
            var container = Bootstrap.InitializeAsClient();
            using var logService = container.Resolve<ILogger>();

            var logFilePath = logService.GetLogFilePath();
            logService.Dispose();
            Assert.True(File.Exists(logFilePath), $"Looking for file ${logFilePath}");
        }
    }
}