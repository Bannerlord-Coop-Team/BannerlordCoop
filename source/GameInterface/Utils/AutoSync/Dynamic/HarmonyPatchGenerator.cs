using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using ProtoBuf.Meta;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Utils.AutoSync.Dynamic;
public class HarmonyPatchGenerator
{
    private static readonly ILogger Logger = LogManager.GetLogger<HarmonyPatchGenerator>();
    private readonly DataClassGenerator dataClassGenerator;
    private readonly TypeBuilder autoPatchType;

    public HarmonyPatchGenerator(ModuleBuilder moduleBuilder, PropertyInfo propertyInfo)
    {
        dataClassGenerator = new DataClassGenerator(moduleBuilder);
        autoPatchType = moduleBuilder.DefineType($"AutoSync_{propertyInfo.PropertyType.Name}_{propertyInfo.Name}_Patches");
    }

    public MethodInfo GenerateSetterPrefixPatch<T>(MethodInfo setMethod, Func<T, string> idGetterMethod) where T : class
    {
        var classType = setMethod.DeclaringType;

        var valueType = setMethod.GetParameters().Single().ParameterType;
        var parameters = new Type[] {
            classType,
            valueType,
        };

        var setterPrefixPatch = autoPatchType.DefineMethod(
            name: $"AutoSync_Prefix_{setMethod.Name}",
            attributes: MethodAttributes.Private | MethodAttributes.Static,
            callingConvention: CallingConventions.Standard,
            returnType: typeof(bool),
            parameterTypes: parameters);

        setterPrefixPatch.DefineParameter(0, ParameterAttributes.In, "__instance");
        setterPrefixPatch.DefineParameter(1, ParameterAttributes.In, "value");

        ILGenerator ilGenerator = setterPrefixPatch.GetILGenerator();

        var jumpTable = new Label[]
        {
            ilGenerator.DefineLabel(),
        };

        var dataClassType = dataClassGenerator.GenerateClass(valueType, setMethod.Name);


        ilGenerator.Emit(OpCodes.Call, AccessTools.Method(typeof(CallOriginalPolicy), nameof(CallOriginalPolicy.IsOriginalAllowed)));
        ilGenerator.Emit(OpCodes.Brtrue, jumpTable[0]);

        ilGenerator.Emit(OpCodes.Ldstr, valueType.Name);
        ilGenerator.Emit(OpCodes.Call, AccessTools.Method(GetType(), nameof(IsClient)));
        ilGenerator.Emit(OpCodes.Brtrue, jumpTable[0]);

        ilGenerator.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(MessageBroker), nameof(MessageBroker.Instance)));
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Call, idGetterMethod.Method);
        ilGenerator.Emit(OpCodes.Ldarg_1);

        ilGenerator.Emit(OpCodes.Newobj, dataClassType.GetConstructors().Single());

        var castedPublish = AccessTools.Method(typeof(MessageBroker), nameof(MessageBroker.Publish)).MakeGenericMethod(dataClassType);
        ilGenerator.Emit(OpCodes.Callvirt, castedPublish);

        ilGenerator.MarkLabel(jumpTable[0]);
        ilGenerator.Emit(OpCodes.Ldc_I4_1);
        ilGenerator.Emit(OpCodes.Ret);

        return setterPrefixPatch;
    }

    public static bool IsClient(string typeName)
    {
        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeName, Environment.StackTrace);
            return true;
        }

        return false;
    }
}
