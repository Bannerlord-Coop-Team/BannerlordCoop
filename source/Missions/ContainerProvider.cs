using Autofac;

namespace Missions
{
    public static class ContainerProvider
    {
        private static IContainer container;

        public static void SetContainer(IContainer container) 
        {
            if (container == null) return;

            ContainerProvider.container = container;
        }

        public static bool TryResolve<T>(out T resolvedObj)
        {
            resolvedObj = default;

            if (container == null) return false;

            resolvedObj = container.Resolve<T>();

            return true;
        }
    }
}
