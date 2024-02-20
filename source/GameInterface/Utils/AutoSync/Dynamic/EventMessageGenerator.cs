using Common.Messaging;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;

namespace GameInterface.Utils.AutoSync.Dynamic;
public class EventMessageGenerator
{
    public Type GenerateEvent(ModuleBuilder moduleBuilder, Type dataType)
    {
        TypeBuilder type = moduleBuilder.DefineType($"AutoSync_{dataType.Name}Changed");

        type.AddInterfaceImplementation(typeof(IEvent));

        GenerateConstructor(type, dataType);

        return type.CreateTypeInfo();
    }

    public void GenerateConstructor(TypeBuilder type, Type dataType)
    {
        ConstructorBuilder constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { dataType });

        ILGenerator il = constructor.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

        var dataField = CreateProperty(type, "Data", dataType);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, dataField);

        il.Emit(OpCodes.Ret);
    }

    private FieldBuilder CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
    {

        FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
        PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, Type.EmptyTypes);

        // Build get method
        MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
        ILGenerator il = getPropMthdBldr.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, fieldBuilder);
        il.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getPropMthdBldr);

        return fieldBuilder;
    }
}
