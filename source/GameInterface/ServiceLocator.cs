using Autofac;

namespace GameInterface
{
    public static class ServiceLocator
    {
        private static ILifetimeScope _lifetimeScope;

        public static void SetContainer(ILifetimeScope lifetimeScope)
        {
            _lifetimeScope = lifetimeScope;
        }

        public static T Resolve<T>()
        {
            return _lifetimeScope.Resolve<T>();
        }
    }
}
