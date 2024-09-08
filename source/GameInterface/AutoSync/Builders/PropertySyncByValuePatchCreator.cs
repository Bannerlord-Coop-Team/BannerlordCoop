using Common.Logging;
using Common.Network;
using Common.PacketHandlers;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Library;

namespace GameInterface.AutoSync.Builders;
internal class PropertySyncByValuePatchCreator
{
    private readonly TypeBuilder typeBuilder;

    private readonly FieldBuilder loggerField;

    public PropertySyncByValuePatchCreator(ModuleBuilder moduleBuilder, Type type)
    {
        typeBuilder = moduleBuilder.DefineType($"{type.Name}_Patches",
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

    public void CreatePropertyPrefixes(int typeId, PropertyInfo[] properties)
    {
        for (int i = 0; i < properties.Length; i++)
        {
            CreatePropertyPrefix(typeId, i, properties[i]);
        }
    }

    private MethodBuilder CreatePropertyPrefix(int typeId, int propId, PropertyInfo prop)
    {
        var methodBuilder = typeBuilder.DefineMethod($"{prop.DeclaringType.Name}_{prop.Name}_Prefix",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(bool),
            new Type[] { prop.DeclaringType, prop.PropertyType });
        methodBuilder.DefineParameter(0, ParameterAttributes.In, "__instance");
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "value");

        var il = methodBuilder.GetILGenerator();

        IsClientCheck(il, prop);

        var networkLocal = TryResolve<INetwork>(il);
        var idLocal = TryGetId(il, prop.DeclaringType);

        il.Emit(OpCodes.Ldloc, networkLocal);
        il.Emit(OpCodes.Ldloc, idLocal);
        il.Emit(OpCodes.Ldc_I4, typeId);
        il.Emit(OpCodes.Ldc_I4, propId);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Box, prop.PropertyType);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Serialize)));

        il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(AutoSyncFieldPacket), new Type[] { typeof(string), typeof(int), typeof(int), typeof(byte[]) }));
        il.Emit(OpCodes.Box, typeof(AutoSyncFieldPacket));
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(INetwork), nameof(INetwork.SendAll), new Type[] { typeof(IPacket) }));

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
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        // Return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(validLabel);

        return local;
    }

    private LocalBuilder TryGetId(ILGenerator il, Type instanceType)
    {
        var objectManagerLocal = TryResolve<IObjectManager>(il);

        var validLabel = il.DefineLabel();
        var idLocal = il.DeclareLocal(typeof(string));

        il.Emit(OpCodes.Ldloc, objectManagerLocal);

        // load instance
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloca, idLocal);

        // Try resolve instance id
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetId)));
        il.Emit(OpCodes.Brtrue, validLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Could not resolve id");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        // Return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(validLabel);

        return idLocal;
    }

    private void IsClientCheck(ILGenerator il, MemberInfo field)
    {
        il.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(ModInformation), nameof(ModInformation.IsClient)));
        var notClientLabel = il.DefineLabel();

        il.Emit(OpCodes.Brfalse, notClientLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Client attempted to change {field.Name}");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        // Return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(notClientLabel);
    }

    public Type Build(int typeId, PropertyInfo[] props)
    {
        CreatePropertyPrefixes(typeId, props);

        return typeBuilder.CreateTypeInfo();
    }
}

