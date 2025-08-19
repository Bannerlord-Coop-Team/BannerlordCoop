using Autofac;
using Common.Logging;
using Serilog;

namespace GameInterface.Registry.Auto
{
    /// <summary>
    /// Autofac module for automatic registry initialization
    /// </summary>
    public class AutoRegistryModule : Module
    {
        private static readonly ILogger Logger = LogManager.GetLogger<AutoRegistryModule>();

        protected override void Load(ContainerBuilder builder)
        {
            Logger.Information("Loading AutoRegistry module...");

            // Register the scanner
            builder.RegisterType<RegistryScanner>()
                .As<IRegistryScanner>()
                .SingleInstance();

            // Register factory (if not already registered)
            builder.RegisterType<AutoRegistryFactory>()
                .As<IAutoRegistryFactory>()
                .SingleInstance()
                .IfNotRegistered(typeof(IAutoRegistryFactory));

            Logger.Information("AutoRegistry module loaded");
        }

        /// <summary>
        /// Call this after container is built to scan and register all types
        /// </summary>
        public static void InitializeAutoRegistries(ILifetimeScope scope)
        {
            Logger.Information("Initializing auto registries...");

            var scanner = scope.Resolve<IRegistryScanner>();
            scanner.ScanAndRegister();

            Logger.Information("Auto registries initialized");
        }
    }
}