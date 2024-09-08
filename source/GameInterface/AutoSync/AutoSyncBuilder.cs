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
    void AddField(FieldInfo field);
    void AddProperty(PropertyInfo property);
    void Build();
    bool TryGetIntercept(FieldInfo field, out MethodInfo intercept);
}
internal class AutoSyncBuilder : IAutoSyncBuilder
{
    private readonly List<FieldInfo> fields = new List<FieldInfo>();
    private readonly List<PropertyInfo> properties = new List<PropertyInfo>();
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

        if (fields.Contains(field)) return;
        fields.Add(field);
    }

    public void AddProperty(PropertyInfo property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));

        if (properties.Contains(property)) return;
        properties.Add(property);
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
        }

        var typeSwitchCreator = new FieldTypeSwitchCreator(moduleBuilder, objectManager);

        var typeSwitchType = typeSwitchCreator.Build(fieldMap);

        // Set packet switcher
        packetSwitchProvider.FieldSwitch = (IFieldTypeSwitcher)Activator.CreateInstance(typeSwitchType, objectManager);
    }

    private void CreatePropertySync(ModuleBuilder moduleBuilder)
    {
        // TODO finish
        var propertyMap = ConvertProperties();

        var types = propertyMap.Keys.ToArray();
        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var prefixType = CreatePropertyPrefix(moduleBuilder, type, i, propertyMap[type].ToArray());

            var transpilerMethod = prefixType.Method("Transpiler");

            foreach (var method in AccessTools.GetDeclaredMethods(type))
            {
                patchCollector.AddTranspiler(method, transpilerMethod);
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
            var genericParams = method.IsGenericMethod ? method.GetGenericArguments() : null;
            var actualMethod = AccessTools.Method(compiledType, method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray(), genericParams);

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

    public bool TryGetIntercept(FieldInfo field, out MethodInfo intercept) => interceptMap.TryGetValue(field, out intercept);
}
