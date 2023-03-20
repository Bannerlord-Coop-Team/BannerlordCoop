using Autofac;

namespace GameInterface.Services
{
    internal interface IServiceModule
    {
        void InstantiateServices(IContainer container);
    }
}