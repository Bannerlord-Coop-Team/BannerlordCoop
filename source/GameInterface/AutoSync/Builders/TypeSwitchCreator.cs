using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using GameInterface.Services.ObjectManager;

namespace GameInterface.AutoSync.Builders;
internal class TypeSwitchCreator
{
    private readonly TypeBuilder typeBuilder;

    public TypeSwitchCreator(ModuleBuilder moduleBuilder)
    {
        // TODO add custom attr to asm for every type asm
        //CustomAttributeBuilder myCABuilder = new CustomAttributeBuilder(
        //    AccessTools.Constructor(typeof(IgnoresAccessChecksToAttribute), new Type[] { typeof(string) }),
        //    new object[] { asmName });
        //assemblyBuilder.SetCustomAttribute(myCABuilder);


        typeBuilder = moduleBuilder.DefineType("TypeSwitcher",
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);

        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
    }

    private MethodBuilder CreateSwitch(Type[] types)
    {
        var methodBuilder = typeBuilder.DefineMethod("TypeSwitch",
            MethodAttributes.Public,
            typeof(int) /* TODO remove */,
            new Type[] { typeof(int) });
        methodBuilder.DefineParameter(1, ParameterAttributes.In, "typeId");

        var il = methodBuilder.GetILGenerator();

        var labels = types.Select(i => il.DefineLabel()).ToArray();

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Switch, labels);

        for (int i = 0; i < types.Length; i++)
        {
            il.MarkLabel(labels[i]);
            //il.Emit(OpCodes.Stfld, types_array[i]);
            il.Emit(OpCodes.Ldc_I4, i); /* TODO remove */
            il.Emit(OpCodes.Ret);
        }

        return methodBuilder;
    }

    public dynamic Build(Type[] types)
    {
        CreateSwitch(types);

        var type = typeBuilder.CreateTypeInfo();

        return Activator.CreateInstance(type);
    }
}
