using Autofac;

namespace Missions
{
    public static class ContainerProvider
    {
        private static IContainer Container;

        public static void SetContainer(IContainer container) 
        {
            if (container == null) return;

            Container?.Dispose();

            Container = container;
        }

        public static bool TryResolve<T>(out T resolvedObj)
        {
            resolvedObj = default;

            if (Container == null) return false;

            resolvedObj = Container.Resolve<T>();

            return true;
        }
    }
}
