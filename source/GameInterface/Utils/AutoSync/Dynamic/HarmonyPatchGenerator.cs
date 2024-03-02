using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Utils.AutoSync.Template;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Utils.AutoSync.Dynamic;
public class HarmonyPatchGenerator
{
    private static readonly ILogger Logger = LogManager.GetLogger<HarmonyPatchGenerator>();
    private readonly TypeBuilder autoPatchType;
    private readonly Type dataClassType;
    private readonly Type eventType;

    public HarmonyPatchGenerator(ModuleBuilder moduleBuilder, PropertyInfo propertyInfo, Type dataClassType, Type eventClassType)
    {
        autoPatchType = moduleBuilder.DefineType($"AutoSync_{propertyInfo.PropertyType.Name}_{propertyInfo.Name}_Patches");
        this.dataClassType = dataClassType;
        this.eventType = eventClassType;
    }

    public MethodInfo GenerateSetterPrefixPatch<T>(MethodInfo setMethod, Func<T, string> idGetterMethod) where T : class
    {
        if (idGetterMethod.Method.IsStatic == false)
        {
            throw new ArgumentException("IdGetterMethod must be static");
        }

        if (typeof(T) != setMethod.DeclaringType)
        {
            throw new ArgumentException($"T must be of type {setMethod.DeclaringType.Name}");
        }

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

        setterPrefixPatch.DefineParameter(1, ParameterAttributes.In, "__instance");
        setterPrefixPatch.DefineParameter(2, ParameterAttributes.In, "value");

        ILGenerator il = setterPrefixPatch.GetILGenerator();

        var returnTrue = il.DefineLabel();
        var returnFalse = il.DefineLabel();


        il.Emit(OpCodes.Call, AccessTools.Method(typeof(CallOriginalPolicy), nameof(CallOriginalPolicy.IsOriginalAllowed)));
        il.Emit(OpCodes.Brtrue, returnTrue);

        il.Emit(OpCodes.Ldstr, valueType.Name);
        il.Emit(OpCodes.Call, AccessTools.Method(GetType(), nameof(IsClient)));
        il.Emit(OpCodes.Brtrue, returnFalse);

        il.Emit(OpCodes.Call, AccessTools.Method(GetType(), nameof(GetMessageBroker)));
        il.Emit(OpCodes.Ldarg_0);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, idGetterMethod.Method);
        il.Emit(OpCodes.Ldarg_1);

        il.Emit(OpCodes.Newobj, dataClassType.GetConstructors().Single());
        il.Emit(OpCodes.Newobj, eventType.GetConstructors().Single());

        var castedPublish = AccessTools.Method(typeof(MessageBroker), nameof(MessageBroker.Publish)).MakeGenericMethod(eventType);
        il.Emit(OpCodes.Callvirt, castedPublish);

        il.MarkLabel(returnTrue);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(returnFalse);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        var compiledType = autoPatchType.CreateTypeInfo();
        return compiledType.GetMethod(setterPrefixPatch.Name, BindingFlags.NonPublic | BindingFlags.Static);
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

    public static IMessageBroker GetMessageBroker()
    {
        if (ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
        {
            return messageBroker;
        }

        return MessageBroker.Instance;
    }

    public static void Test(Type obj)
    {
        ;
    }
}

public class TestEvent : IEvent//, IAutoSyncMessage<T>
{
    public object Data { get; }

    public TestEvent(object data)
    {
        Data = data;
    }
}