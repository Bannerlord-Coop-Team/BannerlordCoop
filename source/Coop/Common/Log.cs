using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Common
{
    public static class Log
    {
        public static Action<string> debugAction;
        public static Action<string> infoAction;
        public static Action<string> errorAction;

        public static void Debug(string str)
        {
            write("DEBUG", str, infoAction);
        }
        public static void Info(string str)
        {
            write("INFO", str, infoAction);            
        }

        public static void Error(string str)
        {
            write("ERROR", str, infoAction);
        }
        private static void write(string sPrefix, string sMessage, Action<string> action)
        {
            
            string sLogEntry = $"{DateTime.Now.ToString()} {sPrefix}:\t{sMessage}.";
            if (infoAction != null)
            {
                infoAction(sLogEntry);
            }
            else
            {
                Console.WriteLine(sLogEntry);
            }
        }
    }
}
