using System;
using System.Collections.Concurrent;

namespace GameInterface.Serialization
{
    /// <summary>
    /// Remembers which skewed fields were already reported. A modded hero can carry thousands of
    /// lists with the same missing field, and one warning per value kept the dedicated server busy
    /// writing its console long enough to time out the join time hero transfer.
    /// </summary>
    internal static class FieldSkewLog
    {
        private static readonly ConcurrentDictionary<(Type, string), byte> Reported = new ConcurrentDictionary<(Type, string), byte>();

        public static bool FirstTime(Type type, string fieldName) => Reported.TryAdd((type, fieldName), 0);
    }
}
