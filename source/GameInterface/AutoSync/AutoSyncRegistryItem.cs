using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync;

public class AutoSyncRegistryItem
{
    public HashSet<Debuggable<FieldInfo>> Fields = new();
    public HashSet<Debuggable<PropertyInfo>> Properties = new();

    public List<MethodInfo> TargetMethods = new();
    public string PatchCategory { get; private set; }

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
        if (PatchCategory != null && patchCategory != null && PatchCategory != patchCategory)
            throw new System.ArgumentException("AutoSync targets for the same type cannot use different patch categories");

        PatchCategory ??= patchCategory;
        TargetMethods.Add(targetMethod);
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
