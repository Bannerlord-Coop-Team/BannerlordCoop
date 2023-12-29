using Autofac;

namespace GameInterface;

public static class ContainerProvider
{
    private static ILifetimeScope _lifetimeScope;

    public static void SetContainer(ILifetimeScope lifetimeScope)
    {
        _lifetimeScope = lifetimeScope;
    }

    public static bool TryGetContainer(out ILifetimeScope lifetimeScope)
    {
        lifetimeScope = _lifetimeScope;

        return lifetimeScope != null;
    }

    public static bool TryResolve<T>(out T instance) where T : class
    {
        instance = null;

        if (TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out instance);
    }
}
