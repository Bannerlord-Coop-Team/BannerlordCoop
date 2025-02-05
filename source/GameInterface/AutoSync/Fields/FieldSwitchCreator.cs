using Common.Logging;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.AutoSync.Fields;
public class FieldSwitchCreator
{
    private readonly TypeBuilder typeBuilder;

    private readonly FieldBuilder objectManagerField;
    private readonly FieldBuilder loggerField;
    private readonly Type instanceType;
    private readonly IObjectManager objectManager;

    public FieldSwitchCreator(ModuleBuilder moduleBuilder, Type type, IObjectManager objectManager)
    {
        instanceType = type;
        this.objectManager = objectManager;
        typeBuilder = moduleBuilder.DefineType($"FieldSwitcher_{type.Name}",
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);

        loggerField = typeBuilder.DefineField("logger", typeof(ILogger), FieldAttributes.Private | FieldAttributes.InitOnly | FieldAttributes.Static);

        CreateStaticCtor();

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard | CallingConventions.HasThis,
            new Type[] { typeof(IObjectManager) });

        objectManagerField = typeBuilder.DefineField("objectManager", typeof(IObjectManager), FieldAttributes.Private | FieldAttributes.InitOnly);

        var il = ctorBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, AccessTools.Constructor(typeof(object)));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, objectManagerField);

        il.Emit(OpCodes.Ret);
    }

    private void CreateStaticCtor()
    {
        var cctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static,
            CallingConventions.Standard,
            null);

        var il = cctorBuilder.GetILGenerator();

        il.Emit(OpCodes.Call, AccessTools.Method(typeof(LogManager), nameof(LogManager.GetLogger)).MakeGenericMethod(typeBuilder));
        il.Emit(OpCodes.Stsfld, loggerField);

        il.Emit(OpCodes.Ret);
    }

    private MethodBuilder CreateSwitch(FieldInfo[] fields)
    {
        var methodBuilder = typeBuilder.DefineMethod("FieldSwitch",
            MethodAttributes.Public,
            null,
            new Type[] { typeof(FieldAutoSyncPacket) });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "packet");


        var il = methodBuilder.GetILGenerator();

        var instanceLocal = il.DeclareLocal(instanceType);

        var labels = fields.Select(i => il.DefineLabel()).ToArray();

        var retLabel = il.DefineLabel();
        var switchLabel = il.DefineLabel();


        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, objectManagerField);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(FieldAutoSyncPacket), nameof(FieldAutoSyncPacket.instanceId)));

        il.Emit(OpCodes.Ldloca, instanceLocal);

        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetObject)).MakeGenericMethod(instanceType));
        il.Emit(OpCodes.Brtrue, switchLabel);

        il.Emit(OpCodes.Ldsfld, loggerField);

        var errorString = $"Unable to find instance of type {instanceType.Name} with id ";
        il.Emit(OpCodes.Ldstr, errorString);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(FieldAutoSyncPacket), nameof(FieldAutoSyncPacket.instanceId)));

        // Concat strings
        var stringConcatMethod = AccessTools.Method(typeof(FieldSwitchCreator), nameof(Concat));

        il.Emit(OpCodes.Call, stringConcatMethod);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        il.Emit(OpCodes.Ret);


        il.MarkLabel(switchLabel);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(FieldAutoSyncPacket), nameof(FieldAutoSyncPacket.fieldId)));
        il.Emit(OpCodes.Switch, labels);

        for (int i = 0; i < fields.Length; i++)
        {
            il.MarkLabel(labels[i]);

            if (RuntimeTypeModel.Default.CanSerialize(fields[i].FieldType))
                CreateByValue(il, fields[i], instanceLocal);
            else if (objectManager.IsTypeManaged(fields[i].FieldType))
                CreateByRef(il, fields[i], instanceLocal);
            else
                throw new NotSupportedException(
                    $"{fields[i].FieldType.Name} is not serializable and not managed by the object manager. " +
                    $"Either manage the type using the object manager or make this type serializable");
        }

        il.MarkLabel(retLabel);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private void CreateByValue(ILGenerator il, FieldInfo field, LocalBuilder instanceLocal)
    {
        // Loads the instance (used by strfld)
        il.Emit(OpCodes.Ldloc, instanceLocal);

        // Load and deserialize the new value casted as field type
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(FieldAutoSyncPacket), nameof(FieldAutoSyncPacket.value)));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(FieldSwitchCreator), nameof(Deserialize)).MakeGenericMethod(field.FieldType));
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
    }

    private void CreateByRef(ILGenerator il, FieldInfo field, LocalBuilder instanceLocal)
    {
        var errorString = $"Unable to find instance of type {instanceType.Name} with id ";
        var stringConcatMethod = AccessTools.Method(typeof(FieldSwitchCreator), nameof(Concat));

        var valueLocal = il.DeclareLocal(field.FieldType);

        il.Emit(OpCodes.Ldloc, instanceLocal);

        // Load objectmanager
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, objectManagerField);

        var getObjectSuccess = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(FieldAutoSyncPacket), nameof(FieldAutoSyncPacket.value)));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(FieldSwitchCreator), nameof(Deserialize)).MakeGenericMethod(typeof(string)));

        il.Emit(OpCodes.Ldloca, valueLocal);

        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetObject)).MakeGenericMethod(field.FieldType));
        il.Emit(OpCodes.Brtrue, getObjectSuccess);

        // if TryGetObject failes log error
        il.Emit(OpCodes.Ldsfld, loggerField);

        il.Emit(OpCodes.Ldstr, errorString);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(FieldAutoSyncPacket), nameof(FieldAutoSyncPacket.value)));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(FieldSwitchCreator), nameof(Deserialize)).MakeGenericMethod(typeof(string)));

        il.Emit(OpCodes.Call, stringConcatMethod);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(getObjectSuccess);

        il.Emit(OpCodes.Ldloc, valueLocal);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
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

    public TypeInfo Build(FieldInfo[] fields)
    {
        CreateSwitch(fields);

        return typeBuilder.CreateTypeInfo();
    }
}
