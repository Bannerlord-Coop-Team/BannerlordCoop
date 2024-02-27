using GameInterface.Utils.AutoSync.Template;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Utils.AutoSync.Dynamic;
public class EventMessageGenerator
{
    public Type GenerateEvent(ModuleBuilder moduleBuilder, Type dataType)
    {
        TypeBuilder typeBuilder = moduleBuilder.DefineType($"{dataType.Name}Changed");

        typeBuilder.AddInterfaceImplementation(typeof(IAutoSyncMessage<>).MakeGenericType(dataType));

        var dataField = CreateProperty(typeBuilder, "Data", typeof(IAutoSyncData<>).MakeGenericType(dataType));

        GenerateConstructor(typeBuilder, new FieldInfo[] { dataField });

        return typeBuilder.CreateTypeInfo();
    }

    public ConstructorInfo GenerateConstructor(TypeBuilder typeBuilder, FieldInfo[] fields)
    {
        ConstructorBuilder constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public, CallingConventions.Standard,
            fields.Select(field => field.FieldType).ToArray());
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

    private FieldBuilder CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
    {
        // Define a private field for the property
        FieldBuilder fieldBuilder = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private | FieldAttributes.InitOnly);

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
