using GameInterface.Registry.Auto;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.AutoSync;

/// <summary>
/// Guards against a constructor being Harmony-<b>prefixed</b> by more than one of Coop's lifetime
/// systems.
///
/// <para>
/// Two systems prefix constructors to track object creation: the generic <c>AutoRegistry</c>
/// (<see cref="IAutoRegistry{T}.Constructors"/> -> <c>LifetimePatches&lt;T&gt;.CreatePrefix</c>) and
/// hand-written <c>[HarmonyPatch]</c> classes that expose a <c>TargetMethods()</c> returning
/// constructors plus a prefix. When both prefix the same constructor it gets two stacked prefixes;
/// tearing the session down (<see cref="GameInterface.IGameInterface.UnpatchAll"/>) then removes one
/// prefix and forces Harmony to regenerate the wrapper of the remaining one, which throws
/// <see cref="InvalidProgramException"/> for a JIT-compiled constructor and crashes the game. This
/// happened with <c>Hideout..ctor</c> (HideoutLifetimePatches + HideoutRegistry) and
/// <c>GauntletMapEventVisual..ctor</c> (a stray <c>Debug</c> patch + GauntletMapEventVisualRegistry).
/// </para>
///
/// <para>
/// A transpiler (or postfix) stacked with a single prefix does <i>not</i> hit this — only multiple
/// prefixes do — so only prefix-contributing sources are counted.
/// </para>
/// </summary>
public class ConstructorPatchUniquenessTests
{
    private readonly ITestOutputHelper output;

    public ConstructorPatchUniquenessTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    // No-op factory so AutoRegistry instances can be constructed just to read their Constructors.
    private sealed class StubAutoRegistryFactory : IAutoRegistryFactory
    {
        public void AddRegistry<T>(AutoRegistryBase<T> autoRegistry) where T : class { }
        public void RegisterAll() { }
        public void Dispose() { }
    }

    [Fact]
    public void NoConstructorIsPrefixedByMoreThanOneLifetimeSystem()
    {
        var assembly = typeof(GameInterface.GameInterface).Assembly;

        var prefixedConstructors = new List<(MethodBase ctor, string source)>();
        prefixedConstructors.AddRange(AnnotatedConstructorPrefixTargets(assembly));
        prefixedConstructors.AddRange(AutoRegistryConstructorTargets(assembly));

        var duplicates = prefixedConstructors
            .GroupBy(t => t.ctor)
            .Where(g => g.Count() > 1)
            .ToArray();

        foreach (var dup in duplicates)
        {
            output.WriteLine($"{dup.Key.DeclaringType?.Name}..ctor prefixed by: {string.Join(", ", dup.Select(d => d.source))}");
        }

        Assert.True(duplicates.Length == 0,
            "A constructor is prefixed by more than one lifetime system; tearing the session down will throw InvalidProgramException. " +
            "Remove the redundant prefix (keep the AutoRegistry).");
    }

    // Hand-written [HarmonyPatch] classes that prefix constructors returned by `static TargetMethods()`.
    private static IEnumerable<(MethodBase, string)> AnnotatedConstructorPrefixTargets(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var targetMethods = type.GetMethod(
                "TargetMethods",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethods == null || !typeof(IEnumerable<MethodBase>).IsAssignableFrom(targetMethods.ReturnType))
                continue;

            // Only a prefix on a constructor stacks dangerously; a transpiler/postfix does not.
            if (!HasPrefix(type)) continue;

            IEnumerable<MethodBase> methods;
            try
            {
                methods = (IEnumerable<MethodBase>)targetMethods.Invoke(null, null);
            }
            catch
            {
                continue;
            }

            foreach (var method in methods.Where(m => m is ConstructorInfo))
                yield return (method, $"[HarmonyPatch] {type.Name}");
        }
    }

    // Harmony treats a method as a prefix if it is named "Prefix" or carries [HarmonyPrefix].
    private static bool HasPrefix(Type type)
    {
        return type
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(m => m.Name == "Prefix" || m.GetCustomAttribute<HarmonyPrefix>() != null);
    }

    // AutoRegistry implementations expose the constructors they patch via Constructors.
    private static IEnumerable<(MethodBase, string)> AutoRegistryConstructorTargets(Assembly assembly)
    {
        var factory = new StubAutoRegistryFactory();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;

            var autoRegistryInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutoRegistry<>));
            if (autoRegistryInterface == null) continue;

            object instance;
            try
            {
                instance = Activator.CreateInstance(type, args: new object[] { null, factory, null });
            }
            catch
            {
                continue;
            }

            var constructors = (IEnumerable<MethodBase>)type.GetProperty(nameof(IAutoRegistry<object>.Constructors)).GetValue(instance);
            foreach (var method in constructors.Where(m => m is ConstructorInfo))
                yield return (method, $"AutoRegistry {type.Name}");
        }
    }
}
