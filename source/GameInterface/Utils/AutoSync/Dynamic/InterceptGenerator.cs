using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameInterface.Utils.AutoSync.Dynamic;

internal class TranspilerGenerator
{
    public TypeBuilder GenerateTranspiler(ModuleBuilder module)
    {

    }

    private FieldBuilder GenerateStaticConstructor(TypeBuilder typeBuilder)
    {
        // Define the static field
        var fieldBuilder = typeBuilder.DefineField("Logger", typeof(ILogger), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);

        // Define the static constructor
        var staticConstructor = typeBuilder.DefineTypeInitializer();
        var il = staticConstructor.GetILGenerator();

        // Call GetLogger<T> method from LogManager and assign it to the field
        var getLoggerMethod = AccessTools.Method(typeof(LogManager), nameof(LogManager.GetLogger)).MakeGenericMethod(typeBuilder);
        il.Emit(OpCodes.Call, getLoggerMethod);
        il.Emit(OpCodes.Stsfld, fieldBuilder);
        il.Emit(OpCodes.Ret);

        return fieldBuilder;
    }

    private void GenerateConstructor()
    {

    }
}
internal class InterceptGenerator
{
    public MethodInfo GenerateIntercept(TypeBuilder typeBuilder, FieldInfo field)
    {
        var parameters = new Type[]
        {
            field.DeclaringType,
            field.FieldType
        };

        var method = typeBuilder.DefineMethod(
            name: $"{field.Name}Intercept",
            attributes: MethodAttributes.Private | MethodAttributes.Static,
            callingConvention: CallingConventions.Standard,
            returnType: null,
            parameterTypes: parameters);

        
        var il = method.GetILGenerator();

        var labels = new Label[] {
            il.DefineLabel(),
        };

        il.Emit(OpCodes.Call, AccessTools.Method(typeof(CallOriginalPolicy), nameof(CallOriginalPolicy.IsOriginalAllowed)));
        il.Emit(OpCodes.Brfalse, labels[0]);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stfld, field);

        il.MarkLabel(labels[0]);
    }
}
