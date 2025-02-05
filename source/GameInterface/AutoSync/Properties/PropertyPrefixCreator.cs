using Common.Logging;
using Common.Network;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.AutoSync.Properties;
public class PropertyPrefixCreator
{
    private readonly TypeBuilder typeBuilder;

    private readonly FieldBuilder loggerField;
    private readonly IObjectManager objectManager;
    private readonly int typeId;
    private readonly PropertyInfo[] properties;

    public PropertyPrefixCreator(IObjectManager objectManager, ModuleBuilder moduleBuilder, Type type, int typeId, PropertyInfo[] interceptProperties)
    {
        this.objectManager = objectManager;
        this.typeId = typeId;
        this.properties = interceptProperties;

        typeBuilder = moduleBuilder.DefineType($"{type.Name}PropertyPrefixes",
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
            null);

        var il = ctorBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, AccessTools.Constructor(typeof(object)));

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

    public string GetPrefixName(PropertyInfo property)
    {
        return $"{property.DeclaringType.Name}_{property.Name}_Prefix";
    }

    private MethodBuilder CreatePropertyByValPrefix(int typeId, int propId, PropertyInfo prop)
    {
        var methodBuilder = typeBuilder.DefineMethod(GetPrefixName(prop),
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(bool),
            new Type[] { prop.DeclaringType, prop.PropertyType });
        var instanceArg = methodBuilder.DefineParameter(1, ParameterAttributes.In, "__instance");
        var valueArg = methodBuilder.DefineParameter(2, ParameterAttributes.In, "value");

        var il = methodBuilder.GetILGenerator();

        IsThreadAllowed(il);

        IsClientCheck(il, prop);

        var networkLocal = TryResolve<INetwork>(il);

        var objectManagerLocal = TryResolve<IObjectManager>(il);
        var idLocal = TryGetId(il, OpCodes.Ldarg_0, objectManagerLocal);

        il.Emit(OpCodes.Ldloc, networkLocal);
        il.Emit(OpCodes.Ldloc, idLocal);
        il.Emit(OpCodes.Ldc_I4, typeId);
        il.Emit(OpCodes.Ldc_I4, propId);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Box, prop.PropertyType);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Serialize)));

        il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(PropertyAutoSyncPacket), new Type[] { typeof(string), typeof(int), typeof(int), typeof(byte[]) }));
        il.Emit(OpCodes.Box, typeof(PropertyAutoSyncPacket));

        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(INetwork), nameof(INetwork.SendAll), new Type[] { typeof(IPacket) }));

        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private MethodBuilder CreatePropertyByRefPrefix(int typeId, int propId, PropertyInfo prop)
    {
        var methodBuilder = typeBuilder.DefineMethod(GetPrefixName(prop),
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(bool),
            new Type[] { prop.DeclaringType, prop.PropertyType });
        var instanceArg = methodBuilder.DefineParameter(1, ParameterAttributes.In, "__instance");
        var valueArg = methodBuilder.DefineParameter(2, ParameterAttributes.In, "value");

        var il = methodBuilder.GetILGenerator();

        IsThreadAllowed(il);

        IsClientCheck(il, prop);

        var networkLocal = TryResolve<INetwork>(il);
        var objectManagerLocal = TryResolve<IObjectManager>(il);

        var idLocal = TryGetId(il, OpCodes.Ldarg_0, objectManagerLocal);
        var valueIdLocal = TryGetId(il, OpCodes.Ldarg_1, objectManagerLocal);

        il.Emit(OpCodes.Ldloc, networkLocal);
        il.Emit(OpCodes.Ldloc, idLocal);
        il.Emit(OpCodes.Ldc_I4, typeId);
        il.Emit(OpCodes.Ldc_I4, propId);

        il.Emit(OpCodes.Ldloc, valueIdLocal);
        il.Emit(OpCodes.Box, prop.PropertyType);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Serialize)));

        il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(PropertyAutoSyncPacket), new Type[] { typeof(string), typeof(int), typeof(int), typeof(byte[]) }));
        il.Emit(OpCodes.Box, typeof(PropertyAutoSyncPacket));
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(INetwork), nameof(INetwork.SendAll), new Type[] { typeof(IPacket) }));

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Syncing {prop.Name} for {prop.DeclaringType}");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(PropertyPrefixCreator), nameof(LogMessage)));

        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private LocalBuilder TryResolve<T>(ILGenerator il)
    {
        var validLabel = il.DefineLabel();
        var local = il.DeclareLocal(typeof(T));

        // Attempt resolve
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ContainerProvider), nameof(ContainerProvider.TryResolve)).MakeGenericMethod(typeof(T)));
        il.Emit(OpCodes.Brtrue, validLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Unable to resolve {nameof(T)}");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(PropertyPrefixCreator), nameof(LogMessage)));

        // Return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(validLabel);

        return local;
    }

    private LocalBuilder TryGetId(ILGenerator il, OpCode argOpcode, LocalBuilder objectManagerLocal)
    {
        var validLabel = il.DefineLabel();
        var idLocal = il.DeclareLocal(typeof(string));

        il.Emit(OpCodes.Ldloc, objectManagerLocal);

        // load instance
        il.Emit(argOpcode);
        il.Emit(OpCodes.Ldloca, idLocal);

        // Try resolve instance id
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetId)));
        il.Emit(OpCodes.Brtrue, validLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Could not resolve id");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(PropertyPrefixCreator), nameof(LogMessage)));

        // Return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(validLabel);

        return idLocal;
    }

    private void IsThreadAllowed(ILGenerator il)
    {
        var notAllowedLabel = il.DefineLabel();

        il.Emit(OpCodes.Call, AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.IsThisThreadAllowed)));
        il.Emit(OpCodes.Brfalse, notAllowedLabel);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(notAllowedLabel);
    }

    private void IsClientCheck(ILGenerator il, MemberInfo field)
    {
        il.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(ModInformation), nameof(ModInformation.IsClient)));
        var notClientLabel = il.DefineLabel();

        il.Emit(OpCodes.Brfalse, notClientLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Client attempted to change {field.Name}");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(PropertyPrefixCreator), nameof(LogMessage)));

        // Return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(notClientLabel);
    }

    public Type Build()
    {
        for (int i = 0; i < properties.Length; i++)
        {
            var propType = properties[i].PropertyType;
            if (RuntimeTypeModel.Default.CanSerialize(propType))
                CreatePropertyByValPrefix(typeId, i, properties[i]);
            else if (objectManager.IsTypeManaged(propType))
                CreatePropertyByRefPrefix(typeId, i, properties[i]);
            else
                throw new NotSupportedException(
                    $"{propType.Name} is not serializable and not managed by the object manager. " +
                    $"Either manage the type using the object manager or make this type serializable");
        }

        return typeBuilder.CreateTypeInfo();
    }

    public static void LogMessage(ILogger logger, string message)
    {
        logger.Debug(message);
        //DebugMessageLogger.Write(message);
    }
}

