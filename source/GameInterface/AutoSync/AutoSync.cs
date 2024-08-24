using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameInterface.AutoSync;
internal class AutoSync
{
    readonly List<FieldInfo> fields = new List<FieldInfo>();
    readonly List<PropertyInfo> properties = new List<PropertyInfo>();

    AssemblyBuilder dynamicAssembly;

    public void AddField(FieldInfo field, IEnumerable<MethodInfo> externalUseMethods)
    {
        if (fields.Contains(field))
        {
            throw new ArgumentException($"Field {field.Name} was already added");
        }

        fields.Add(field);
    }

    public void AddProperty(PropertyInfo property)
    {
        if (properties.Contains(property))
        {
            throw new ArgumentException($"Field {property.Name} was already added");
        }

        properties.Add(property);
    }

    public void Build()
    {
        var allFields = new Dictionary<Type, List<FieldInfo>>();

        foreach (FieldInfo field in fields.Distinct())
        {
            var type = field.DeclaringType;

            if (allFields.ContainsKey(type))
            {
                allFields[type] = new List<FieldInfo>();
            }

            allFields[type].Add(field);
        }

        // TODO properties
        //foreach (var property in properties)
        //{
        //    var type = property.DeclaringType;
        //    if (handledTypes.Contains(type)) continue;

        //    types.Add(type);
        //    handledTypes.Add(type);

        //    // Build type switch
        //    // In type switch builder build field switch?
        //}
        // TODO add custom attr to asm for every type asm
        //CustomAttributeBuilder myCABuilder = new CustomAttributeBuilder(
        //    AccessTools.Constructor(typeof(IgnoresAccessChecksToAttribute), new Type[] { typeof(string) }),
        //    new object[] { asmName });
        //assemblyBuilder.SetCustomAttribute(myCABuilder);

        dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("AutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("AutoSyncAsm");
    }
}
