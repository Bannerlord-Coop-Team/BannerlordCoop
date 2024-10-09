using System.Collections.Generic;

namespace Common.Logging;
public class DebugMessageLogger
{
    public readonly static List<string> Messages = new List<string>();

    public static void Write(string msg)
    {
        Messages.Add(msg);
    }
}
