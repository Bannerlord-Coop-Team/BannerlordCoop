using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.AutoSync.Properties;

public interface IPropertyTypeSwitcher
{
    void TypeSwitch(PropertyAutoSyncPacket autoSyncFieldPacket);
}


internal class PropertyTypeSwitchCreator
{
    private readonly TypeBuilder typeBuilder;
    private readonly ModuleBuilder moduleBuilder;
    private readonly IObjectManager objectManager;

    public PropertyTypeSwitchCreator(ModuleBuilder moduleBuilder, IObjectManager objectManager)
    {
        typeBuilder = moduleBuilder.DefineType("PropertyTypeSwitcher",
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null,
                new Type[] { typeof(IPropertyTypeSwitcher) });

        this.moduleBuilder = moduleBuilder;
        this.objectManager = objectManager;
    }

    private MethodBuilder CreateSwitch(Dictionary<Type, List<PropertyInfo>> propertyMap)
    {
        var types = propertyMap.Keys.ToArray();

        var propertySwitches = CreateSwitches(propertyMap);

        var methodBuilder = typeBuilder.DefineMethod("TypeSwitch",
            MethodAttributes.Public | MethodAttributes.Virtual,
            null,
            new Type[] { typeof(PropertyAutoSyncPacket) });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "packet");

        typeBuilder.DefineMethodOverride(methodBuilder, AccessTools.Method(typeof(IPropertyTypeSwitcher), nameof(IPropertyTypeSwitcher.TypeSwitch)));

        var il = methodBuilder.GetILGenerator();

        var labels = types.Select(i => il.DefineLabel()).ToArray();

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(PropertyAutoSyncPacket), nameof(PropertyAutoSyncPacket.typeId)));
        il.Emit(OpCodes.Switch, labels);

        for (int i = 0; i < types.Length; i++)
        {
            il.MarkLabel(labels[i]);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, propertySwitches[i]);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, AccessTools.Method(propertySwitches[i].FieldType, "PropertySwitch"));
            il.Emit(OpCodes.Ret);
        }

        return methodBuilder;
    }

    private FieldBuilder[] CreateSwitches(Dictionary<Type, List<PropertyInfo>> propertyMap)
    {
        var types = propertyMap.Keys.ToArray();
        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard | CallingConventions.HasThis,
            new Type[] { typeof(IObjectManager) });

        var propertySwitches = new List<FieldBuilder>();

        var objectManagerField = typeBuilder.DefineField("objectManager", typeof(IObjectManager), FieldAttributes.Private | FieldAttributes.InitOnly);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, AccessTools.Constructor(typeof(object)));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, objectManagerField);

        foreach (var type in types)
        {
            var propertySwitchBuilder = new PropertySwitchCreator(moduleBuilder, type, objectManager);
            var propertySwitchType = propertySwitchBuilder.Build(propertyMap[type].ToArray());

            var propertySwitchField = typeBuilder.DefineField(propertySwitchType.Name, propertySwitchType, FieldAttributes.Private | FieldAttributes.InitOnly);

            propertySwitches.Add(propertySwitchField);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, propertySwitchType.Constructor(new Type[] { typeof(IObjectManager) }));
            il.Emit(OpCodes.Stfld, propertySwitchField);
        }

        il.Emit(OpCodes.Ret);

        return propertySwitches.ToArray();
    }

    public TypeInfo Build(Dictionary<Type, List<PropertyInfo>> propertyMap)
    {
        CreateSwitch(propertyMap);

        return typeBuilder.CreateTypeInfo();
    }
}
