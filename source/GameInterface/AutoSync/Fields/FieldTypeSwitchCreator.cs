using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.AutoSync.Fields;

public interface IFieldTypeSwitcher
{
    void TypeSwitch(FieldAutoSyncPacket autoSyncFieldPacket);
}


internal class FieldTypeSwitchCreator
{
    private readonly TypeBuilder typeBuilder;
    private readonly ModuleBuilder moduleBuilder;
    private readonly IObjectManager objectManager;

    public FieldTypeSwitchCreator(ModuleBuilder moduleBuilder, IObjectManager objectManager)
    {
        typeBuilder = moduleBuilder.DefineType("FieldTypeSwitcher",
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null,
                new Type[] { typeof(IFieldTypeSwitcher) });

        this.moduleBuilder = moduleBuilder;
        this.objectManager = objectManager;
    }

    private MethodBuilder CreateSwitch(Dictionary<Type, List<FieldInfo>> fieldMap)
    {
        var types = fieldMap.Keys.ToArray();

        var fieldSwitches = CreateFieldSwitches(fieldMap);

        var methodBuilder = typeBuilder.DefineMethod("TypeSwitch",
            MethodAttributes.Public | MethodAttributes.Virtual,
            null,
            new Type[] { typeof(FieldAutoSyncPacket) });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "packet");

        typeBuilder.DefineMethodOverride(methodBuilder, AccessTools.Method(typeof(IFieldTypeSwitcher), nameof(IFieldTypeSwitcher.TypeSwitch)));

        var il = methodBuilder.GetILGenerator();

        var labels = types.Select(i => il.DefineLabel()).ToArray();

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(FieldAutoSyncPacket), nameof(FieldAutoSyncPacket.typeId)));
        il.Emit(OpCodes.Switch, labels);

        // Add property switching
        for (int i = 0; i < types.Length; i++)
        {
            il.MarkLabel(labels[i]);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, fieldSwitches[i]);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, AccessTools.Method(fieldSwitches[i].FieldType, "FieldSwitch"));
            il.Emit(OpCodes.Ret);
        }

        return methodBuilder;
    }

    private FieldBuilder[] CreateFieldSwitches(Dictionary<Type, List<FieldInfo>> fieldMap)
    {
        var types = fieldMap.Keys.ToArray();
        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard | CallingConventions.HasThis,
            new Type[] { typeof(IObjectManager) });

        var fieldSwitches = new List<FieldBuilder>();

        var objectManagerField = typeBuilder.DefineField("objectManager", typeof(IObjectManager), FieldAttributes.Private | FieldAttributes.InitOnly);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, AccessTools.Constructor(typeof(object)));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, objectManagerField);

        foreach (var type in types)
        {
            var fieldSwitchBuilder = new FieldSwitchCreator(moduleBuilder, type, objectManager);
            var fieldSwitchType = fieldSwitchBuilder.Build(fieldMap[type].ToArray());

            var fieldSwitchField = typeBuilder.DefineField(fieldSwitchType.Name, fieldSwitchType, FieldAttributes.Private | FieldAttributes.InitOnly);

            fieldSwitches.Add(fieldSwitchField);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, fieldSwitchType.Constructor(new Type[] { typeof(IObjectManager) }));
            il.Emit(OpCodes.Stfld, fieldSwitchField);
        }

        il.Emit(OpCodes.Ret);

        return fieldSwitches.ToArray();
    }

    public TypeInfo Build(Dictionary<Type, List<FieldInfo>> fieldMap)
    {
        CreateSwitch(fieldMap);

        return typeBuilder.CreateTypeInfo();
    }
}
