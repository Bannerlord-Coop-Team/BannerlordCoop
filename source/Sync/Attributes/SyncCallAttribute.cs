using System;
using HarmonyLib;

namespace Sync.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SyncCallAttribute : SyncAttribute
    {
        public SyncCallAttribute(
            Type targetType,
            string method,
            MethodType type = MethodType.Normal) : base(targetType, method, type)
        {
        }
    }
}
