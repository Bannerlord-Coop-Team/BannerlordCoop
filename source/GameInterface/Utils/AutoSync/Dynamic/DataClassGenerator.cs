using GameInterface.Utils.AutoSync.Template;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Utils.AutoSync.Dynamic;

public class DataClassGenerator
{
    private readonly ModuleBuilder moduleBuilder;
    private int protoMemberCounter = 1;

    public DataClassGenerator(ModuleBuilder moduleBuilder)
    {
        this.moduleBuilder = moduleBuilder;
    }

    public Type GenerateClass(Type dataType, string name)
    {
        var attributeBuilder = new CustomAttributeBuilder(
            typeof(ProtoContractAttribute).GetConstructor(Type.EmptyTypes),
            new object[0]);

        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            $"{name}Data",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit,
            typeof(ValueType)
        );

        typeBuilder.AddInterfaceImplementation(typeof(IEquatable<>).MakeGenericType(typeBuilder));
        typeBuilder.AddInterfaceImplementation(typeof(IAutoSyncData<>).MakeGenericType(dataType));

        typeBuilder.SetCustomAttribute(attributeBuilder);


        var objIdFieldBuilder = CreateSerializableProperty(typeBuilder, "StringId", typeof(string));
        var dataFieldBuilder = CreateSerializableProperty(typeBuilder, "Value", dataType);

        var fields = new FieldInfo[] { objIdFieldBuilder, dataFieldBuilder };

        BuildContructor(typeBuilder, fields);

        MakeRecord(typeBuilder, fields);

        return typeBuilder.CreateTypeInfo();
    }

    private ConstructorBuilder BuildContructor(TypeBuilder tb, FieldInfo[] fields)
    {
        ConstructorBuilder constructor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, fields.Select(field => field.FieldType).ToArray());
        ILGenerator il = constructor.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

        for (int i = 0; i < fields.Length; i++)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_S, i + 1);
            il.Emit(OpCodes.Stfld, fields[i]);
        }

        il.Emit(OpCodes.Ret);

        return constructor;
    }

    private void MakeRecord(TypeBuilder typeBuilder, IEnumerable<FieldInfo> fields)
    {
        // Implement IEquatable<T>.Equals method
        MethodBuilder equalsMethod = typeBuilder.DefineMethod("Equals", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, typeof(bool), new Type[] { typeBuilder });
        ILGenerator il = equalsMethod.GetILGenerator();

        var returnFalse = il.DefineLabel();

        foreach (var field in fields)
        {
            il.Emit(OpCodes.Call, typeof(EqualityComparer<>).MakeGenericType(field.FieldType).GetProperty("Default").GetGetMethod());
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Callvirt, typeof(EqualityComparer<>).MakeGenericType(field.FieldType).GetMethod(nameof(Equals), new Type[] { field.FieldType, field.FieldType }));
            il.Emit(OpCodes.Brfalse, returnFalse);
        }

        // Default return true
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(returnFalse);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        
    }

    private FieldBuilder CreateSerializableProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
    {
        var customAttributeBuilder = new CustomAttributeBuilder(
            typeof(ProtoMemberAttribute).GetConstructor(new Type[] { typeof(int) }),
            new object[] { protoMemberCounter++ });

        // Define a private field for the property
        FieldBuilder fieldBuilder = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private | FieldAttributes.InitOnly);
        fieldBuilder.SetCustomAttribute(customAttributeBuilder);

        // Define the getter method
        MethodBuilder getMethodBuilder = typeBuilder.DefineMethod("get_" + propertyName,
                                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName |
                                        MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final,
                                        propertyType, null);

        // Generate IL for the getter method
        ILGenerator ilGenerator = getMethodBuilder.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);                 // Load "this" onto the stack
        ilGenerator.Emit(OpCodes.Ldfld, fieldBuilder);     // Load the field onto the stack
        ilGenerator.Emit(OpCodes.Ret);                     // Return the field value

        // Define the property
        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
        propertyBuilder.SetGetMethod(getMethodBuilder);

        // Return the fieldBuilder, which may be useful if you want to further modify the field
        return fieldBuilder;
    }
}
