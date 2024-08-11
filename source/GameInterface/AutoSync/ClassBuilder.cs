using Common.Serialization;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace GameInterface.AutoSync;
internal class ClassBuilder
{
    public object CreateNewObject(FieldInfo field)
    {
        var myType = CompileResultType(field);
        return Activator.CreateInstance(myType);
    }
    public Type CompileResultType(FieldInfo field)
    {
        TypeBuilder tb = GetTypeBuilder(field);

        tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

        return tb.CreateTypeInfo();
    }

    private TypeBuilder GetTypeBuilder(FieldInfo field)
    {
        var typeSignature = "MyDynamicType";
        var an = new AssemblyName(typeSignature);

        var asmName = field.DeclaringType.Assembly.GetName().Name;


        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);

        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);

        CreateMethod(tb, "Prefix", null);
        CreateSetter(tb, field);

        CustomAttributeBuilder myCABuilder = new CustomAttributeBuilder(
            AccessTools.Constructor(typeof(IgnoresAccessChecksToAttribute), new Type[] { typeof(string) }),
            new object[] { asmName });

        assemblyBuilder.SetCustomAttribute(myCABuilder);

        return tb;
    }

    private MethodBuilder CreateMethod(TypeBuilder typeBuilder, string name, params Type[] types)
    {
        var methodBuilder = typeBuilder.DefineMethod(name, MethodAttributes.Static | MethodAttributes.Public);

        ILGenerator il = methodBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private MethodBuilder CreateSetter(TypeBuilder typeBuilder, FieldInfo field)
    {
        var methodBuilder = typeBuilder.DefineMethod("FieldSetter", MethodAttributes.Static | MethodAttributes.Public, null, new Type[] { field.DeclaringType, field.FieldType });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "obj");
        methodBuilder.DefineParameter(2, ParameterAttributes.In, "value");

        ILGenerator il = methodBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);

        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private MethodBuilder CreateSwitch(TypeBuilder typeBuilder, IEnumerable<FieldInfo> fields)
    {
        var field_array = fields.ToArray();

        var methodBuilder = typeBuilder.DefineMethod("FieldSetter", MethodAttributes.Static | MethodAttributes.Public, null, new Type[] { typeof(int) });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "instance");
        methodBuilder.DefineParameter(2, ParameterAttributes.In, "value");

        var il = methodBuilder.GetILGenerator();

        var labels = field_array.Select(i => il.DefineLabel()).ToArray();

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Switch, labels);

        for (int i = 0; i < field_array.Length; i++)
        {
            il.MarkLabel(labels[i]);
            il.Emit(OpCodes.Stfld, field_array[i]);
            il.Emit(OpCodes.Ret);
        }

        return methodBuilder;
    }
}
public class ASDF
{
    private string SomeField = "Zero";

    public string GetField => SomeField;
}