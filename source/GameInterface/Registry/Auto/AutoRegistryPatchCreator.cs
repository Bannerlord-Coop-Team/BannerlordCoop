using GameInterface.AutoSync;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.Registry.Auto;

public interface IAutoRegistryPatchCreator
{
    string AddCreateEvent(Type eventTemplateType);
    string AddDestroyEvent(Type eventTemplateType);
    Type Build();
}

internal class AutoRegistryPatchCreator : IAutoRegistryPatchCreator
{
    private static int AsmCounter = 0;

    private readonly AssemblyBuilder assemblyBuilder;
    private readonly ModuleBuilder moduleBuilder;
    private readonly TypeBuilder typeBuilder;
    private readonly IAutoSyncPatchCollector syncPatchCollector;

    public AutoRegistryPatchCreator(IAutoSyncPatchCollector syncPatchCollector)
    {

        assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"AutoRegistryAsm_{AsmCounter++}"), AssemblyBuilderAccess.RunAndCollect);

        moduleBuilder = assemblyBuilder.DefineDynamicModule("AutoRegistryModule");

        typeBuilder = moduleBuilder.DefineType($"AutoRegistryPatches",
            TypeAttributes.Public |
            TypeAttributes.Class |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            null);
        this.syncPatchCollector = syncPatchCollector;
    }

    public string AddCreateEvent(Type type)
    {
        var patch = typeof(LifetimePatches<>).MakeGenericType(type).GetMethod("CreatePrefix", BindingFlags.Public | BindingFlags.Static);
        return AddEventGeneric(type, $"CreationPrefix", patch);
    }

    public string AddDestroyEvent(Type type)
    {
        var patch = typeof(LifetimePatches<>).MakeGenericType(type).GetMethod("DestroyPostfix", BindingFlags.Public | BindingFlags.Static);
        return AddEventGeneric(type, $"DestroyPostfix", patch);
    }

    private string AddEventGeneric(Type type, string suffix, MethodInfo patch)
    {
        var methodName = $"{type.Name}{suffix}";
        var methodBuilder = typeBuilder.DefineMethod(
            methodName,
            MethodAttributes.Static | MethodAttributes.Public,
            null,
            new Type[] { type, typeof(MethodBase) });

        var parameterNumber = 1;

        methodBuilder.DefineParameter(parameterNumber++, ParameterAttributes.None, "__instance");
        methodBuilder.DefineParameter(parameterNumber++, ParameterAttributes.None, "__originalMethod");

        var il = methodBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, patch);
        il.Emit(OpCodes.Ret);

        return methodName;
    }

    public Type Build() => typeBuilder.CreateTypeInfo();
}
