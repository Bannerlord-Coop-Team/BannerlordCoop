using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameInterface.Utils.AutoSync.Dynamic;
internal class DelegateGenerator
{
    private readonly ModuleBuilder moduleBuilder;

    public DelegateGenerator(ModuleBuilder moduleBuilder)
    {
        this.moduleBuilder = moduleBuilder;
    }

    public DelegateResults GenerateAnonymousFunction(string name, Type returnType, Type[] parameterTypes)
    {
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
           $"Auto_Sync_{name}",
           TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass,
           null);

        var fields = GenerateConstructor(typeBuilder, parameterTypes);

        var methodBuilder = GenerateInvokeMethod(typeBuilder, returnType, parameterTypes);

        return new DelegateResults(typeBuilder, fields, methodBuilder);
    }

    private FieldBuilder[] GenerateConstructor(TypeBuilder typeBuilder, Type[] parameterTypes)
    {
        var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                parameterTypes);

        var il = constructor.GetILGenerator();

        var fields = parameterTypes.Select((type, index) => typeBuilder.DefineField($"_arg{index}", type, FieldAttributes.Private)).ToArray();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

        for (int i = 0; i < fields.Length; i++)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_S, i + 1);
            il.Emit(OpCodes.Stfld, fields[i]);
        }

        il.Emit(OpCodes.Ret);

        return fields;
    }

    private MethodBuilder GenerateInvokeMethod(TypeBuilder typeBuilder, Type returnType, Type[] parameterTypes)
    {
        return typeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
            returnType,
            parameterTypes);
    }

    public class DelegateResults
    {
        internal DelegateResults(
            TypeBuilder delegateType,
            FieldBuilder[] delegateFields,
            MethodBuilder delegateBuilder)
        {
            DelegateType = delegateType;
            DelegateFields = delegateFields;
            DelegateBuilder = delegateBuilder;
        }

        public TypeBuilder DelegateType { get; }
        public FieldBuilder[] DelegateFields { get; }
        public MethodBuilder DelegateBuilder { get; }
    }
}
