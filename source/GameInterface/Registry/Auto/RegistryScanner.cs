using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Logging;
using Serilog;
using GameInterface.Services;

namespace GameInterface.Registry.Auto
{
    /// <summary>
    /// Attribute to mark classes for automatic registry
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class AutoRegisterAttribute : Attribute
    {
        public Type RegistryType { get; }
        public bool ScanConstructors { get; set; } = true;
        public bool ScanDestructors { get; set; } = true;

        public AutoRegisterAttribute(Type registryType = null)
        {
            RegistryType = registryType;
        }
    }

    /// <summary>
    /// Scans assemblies for types marked with AutoRegister attribute
    /// </summary>
    public interface IRegistryScanner
    {
        void ScanAndRegister();
        void ScanAssembly(Assembly assembly);
        IEnumerable<Type> GetAutoRegisterTypes();
    }

    public class RegistryScanner : IRegistryScanner
    {
        private static readonly ILogger Logger = LogManager.GetLogger<RegistryScanner>();
        private readonly IAutoRegistryFactory registryFactory;
        private readonly HashSet<Type> processedTypes = new HashSet<Type>();

        public RegistryScanner(IAutoRegistryFactory registryFactory)
        {
            this.registryFactory = registryFactory ?? throw new ArgumentNullException(nameof(registryFactory));
        }

        public void ScanAndRegister()
        {
            Logger.Information("Starting automatic registry scan...");
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !a.GlobalAssemblyCache)
                .ToList();

            foreach (var assembly in assemblies)
            {
                try
                {
                    ScanAssembly(assembly);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to scan assembly {Assembly}", assembly.FullName);
                }
            }

            Logger.Information("Registry scan completed. Processed {Count} types", processedTypes.Count);
        }

        public void ScanAssembly(Assembly assembly)
        {
            if (assembly == null) return;

            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<AutoRegisterAttribute>() != null)
                    .ToList();

                foreach (var type in types)
                {
                    ProcessType(type);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Logger.Debug("Could not load all types from {Assembly}: {Error}", 
                    assembly.FullName, ex.Message);
            }
        }

        public IEnumerable<Type> GetAutoRegisterTypes()
        {
            var types = new List<Type>();
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !a.GlobalAssemblyCache);

            foreach (var assembly in assemblies)
            {
                try
                {
                    types.AddRange(
                        assembly.GetTypes()
                            .Where(t => t.GetCustomAttribute<AutoRegisterAttribute>() != null)
                    );
                }
                catch (Exception ex)
                {
                    Logger.Debug("Error scanning assembly {Assembly}: {Error}", 
                        assembly.FullName, ex.Message);
                }
            }

            return types;
        }

        private void ProcessType(Type type)
        {
            if (processedTypes.Contains(type))
            {
                Logger.Debug("Type {Type} already processed, skipping", type.Name);
                return;
            }

            var attribute = type.GetCustomAttribute<AutoRegisterAttribute>();
            if (attribute == null) return;

            try
            {
                Logger.Information("Auto-registering type {Type}", type.Name);
                
                var registryType = attribute.RegistryType ?? type;
                var constructors = attribute.ScanConstructors ? GetConstructors(type) : Enumerable.Empty<MethodBase>();
                var destructors = attribute.ScanDestructors ? GetDestructors(type) : Enumerable.Empty<MethodBase>();

                // Create AutoRegistry instance for this type
                var autoRegistryType = typeof(AutoRegistry<>).MakeGenericType(registryType);
                var autoRegistry = Activator.CreateInstance(autoRegistryType, new object[] { null, null });

                // Register with factory
                var registerMethod = registryFactory.GetType()
                    .GetMethod(nameof(IAutoRegistryFactory.RegisterType))
                    .MakeGenericMethod(registryType);

                if (registerMethod != null && autoRegistry != null)
                {
                    registerMethod.Invoke(registryFactory, new[] { autoRegistry });
                    processedTypes.Add(type);
                    Logger.Information("Successfully registered {Type}", type.Name);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to auto-register type {Type}", type.Name);
            }
        }

        private IEnumerable<MethodBase> GetConstructors(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Cast<MethodBase>()
                .ToList();

            // Also check for static factory methods
            var factoryMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == type && m.Name.StartsWith("Create"))
                .Cast<MethodBase>();

            constructors.AddRange(factoryMethods);

            return constructors;
        }

        private IEnumerable<MethodBase> GetDestructors(Type type)
        {
            var destructors = new List<MethodBase>();

            // Look for Dispose method
            var disposeMethod = type.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            if (disposeMethod != null)
            {
                destructors.Add(disposeMethod);
            }

            // Look for finalizer
            var finalizer = type.GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance);
            if (finalizer != null)
            {
                destructors.Add(finalizer);
            }

            // Look for custom cleanup methods
            var cleanupMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Contains("Destroy") || m.Name.Contains("Cleanup") || m.Name.Contains("Release"))
                .Cast<MethodBase>();

            destructors.AddRange(cleanupMethods);

            return destructors;
        }
    }
}