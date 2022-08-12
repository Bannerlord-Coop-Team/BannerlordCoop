using System;

namespace Coop.Debugging.Logger
{
    public interface ILogger : IDisposable
    {
        String GetLogFilePath();
        
        void Debug(String message);
        
        void Fatal(String message);
        
        void Error(String message);
    }
}