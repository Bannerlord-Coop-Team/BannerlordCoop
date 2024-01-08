using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions.Services.Exceptions
{
    internal class ExceptionLogger : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ExceptionLogger>();

        public ExceptionLogger()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("{exception}", e.ExceptionObject);
        }
    }
}
