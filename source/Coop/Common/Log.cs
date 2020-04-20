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
        public static ELevel s_ActiveLevels = ELevel.Debug | ELevel.Info | ELevel.Warning | ELevel.Error;

        [Flags]
        public enum ELevel
        {
            None = 0,
            Debug = 1,
            Info = 2,
            Warning = 4,
            Error = 8
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
                string sLogEntry = $"{sTimeStamp} [{eLevel.ToString()}] \t{sMessage}";
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
