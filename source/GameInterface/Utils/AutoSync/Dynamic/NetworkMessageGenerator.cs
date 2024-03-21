using GameInterface.Utils.AutoSync.Template;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Utils.AutoSync.Dynamic;

/// <summary>
/// Serializable network message type generator.
/// </summary>
/// <remarks>
/// An network message type is a type that implements <see cref="IAutoSyncMessage{T}"/> interface.
/// Conains a single property of type <see cref="IAutoSyncData{T}"/>.
/// </remarks>
public class NetworkMessageGenerator
{
    public Type GenerateNetworkMessage(ModuleBuilder moduleBuilder, PropertyInfo property)
    {
        TypeBuilder typeBuilder = moduleBuilder.DefineType($"AutoSync_Network{property.DeclaringType.Name}_{property.Name}_Changed",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | TypeAttributes.BeforeFieldInit,
            typeof(object));

        return GenerateNetworkMessageInternal(property.PropertyType, typeBuilder);
    }

    public Type GenerateNetworkMessage(ModuleBuilder moduleBuilder, FieldInfo field)
    {
        TypeBuilder typeBuilder = moduleBuilder.DefineType($"AutoSync_Network{field.DeclaringType.Name}_{field.Name}_Changed",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | TypeAttributes.BeforeFieldInit,
            typeof(object));

        return GenerateNetworkMessageInternal(field.FieldType, typeBuilder);
    }

    public Type GenerateNetworkMessageInternal(Type dataType, TypeBuilder typeBuilder)
    {
        if (Serializer.NonGeneric.CanSerialize(dataType) == false)
        {
            throw new ArgumentException($"{dataType} is not serializable, try creating a surrogate");
        }

        var attributeBuilder = new CustomAttributeBuilder(
            typeof(ProtoContractAttribute).GetConstructor(Type.EmptyTypes),
            new object[0],
            new PropertyInfo[] { AccessTools.Property(typeof(ProtoContractAttribute), "SkipConstructor") },
            new object[] { true });

        typeBuilder.SetCustomAttribute(attributeBuilder);

        typeBuilder.AddInterfaceImplementation(typeof(IAutoSyncMessage<>).MakeGenericType(dataType));

        var dataField = CreateProperty(typeBuilder, "Data", typeof(IAutoSyncData<>).MakeGenericType(dataType));

        var fields = new FieldInfo[] { dataField };

        GenerateConstructor(typeBuilder, fields);
        GenerateGetHashCode(typeBuilder, fields);

        return typeBuilder.CreateTypeInfo();
    }

    public ConstructorInfo GenerateConstructor(TypeBuilder typeBuilder, FieldInfo[] fields)
    {
        ConstructorBuilder constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig,
            CallingConventions.Standard,
            fields.Select(field => field.FieldType).ToArray());
        ILGenerator il = constructor.GetILGenerator();

        // Call default object constructor
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

        // Load arguments into fields
        // It is assumed that all arguments are passed to the constructor in the same order as the fields are defined
        for (int i = 0; i < fields.Length; i++)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_S, i + 1);
            il.Emit(OpCodes.Stfld, fields[i]);
        }

        il.Emit(OpCodes.Ret);

        return constructor;
    }

    private void GenerateGetHashCode(TypeBuilder typeBuilder, FieldInfo[] fields)
    {
        MethodBuilder getHashCodeMethodBuilder = typeBuilder.DefineMethod("GetHashCode",
                                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot |
                                            MethodAttributes.Virtual | MethodAttributes.Final,
                                            typeof(int), Type.EmptyTypes);

        ILGenerator ilGenerator = getHashCodeMethodBuilder.GetILGenerator();

        // Load a constant seed value (recommended for hash code generation)
        ilGenerator.Emit(OpCodes.Ldc_I4, 17);

        foreach (var field in fields)
        {
            ilGenerator.Emit(OpCodes.Ldarg_0); // Load "this"
            ilGenerator.Emit(OpCodes.Ldfld, field); // Load the field
            ilGenerator.Emit(OpCodes.Callvirt, typeof(object).GetMethod("GetHashCode")); // Call GetHashCode on the field
            ilGenerator.Emit(OpCodes.Xor); // XOR the current hash code with the field's hash code
        }

        ilGenerator.Emit(OpCodes.Ret); // Return the hash code

        // Override GetHashCode method
        typeBuilder.DefineMethodOverride(getHashCodeMethodBuilder, typeof(object).GetMethod("GetHashCode"));
    }

    private FieldBuilder CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
    {
        // Define a private field for the property
        FieldBuilder fieldBuilder = typeBuilder.DefineField($"_{propertyName}", propertyType, FieldAttributes.Private | FieldAttributes.InitOnly);

        // Define the getter method
        MethodBuilder getMethodBuilder = typeBuilder.DefineMethod($"get_{propertyName}",
                                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName |
                                        MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final,
                                        propertyType, Type.EmptyTypes);

        // Generate IL for the getter method
        ILGenerator ilGenerator = getMethodBuilder.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);                 // Load "this" onto the stack
        ilGenerator.Emit(OpCodes.Ldfld, fieldBuilder);     // Load the field onto the stack
        ilGenerator.Emit(OpCodes.Ret);                     // Return the field value

        // Define the property
        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, Type.EmptyTypes);
        propertyBuilder.SetGetMethod(getMethodBuilder);

        // Return the fieldBuilder, which may be useful if you want to further modify the field
        return fieldBuilder;
    }
       
}
