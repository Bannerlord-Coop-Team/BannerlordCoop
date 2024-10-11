using GameInterface.AutoSync.Fields;
using GameInterface.AutoSync.Properties;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace GameInterface.AutoSync;
public interface IAutoSyncBuilder : IDisposable
{
    /// <summary>
    /// Add a field to automatically sync across the network
    /// </summary>
    /// <param name="field">Field to auto sync</param>
    void AddField(FieldInfo field);

    /// <summary>
    /// Add a property to automatically sync across the network
    /// </summary>
    /// <param name="property">Property to auto sync</param>
    void AddProperty(PropertyInfo property);

    /// <summary>
    /// Add a field external to the declaring class that updates a public field as those are not synced automatically
    /// </summary>
    /// <param name="methodBase">Method to add as an external setter</param>
    void AddFieldChangeMethod(MethodBase methodBase);

    /// <summary>
    /// Build autosync and dynamic assembly
    /// </summary>
    void Build();

    /// <summary>
    /// Attempt to retreive the intercept for a specified sync field
    /// </summary>
    /// <remarks>
    /// Mainly used for testing
    /// </remarks>
    /// <param name="field">Field to get intercept</param>
    /// <param name="intercept">Out parameter</param>
    /// <returns>True if successful, false if otherwise</returns>
    bool TryGetIntercept(FieldInfo field, out MethodInfo intercept);
}
internal class AutoSyncBuilder : IAutoSyncBuilder
{
    private readonly HashSet<FieldInfo> fields = new HashSet<FieldInfo>();
    private readonly HashSet<PropertyInfo> properties = new HashSet<PropertyInfo>();
    private readonly HashSet<MethodBase> externalFieldChangeMethods = new HashSet<MethodBase>();
    private readonly IObjectManager objectManager;
    private readonly Harmony harmony;
    private readonly IPacketSwitchProvider packetSwitchProvider;
    private readonly IAutoSyncPatchCollector patchCollector;

    private Dictionary<FieldInfo, MethodInfo> interceptMap = new Dictionary<FieldInfo, MethodInfo>();

    public AutoSyncBuilder(IObjectManager objectManager, Harmony harmony, IPacketSwitchProvider packetSwitchProvider, IAutoSyncPatchCollector patchCollector)
    {
        this.objectManager = objectManager;
        this.harmony = harmony;
        this.packetSwitchProvider = packetSwitchProvider;
        this.patchCollector = patchCollector;
    }

    public void AddField(FieldInfo field)
    {
        if (field == null) throw new ArgumentNullException(nameof(field));

        if (fields.Contains(field)) throw new ArgumentException($"{field.Name} has already been registered as a synced field");
        fields.Add(field);
    }

    public void AddProperty(PropertyInfo property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (property.CanWrite == false) throw new ArgumentException($"{property.Name} does not have a set method");

        if (properties.Contains(property)) throw new ArgumentException($"{property.Name} has already been registered as a synced property");
        properties.Add(property);
    }

    public void AddFieldChangeMethod(MethodBase methodBase)
    {
        if (methodBase == null) throw new ArgumentNullException(nameof(methodBase));
        if (externalFieldChangeMethods.Contains(methodBase)) throw new ArgumentException($"{methodBase.Name} has already been registered as an external method");

        externalFieldChangeMethods.Add(methodBase);
    }

    public static int AsmCounter = 1;
    public void Build()
    {
        ClearCollections();

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"AutoSyncAsm_{AsmCounter++}"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("AutoSyncAsm");

        AllowPrivateAccess(assemblyBuilder);

        CreateFieldSync(moduleBuilder);

        CreatePropertySync(moduleBuilder);
    }

    private void CreateFieldSync(ModuleBuilder moduleBuilder)
    {
        var fieldMap = ConvertFields();

        var types = fieldMap.Keys.ToArray();
        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var transpilerType = CreateTranspiler(moduleBuilder, type, i, fieldMap[type].ToArray());

            var transpilerMethod = transpilerType.Method("Transpiler");

            foreach (var method in AccessTools.GetDeclaredMethods(type))
            {
                patchCollector.AddTranspiler(method, transpilerMethod);
            }

            foreach (var method in AccessTools.GetDeclaredConstructors(type))
            {
                patchCollector.AddTranspiler(method, transpilerMethod);
            }

            foreach (var method in externalFieldChangeMethods)
            {
                // This patches all external methods with all transpilers (might be slow if we have a lot)
                patchCollector.AddTranspiler(method, transpilerMethod);
            }
        }

        var typeSwitchCreator = new FieldTypeSwitchCreator(moduleBuilder, objectManager);

        var typeSwitchType = typeSwitchCreator.Build(fieldMap);

        // Set packet switcher
        packetSwitchProvider.FieldSwitch = (IFieldTypeSwitcher)Activator.CreateInstance(typeSwitchType, objectManager);
    }

    private void CreatePropertySync(ModuleBuilder moduleBuilder)
    {
        var propertyMap = ConvertProperties();

        var types = propertyMap.Keys.ToArray();
        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var properties = propertyMap[type].ToArray();
            var patchType = CreatePropertyPrefix(moduleBuilder, type, i, properties);

            foreach (var property in properties)
            {
                var prefix = AccessTools.Method(patchType, $"{property.DeclaringType.Name}_{property.Name}_Prefix");
                var setter = property.GetSetMethod() ?? property.GetSetMethod(true);

                if (prefix == null) throw new NullReferenceException("Prefix was null, likely an issue mapping the name");
                if (setter == null) throw new NullReferenceException("Setter was null, likely no set method exists");

                patchCollector.AddPrefix(setter, prefix);
            }
        }

        var typeSwitchCreator = new PropertyTypeSwitchCreator(moduleBuilder, objectManager);

        var typeSwitchType = typeSwitchCreator.Build(propertyMap);

        // Set packet switcher
        packetSwitchProvider.PropertySwitch = (IPropertyTypeSwitcher)Activator.CreateInstance(typeSwitchType, objectManager);
    }


    private Type CreateTranspiler(ModuleBuilder moduleBuilder, Type classType, int typeId, FieldInfo[] fieldsToIntercept)
    {
        var builder = new FieldTranspilerCreator(objectManager, moduleBuilder, classType, typeId, fieldsToIntercept, interceptMap);

        var compiledType = builder.Build();

        ConvertStoredInterceptsToActual(compiledType);

        return compiledType;
    }

    private Type CreatePropertyPrefix(ModuleBuilder moduleBuilder, Type classType, int typeId, PropertyInfo[] propertiesToIntercept)
    {
        var builder = new PropertyPrefixCreator(objectManager, moduleBuilder, classType, typeId, propertiesToIntercept);

        return builder.Build();
    }

    private void ConvertStoredInterceptsToActual(Type compiledType)
    {
        interceptMap = interceptMap.ToDictionary(kvp => kvp.Key, kvp =>
        {
            var method = kvp.Value;

            if (compiledType.Name.StartsWith(kvp.Key.DeclaringType.Name) == false) return kvp.Value;

            var genericParams = method.IsGenericMethod ? method.GetGenericArguments() : null;
            var actualMethod = AccessTools.Method(compiledType, method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray(), genericParams);

            if (actualMethod == null)
            {
                throw new NullReferenceException($"Failed to get {method.Name} from compiled class");
            } 
            
            return actualMethod;
        });
    }

    private void AllowPrivateAccess(AssemblyBuilder assemblyBuilder)
    {
        var assemblies = fields.Select(field => field.DeclaringType.Assembly).Concat(properties?.Select(prop => prop.DeclaringType.Assembly)).Distinct();

        // Allow access from dynamic assembly to private types
        foreach (var assembly in assemblies)
        {
            CustomAttributeBuilder myCABuilder = new CustomAttributeBuilder(
                AccessTools.Constructor(typeof(IgnoresAccessChecksToAttribute), new Type[] { typeof(string) }),
                new object[] { assembly.GetName().Name });
            assemblyBuilder.SetCustomAttribute(myCABuilder);
        }
    }

    private Dictionary<Type, List<FieldInfo>> ConvertFields()
    {
        var fieldMap = new Dictionary<Type, List<FieldInfo>>();
        foreach (var field in fields)
        {
            if (fieldMap.ContainsKey(field.DeclaringType) == false)
                fieldMap.Add(field.DeclaringType, new List<FieldInfo>());

            fieldMap[field.DeclaringType].Add(field);
        }

        return fieldMap;
    }

    private Dictionary<Type, List<PropertyInfo>> ConvertProperties()
    {
        var propertyMap = new Dictionary<Type, List<PropertyInfo>>();
        foreach (var property in properties)
        {
            if (propertyMap.ContainsKey(property.DeclaringType) == false)
                propertyMap.Add(property.DeclaringType, new List<PropertyInfo>());

            propertyMap[property.DeclaringType].Add(property);
        }

        return propertyMap;
    }

    private void ClearCollections()
    {
        interceptMap.Clear();
    }

    public void Dispose()
    {
        ClearCollections();
    }

    /// <inheritdoc/>
    public bool TryGetIntercept(FieldInfo field, out MethodInfo intercept) => interceptMap.TryGetValue(field, out intercept);
}
