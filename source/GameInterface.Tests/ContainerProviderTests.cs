using Autofac;
using System;
using Xunit;

namespace GameInterface.Tests;

/// <summary>
/// Covers how <see cref="ContainerProvider.TryResolve{T}"/> reuses instances per scope.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class ContainerProviderTests
{
    [Fact]
    public void SharedRegistration_SecondResolveSkipsAutofac()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<Service>().As<IService>().InstancePerLifetimeScope();
        using var container = builder.Build();
        int resolveOperations = 0;
        container.ResolveOperationBeginning += (sender, args) => resolveOperations++;

        var (first, second) = ResolveTwice<IService>(container);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, resolveOperations);
    }

    [Fact]
    public void PerDependencyRegistration_ResolvesNewInstanceEachTime()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<Service>().As<IService>().InstancePerDependency();
        using var container = builder.Build();

        var (first, second) = ResolveTwice<IService>(container);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void MissingRegistration_FailsEveryTime()
    {
        using var container = new ContainerBuilder().Build();

        bool firstResolved = false;
        bool secondResolved = false;
        UseScope(container, () =>
        {
            firstResolved = ContainerProvider.TryResolve<IService>(out _);
            secondResolved = ContainerProvider.TryResolve<IService>(out _);
        });

        Assert.False(firstResolved);
        Assert.False(secondResolved);
    }

    [Fact]
    public void SwappingScope_ResolvesFromTheActiveScope()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<Service>().As<IService>().InstancePerLifetimeScope();
        using var container = builder.Build();
        using var childScope = container.BeginLifetimeScope();

        var (parentFirst, _) = ResolveTwice<IService>(container);
        var (childFirst, _) = ResolveTwice<IService>(childScope);
        var (parentAgain, _) = ResolveTwice<IService>(container);

        Assert.NotSame(parentFirst, childFirst);
        Assert.Same(parentFirst, parentAgain);
        Assert.Same(childScope.Resolve<IService>(), childFirst);
    }

    [Fact]
    public void ClearedScope_FailsToResolve()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<Service>().As<IService>().SingleInstance();
        using var container = builder.Build();

        bool resolvedAfterClear = true;
        UseScope(container, () =>
        {
            ContainerProvider.TryResolve<IService>(out _);
            ContainerProvider.Clear();
            resolvedAfterClear = ContainerProvider.TryResolve<IService>(out _);
        });

        Assert.False(resolvedAfterClear);
    }

    /// <summary>
    /// Resolves <typeparamref name="T"/> twice through <see cref="ContainerProvider"/> with <paramref name="scope"/> active.
    /// </summary>
    private static (T First, T Second) ResolveTwice<T>(ILifetimeScope scope) where T : class
    {
        T first = null;
        T second = null;
        UseScope(scope, () =>
        {
            ContainerProvider.TryResolve(out first);
            ContainerProvider.TryResolve(out second);
        });

        return (first, second);
    }

    /// <summary>
    /// Runs <paramref name="action"/> with <paramref name="scope"/> active, then puts the previous scope back for other tests.
    /// </summary>
    private static void UseScope(ILifetimeScope scope, Action action)
    {
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            using (ContainerProvider.UseContainerThreadSafe(scope))
            {
                action();
            }
        }
        finally
        {
            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }
    }

    private interface IService
    {
    }

    private class Service : IService
    {
    }
}
