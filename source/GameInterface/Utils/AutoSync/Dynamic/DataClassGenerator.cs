using Common.Messaging;
using ProtoBuf;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Utils.AutoSync.Dynamic;

public class DataClassGenerator
{
    private readonly ModuleBuilder moduleBuilder;

    public DataClassGenerator(ModuleBuilder moduleBuilder)
    {
        this.moduleBuilder = moduleBuilder;
    }

    public Type GenerateClass(Type dataType, string name)
    {
        var attributeBuilder = new CustomAttributeBuilder(
            typeof(ProtoContractAttribute).GetConstructor(Type.EmptyTypes),
            new object[0]);

        TypeBuilder typeBuilder = GetTypeBuilder($"AutoSync_{name}");
        typeBuilder.SetCustomAttribute(attributeBuilder);
        typeBuilder.AddInterfaceImplementation(typeof(ICommand));

        int protoMemberCounter = 1;
        var objIdFieldBuilder = CreateSerializableProperty(typeBuilder, "StringId", typeof(string), ref protoMemberCounter);
        var dataFieldBuilder = CreateSerializableProperty(typeBuilder, dataType.Name, dataType, ref protoMemberCounter);

        BuildContructor(typeBuilder, objIdFieldBuilder, dataFieldBuilder);

        return typeBuilder.CreateTypeInfo();
    }

    private ConstructorBuilder BuildContructor(TypeBuilder tb, FieldBuilder objId, FieldBuilder dataField)
    {
        ConstructorBuilder constructor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(string), dataField.FieldType });
        ILGenerator ilGenerator = constructor.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Ldarg_1);
        ilGenerator.Emit(OpCodes.Stfld, objId);
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Ldarg_2);
        ilGenerator.Emit(OpCodes.Stfld, dataField);
        ilGenerator.Emit(OpCodes.Ret);

        return constructor;
    }

    private TypeBuilder GetTypeBuilder(string name)
    {
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        return moduleBuilder.DefineType(name,
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AnsiClass |
                TypeAttributes.AutoLayout |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);
    }

    private FieldBuilder CreateSerializableProperty(TypeBuilder tb, string propertyName, Type propertyType, ref int protoMemberCounter)
    {
        var customAttributeBuilder = new CustomAttributeBuilder(
            typeof(ProtoMemberAttribute).GetConstructor(new Type[] { typeof(int) }),
            new object[] { protoMemberCounter++ });

        FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
        fieldBuilder.SetCustomAttribute(customAttributeBuilder);

        PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, Type.EmptyTypes);

        // Build get method
        MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
        ILGenerator getIl = getPropMthdBldr.GetILGenerator();

        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, fieldBuilder);
        getIl.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getPropMthdBldr);

        return fieldBuilder;
    }
}
