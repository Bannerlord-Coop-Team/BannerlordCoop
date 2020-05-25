using System;
using HarmonyLib;

namespace Sync.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SyncWatchAttribute : SyncAttribute
    {
        public SyncWatchAttribute(
            Type targetType,
            string method,
            MethodType type = MethodType.Normal) : base(targetType, method, type)
        {
        }
    }
}
