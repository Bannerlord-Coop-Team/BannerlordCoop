using Common.Messaging;
using Common.Network;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Utils.AutoSync.Dynamic;
public class HandlerGenerator
{
    private readonly ModuleBuilder moduleBuilder;

    public HandlerGenerator(ModuleBuilder moduleBuilder)
    {
        this.moduleBuilder = moduleBuilder;
    }

    public void GenerateHandler(Type messageType)
    {
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            $"{messageType.Name}Handler",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(IHandler));

        var messageBrokerField = typeBuilder.DefineField("messageBroker", typeof(IMessageBroker), FieldAttributes.Private);
        var networkField = typeBuilder.DefineField("network", typeof(INetwork), FieldAttributes.Private);

        GenerateConstructor(typeBuilder, messageType, messageBrokerField, networkField);
        GenerateDispose(typeBuilder, messageType);

        GenerateHandleMethod(typeBuilder, messageType);
    }

    private void GenerateConstructor(TypeBuilder typeBuilder, Type messageType, FieldBuilder messageBrokerField, FieldBuilder networkField)
    {
        var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new Type[] { typeof(IMessageBroker), typeof(INetwork) });



        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, messageBrokerField);

        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Stfld, networkField);

        // TODO subscribe to message
        il.Emit(OpCodes.Ldarg_1);


        il.Emit(OpCodes.Ret);
    }

    private void GenerateDispose(TypeBuilder typeBuilder, Type messageType)
    {
        var method = typeBuilder.DefineMethod(
            "Dispose",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            Type.EmptyTypes);

        var il = method.GetILGenerator();

        // Unsubscribe from message

        il.Emit(OpCodes.Ret);
    }

    private MethodBuilder GenerateHandleMethod(TypeBuilder typeBuilder, Type messageType)
    {
        var method = typeBuilder.DefineMethod(
            $"Handle_{messageType.Name}",
            MethodAttributes.Private,
            typeof(void),
            new Type[] { typeof(MessagePayload<>).MakeGenericType(messageType) });

        var il = method.GetILGenerator();

        // TODO resolve object from id (return if not found)
        // TODO call object setter using game thread and allowed thread

        il.Emit(OpCodes.Ret);

        return method;
    }
}
