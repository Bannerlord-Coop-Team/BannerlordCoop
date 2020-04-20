using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Common
{
    public static class Log
    {
        public static Action<ELevel, string> s_OnLogEntry;
        public static ELevel s_ActiveLevels = ELevel.Trace | ELevel.Debug | ELevel.Info | ELevel.Warning | ELevel.Error;

        [Flags]
        public enum ELevel
        {
            None = 0,
            Trace = 1,
            Debug = 2,
            Info = 4,
            Warning = 8,
            Error = 16
        }
        public static void Trace(string str)
        {
            write(ELevel.Trace, str);
        }
        public static void Debug(string str)
        {
            write(ELevel.Debug, str);
        }
        public static void Info(string str)
        {
            write(ELevel.Info, str);            
        }
        public static void Warn(string str)
        {
            write(ELevel.Warning, str);
        }
        public static void Error(string str)
        {
            write(ELevel.Error, str);
        }
        private static void write(ELevel eLevel, string sMessage)
        {
            if(s_ActiveLevels.HasFlag(eLevel))
            {
                string sTimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string sLogEntry = $"{sTimeStamp} [{eLevel.ToString(), -10}] {sMessage}";
                if (s_OnLogEntry != null)
                {
                    s_OnLogEntry(eLevel, sLogEntry);
                }
                else
                {
                    Console.WriteLine(sLogEntry);
                }
            }
        }
    }
}
