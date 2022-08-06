using Autofac;
using Coop.Debug.Logger;
using Coop.Mod;
using Coop.Mod.Client;
using Coop.Mod.Config;
using Coop.Mod.LogicStates.Client;
using Coop.Mod.LogicStates.Server;

namespace Coop
{
    public class Bootstrap
    {
        /// <summary>
        ///     Initialize coop mod stuff.
        /// </summary>
        /// <param name="isServer"></param>
        /// <returns>Container</returns>
        public static IContainer Initialize(bool isServer)
        {
            var builder = new ContainerBuilder();
            
            #if DEBUG
            builder.RegisterType<NLogLogger>().As<ILogger>();
            #endif

            if (isServer)
                InitializeServer(builder);
            else
                InitializeClient(builder);
            
            return builder.Build();
        }

        /// <summary>
        ///     Initialize client components to have an proper Inversion of Control
        ///     by using Autofac library. 
        /// </summary>
        /// <param name="builder">Container Builder</param>
        private static void InitializeClient(ContainerBuilder builder)
        {
            builder.RegisterType<ClientLogic>().As<IClientLogic>();
            builder.RegisterType<CoopClient>().As<ICoopClient>().SingleInstance();
        }

        /// <summary>
        ///     Initialize server components to have an proper Inversion of Control
        ///     by using Autofac library. 
        /// </summary>
        /// <param name="builder">Container Builder</param>
        private static void InitializeServer(ContainerBuilder builder)
        {
            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>()
                .OwnedByLifetimeScope();
            builder.RegisterType<ServerLogic>().As<IServerLogic>();
            builder.RegisterType<CoopServer>().As<ICoopServer>();
        }
    }
}