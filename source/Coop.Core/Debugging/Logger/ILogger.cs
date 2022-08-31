using System;

namespace Coop.Core.Debugging.Logger
{
    public interface ILogger : IDisposable
    {
        string GetLogFilePath();

        void Debug(string message);

        void Fatal(string message);

        void Error(string message);
    }
}