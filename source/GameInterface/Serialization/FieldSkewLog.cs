using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Serialization
{
    /// <summary>
    /// Handles packed fields the sender's runtime has but this runtime's type doesn't, warning once per field.
    /// </summary>
    internal static class FieldSkewLog
    {
        private static readonly ConcurrentDictionary<(Type, string), byte> Reported = new ConcurrentDictionary<(Type, string), byte>();

        // The net6 dedicated server lacks private fields net472 clients pack (List<T>._syncRoot), so a
        // missing field is skipped with a warning instead of a SetValue on a null FieldInfo.
        public static bool TryGetField(ILogger logger, Type type, IEnumerable<FieldInfo> fields, string fieldName, out FieldInfo field)
        {
            field = fields.FirstOrDefault(f => f.Name.Equals(fieldName));
            if (field != null) return true;

            WarnOnce(logger, type, fieldName);
            return false;
        }

        // Logs the skew warning the first time this field is missing on this type.
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
