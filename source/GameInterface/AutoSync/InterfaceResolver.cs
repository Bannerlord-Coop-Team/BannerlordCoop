using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Logging;
using Serilog;

namespace GameInterface.AutoSync;

/// <summary>
/// Helper class to resolve concrete types for interface and abstract types
/// </summary>
public static class InterfaceResolver
{
    private static readonly ILogger Logger = LogManager.GetLogger<AutoSyncBuilder>();
    private static readonly Dictionary<Type, List<Type>> InterfaceToConcreteTypes = new Dictionary<Type, List<Type>>();
    private static readonly object CacheLock = new object();

    /// <summary>
    /// Tries to resolve concrete types for an interface or abstract type
    /// </summary>
    public static bool TryResolveConcreteTypes(Type interfaceType, out List<Type> concreteTypes)
    {
        concreteTypes = null;
        
        if (!interfaceType.IsInterface && !interfaceType.IsAbstract)
        {
            concreteTypes = new List<Type> { interfaceType };
            return true;
        }

        lock (CacheLock)
        {
            if (InterfaceToConcreteTypes.TryGetValue(interfaceType, out concreteTypes))
            {
                return concreteTypes.Count > 0;
            }

            concreteTypes = FindConcreteTypes(interfaceType);
            InterfaceToConcreteTypes[interfaceType] = concreteTypes;
            
            if (concreteTypes.Count == 0)
            {
                Logger.Warning("No concrete types found for {InterfaceType}", interfaceType.Name);
                return false;
            }

            Logger.Information("Found {Count} concrete types for {InterfaceType}: {Types}", 
                concreteTypes.Count, interfaceType.Name, string.Join(", ", concreteTypes.Select(t => t.Name)));
            
            return true;
        }
    }

    private static List<Type> FindConcreteTypes(Type interfaceType)
    {
        var concreteTypes = new List<Type>();
        
        try
        {
            // Search in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => !t.IsInterface && 
                                   !t.IsAbstract && 
                                   t.IsClass &&
                                   (interfaceType.IsAssignableFrom(t) || 
                                    (interfaceType.IsInterface && t.GetInterfaces().Contains(interfaceType))))
                        .ToList();
                    
                    concreteTypes.AddRange(types);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some assemblies might not be fully loaded, skip them
                    Logger.Debug("Could not load types from assembly {Assembly}: {Error}", 
                        assembly.FullName, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error finding concrete types for {InterfaceType}", interfaceType.Name);
        }

        return concreteTypes.Distinct().ToList();
    }

    /// <summary>
    /// Clears the cache of resolved types
    /// </summary>
    public static void ClearCache()
    {
        lock (CacheLock)
        {
            InterfaceToConcreteTypes.Clear();
        }
    }
}