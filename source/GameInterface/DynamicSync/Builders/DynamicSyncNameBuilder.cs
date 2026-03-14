using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders;
internal class DynamicSyncNameBuilder
{
    public static string BuildHandlerName(FieldInfo fieldInfo)
    {
        return $"{fieldInfo.DeclaringType.Name}_Handler";
    }

    public static string BuildInternalMessageName(FieldInfo fieldInfo)
    {
        return $"{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage";
    }

    public static string BuildNetworkMessageName(FieldInfo fieldInfo)
    {
        return $"{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage";
    }
}
