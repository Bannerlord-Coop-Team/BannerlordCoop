#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using TaleWorlds.Engine;

namespace Coop.Tests.Missions.Battles;

/// <summary>Uninitialized engine objects and private-field writes shared by the naval lab unit tests.</summary>
internal static class NavalLabTestShells
{
    // Engine assemblies are not publicized by Coop.Tests; supply the managed script catalog the type initializers expect.
    internal static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes");
        var previous = field.GetValue(null);
        try
        {
            if (previous == null) field.SetValue(null, new Dictionary<string, Type>());
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
        finally { field.SetValue(null, previous); }
    }

    internal static void Set(object target, string name, object? value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);

    internal static GameEntity EntityAtEngineBoundary()
    {
        var entity = AtEngineBoundary(Shell<GameEntity>);
        // GameEntity equality treats a zero pointer as null.
        Set(entity, "<Pointer>k__BackingField", new UIntPtr(0x1234));
        return entity;
    }

    internal static Scene SceneAtEngineBoundary() => AtEngineBoundary(Shell<Scene>);

    // NativeObject's initializer needs IManaged; the shell never reaches native code and is not finalized.
    private static T AtEngineBoundary<T>(Func<T> create) where T : class
    {
        var field = AccessTools.Field(typeof(TaleWorlds.DotNet.NativeObject).Assembly
            .GetType("TaleWorlds.DotNet.LibraryApplicationInterface"), "IManaged");
        var previous = field.GetValue(null);
        try
        {
            field.SetValue(null, typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create))!
                .MakeGenericMethod(field.FieldType, typeof(NativeReferenceBoundary)).Invoke(null, null));
            var value = create();
            GC.SuppressFinalize(value);
            return value;
        }
        finally { field.SetValue(null, previous); }
    }

    public class NativeReferenceBoundary : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod!.ReturnType == typeof(int) ? 0 : null;
    }
}
#endif
