using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.LinQuick;

namespace GameInterface.DynamicSync;

public class DynamicSyncRegistryItem
{
    public HashSet<Debuggable<FieldInfo>> Fields = new();
    public HashSet<Debuggable<PropertyInfo>> Properties = new();

    public List<MethodInfo> TargetMethods = new();

    public bool Contains(FieldInfo field)
    {
        return Fields.Contains(new Debuggable<FieldInfo>(field, true))
            || Fields.Contains(new Debuggable<FieldInfo>(field, false));
    }

    public bool Contains(PropertyInfo property)
    {
        return Properties.Contains(new Debuggable<PropertyInfo>(property, true))
            || Properties.Contains(new Debuggable<PropertyInfo>(property, false));
    }

    public void AddField(FieldInfo field, bool debug)
    {
        Fields.Add(new Debuggable<FieldInfo>(field, debug));
    }

    public void AddProperty(PropertyInfo property, bool debug)
    {
        Properties.Add(new Debuggable<PropertyInfo>(property, debug));
    }

    public void AddTargetMethod(MethodInfo targetMethod)
    {
         TargetMethods.Add(targetMethod);
    }
}

public class Debuggable<T>
{
    public T Value;
    public bool Debug;

    public Debuggable(T value, bool debug)
    {
        Value = value;
        Debug = debug;
    }
}
