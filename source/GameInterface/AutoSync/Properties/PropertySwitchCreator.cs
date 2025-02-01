using Common.Logging;
using Common.Util;
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
using TaleWorlds.Diamond;

namespace GameInterface.AutoSync.Properties;
public class PropertySwitchCreator
{
    private readonly TypeBuilder typeBuilder;

    private readonly FieldBuilder objectManagerField;
    private readonly FieldBuilder loggerField;
    private readonly Type instanceType;
    private readonly IObjectManager objectManager;

    public PropertySwitchCreator(ModuleBuilder moduleBuilder, Type type, IObjectManager objectManager)
    {
        instanceType = type;
        this.objectManager = objectManager;
        typeBuilder = moduleBuilder.DefineType($"PropertySwitcher_{type.Name}",
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

    private MethodBuilder CreateSwitch(PropertyInfo[] properties)
    {
        var methodBuilder = typeBuilder.DefineMethod("PropertySwitch",
            MethodAttributes.Public,
            null,
            new Type[] { typeof(PropertyAutoSyncPacket) });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "packet");


        var il = methodBuilder.GetILGenerator();

        var instanceLocal = il.DeclareLocal(instanceType);

        var labels = properties.Select(i => il.DefineLabel()).ToArray();

        var retLabel = il.DefineLabel();
        var switchLabel = il.DefineLabel();


        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, objectManagerField);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(PropertyAutoSyncPacket), nameof(PropertyAutoSyncPacket.instanceId)));

        il.Emit(OpCodes.Ldloca, instanceLocal);

        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetObject)).MakeGenericMethod(instanceType));
        il.Emit(OpCodes.Brtrue, switchLabel);

        il.Emit(OpCodes.Ldsfld, loggerField);

        var errorString = $"Unable to find instance of type {instanceType.Name} with id ";
        il.Emit(OpCodes.Ldstr, errorString);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(PropertyAutoSyncPacket), nameof(PropertyAutoSyncPacket.instanceId)));

        // Concat strings
        var stringConcatMethod = AccessTools.Method(typeof(PropertySwitchCreator), nameof(Concat));

        il.Emit(OpCodes.Call, stringConcatMethod);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        il.Emit(OpCodes.Ret);


        il.MarkLabel(switchLabel);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(PropertyAutoSyncPacket), nameof(PropertyAutoSyncPacket.propertyId)));
        il.Emit(OpCodes.Switch, labels);

        for (int i = 0; i < properties.Length; i++)
        {
            il.MarkLabel(labels[i]);

            if (RuntimeTypeModel.Default.CanSerialize(properties[i].PropertyType))
                CreateByValue(il, properties[i], instanceLocal);
            else if (objectManager.IsTypeManaged(properties[i].PropertyType))
                CreateByRef(il, properties[i], instanceLocal);
            else
                throw new NotSupportedException(
                    $"{properties[i].PropertyType.Name} is not serializable and not managed by the object manager. " +
                    $"Either manage the type using the object manager or make this type serializable");
        }

        il.MarkLabel(retLabel);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private void CreateByValue(ILGenerator il, PropertyInfo property, LocalBuilder instanceLocal)
    {
        // Loads the instance (used by set method)
        il.Emit(OpCodes.Ldloc, instanceLocal);

        // Load and deserialize the new value casted as field type
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(PropertyAutoSyncPacket), nameof(PropertyAutoSyncPacket.value)));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(PropertySwitchCreator), nameof(Deserialize)).MakeGenericMethod(property.PropertyType));

        il.Emit(OpCodes.Call, AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.AllowThisThread)));
        il.Emit(OpCodes.Callvirt, property.GetSetMethod() ?? property.GetSetMethod(true) ?? throw new InvalidOperationException($"{property.Name} does not have a set method"));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.RevokeThisThread)));

        il.Emit(OpCodes.Ret);
    }

    private void CreateByRef(ILGenerator il, PropertyInfo property, LocalBuilder instanceLocal)
    {
        var errorString = $"Unable to find instance of type {instanceType.Name} with id ";
        var stringConcatMethod = AccessTools.Method(typeof(PropertySwitchCreator), nameof(Concat));

        var valueLocal = il.DeclareLocal(property.PropertyType);

        il.Emit(OpCodes.Ldloc, instanceLocal);

        // Load objectmanager
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, objectManagerField);

        var getObjectSuccess = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(PropertyAutoSyncPacket), nameof(PropertyAutoSyncPacket.value)));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(PropertySwitchCreator), nameof(Deserialize)).MakeGenericMethod(typeof(string)));

        il.Emit(OpCodes.Ldloca, valueLocal);

        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetObject)).MakeGenericMethod(property.PropertyType));
        il.Emit(OpCodes.Brtrue, getObjectSuccess);

        // if TryGetObject failes log error
        il.Emit(OpCodes.Ldsfld, loggerField);

        il.Emit(OpCodes.Ldstr, errorString);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(PropertyAutoSyncPacket), nameof(PropertyAutoSyncPacket.value)));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(PropertySwitchCreator), nameof(Deserialize)).MakeGenericMethod(typeof(string)));

        il.Emit(OpCodes.Call, stringConcatMethod);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(getObjectSuccess);

        il.Emit(OpCodes.Ldloc, valueLocal);

        il.Emit(OpCodes.Call, AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.AllowThisThread)));
        il.Emit(OpCodes.Callvirt, AccessTools.PropertySetter(property.DeclaringType, property.Name));
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.RevokeThisThread)));

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

    public TypeInfo Build(PropertyInfo[] properties)
    {
        CreateSwitch(properties);

        return typeBuilder.CreateTypeInfo();
    }
}
