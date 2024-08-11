using Common.Logging;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.AutoSync.Builders;
public class FieldSwitchCreator
{
    private static readonly ILogger Logger = LogManager.GetLogger<FieldSwitchCreator>();

    private readonly TypeBuilder typeBuilder;

    private readonly FieldBuilder objectManagerField;
    private readonly FieldBuilder loggerField;
    private readonly Type instanceType;

    public FieldSwitchCreator(ModuleBuilder moduleBuilder, Type type)
    {
        instanceType = type;

        typeBuilder = moduleBuilder.DefineType($"FieldSwitcher_{type.Name}",
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard | CallingConventions.HasThis,
            new Type[] { typeof(IObjectManager), typeof(ILogger) });

        objectManagerField = typeBuilder.DefineField("objectManager", typeof(IObjectManager), FieldAttributes.Private | FieldAttributes.InitOnly);
        loggerField = typeBuilder.DefineField("logger", typeof(ILogger), FieldAttributes.Private | FieldAttributes.InitOnly);

        var il = ctorBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, AccessTools.Constructor(typeof(object)));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, objectManagerField);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Stfld, loggerField);

        il.Emit(OpCodes.Ret);
    }

    

    private MethodBuilder CreateSwitch(FieldInfo[] fields)
    {
        var methodBuilder = typeBuilder.DefineMethod("FieldSwitch",
            MethodAttributes.Public,
            null,
            new Type[] { typeof(AutoSyncFieldPacket) });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "packet");


        var il = methodBuilder.GetILGenerator();

        il.DeclareLocal(instanceType);

        var labels = fields.Select(i => il.DefineLabel()).ToArray();

        var retLabel = il.DefineLabel();
        var switchLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, objectManagerField);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(AutoSyncFieldPacket), nameof(AutoSyncFieldPacket.instanceId)));

        il.Emit(OpCodes.Ldloca_S, 0);

        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetObject)).MakeGenericMethod(instanceType));
        il.Emit(OpCodes.Brtrue, switchLabel);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, loggerField);

        var str = $"Unable to find instance of type {instanceType.Name} with id ";
        il.Emit(OpCodes.Ldstr, str);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(AutoSyncFieldPacket), nameof(AutoSyncFieldPacket.instanceId)));

        // Concat strings
        var stringConcatMethod = AccessTools.Method(typeof(FieldSwitchCreator), nameof(Concat));

        il.Emit(OpCodes.Call, stringConcatMethod);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Pop);

        il.Emit(OpCodes.Ret);


        il.MarkLabel(switchLabel);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(AutoSyncFieldPacket), nameof(AutoSyncFieldPacket.fieldId)));
        il.Emit(OpCodes.Switch, labels);

        for (int i = 0; i < fields.Length; i++)
        {
            il.MarkLabel(labels[i]);
            il.Emit(OpCodes.Ldloc, 0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(AutoSyncFieldPacket), nameof(AutoSyncFieldPacket.value)));
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(FieldSwitchCreator), nameof(Deserialize)).MakeGenericMethod(fields[i].FieldType));
            il.Emit(OpCodes.Stfld, fields[i]);
            il.Emit(OpCodes.Ret);
        }

        il.MarkLabel(retLabel);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }


    public static T Deserialize<T>(byte[] bytes)
    {
        using (var ms = new MemoryStream(bytes))
        {
            return Serializer.Deserialize<T>(ms);
        }
    }

    public static string Concat(string str1, string str2)
    {
        return str1 + str2;
    }

    public dynamic Build(FieldInfo[] fields, IObjectManager objectManager)
    {
        CreateSwitch(fields);

        var type = typeBuilder.CreateTypeInfo();

        return Activator.CreateInstance(type, objectManager, Logger);
    }
}
