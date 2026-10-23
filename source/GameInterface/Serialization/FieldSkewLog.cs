using Serilog;
using System;
using System.Collections.Concurrent;

namespace GameInterface.Serialization
{
    /// <summary>
    /// Reports each skewed field once. A modded hero can carry thousands of lists with the same
    /// missing field, and one warning per value kept the dedicated server busy writing its console
    /// long enough to time out the join time hero transfer.
    /// </summary>
    internal static class FieldSkewLog
    {
        private static readonly ConcurrentDictionary<(Type, string), byte> Reported = new ConcurrentDictionary<(Type, string), byte>();

        public static void WarnOnce(ILogger logger, Type type, string fieldName)
        {
            if (FirstTime(type, fieldName))
                logger.Warning("[FieldSkew] {Type} has no field '{Field}' on this runtime; skipping its packed values (logged once per field)", type.Name, fieldName);
        }

        // A generic type's fields don't depend on its type arguments, and the warning prints type.Name,
        // so every closed MBList<T> shares one entry.
        internal static bool FirstTime(Type type, string fieldName)
        {
            var key = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            return Reported.TryAdd((key, fieldName), 0);
        }
    }
}
