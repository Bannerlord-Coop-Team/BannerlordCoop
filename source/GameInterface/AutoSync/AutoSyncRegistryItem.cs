using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.AutoSync;

public class AutoSyncRegistryItem
{
    public HashSet<Debuggable<FieldInfo>> Fields = new();
    public HashSet<Debuggable<PropertyInfo>> Properties = new();

    public List<MethodInfo> TargetMethods = new();
    public Dictionary<string, List<MethodInfo>> CategorizedTargetMethods = new();

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

    public void AddField(FieldInfo field, bool debug, bool coalesce)
    {
        Fields.Add(new Debuggable<FieldInfo>(field, debug, coalesce));
    }

    public void AddProperty(PropertyInfo property, bool debug, bool coalesce)
    {
        Properties.Add(new Debuggable<PropertyInfo>(property, debug, coalesce));
    }

    public void AddTargetMethod(MethodInfo targetMethod, string patchCategory = null)
    {
        if (patchCategory == null)
        {
            TargetMethods.Add(targetMethod);
            return;
        }

        if (!CategorizedTargetMethods.TryGetValue(patchCategory, out var targetMethods))
        {
            targetMethods = new List<MethodInfo>();
            CategorizedTargetMethods.Add(patchCategory, targetMethods);
        }

        targetMethods.Add(targetMethod);
    }

    public bool ContainsTargetMethod(MethodInfo targetMethod)
    {
        return TargetMethods.Contains(targetMethod) ||
            CategorizedTargetMethods.Values.Any(methods => methods.Contains(targetMethod));
    }
}

public class Debuggable<T>
{
    public T Value;
    public bool Debug;
    // Route this member's per-change sends through the per-tick coalescer instead of SendAll.
    public bool Coalesce;

    public Debuggable(T value, bool debug, bool coalesce = false)
    {
        Value = value;
        Debug = debug;
        Coalesce = coalesce;
    }
}
