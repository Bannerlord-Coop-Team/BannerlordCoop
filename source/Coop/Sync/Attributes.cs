using System;
using System.Reflection;
using Coop.Reflection;
using HarmonyLib;

namespace Coop.Sync
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SyncWatchAttribute : Attribute
    {
        private readonly MethodType m_eType;
        private readonly string m_MethodName;
        private readonly Type m_Type;
        private MethodBase m_Method;

        public SyncWatchAttribute(
            Type targetType,
            string method,
            MethodType type = MethodType.Normal)
        {
            m_Type = targetType;
            m_MethodName = method;
            m_eType = type;
        }

        public MethodBase Method
        {
            get
            {
                if (m_Method != null)
                {
                    return m_Method;
                }

                m_Method = Util.GetOriginalMethod(HarmonyMethod);
                if (m_Method == null)
                {
                    throw new Exception($"Couldn't find method {m_MethodName} in type {m_Type}");
                }

                return m_Method;
            }
        }

        public HarmonyMethod HarmonyMethod =>
            new HarmonyMethod
            {
                declaringType = m_Type,
                methodName = m_MethodName,
                methodType = m_eType
            };
    }
}
