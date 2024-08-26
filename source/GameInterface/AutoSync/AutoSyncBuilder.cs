using GameInterface.AutoSync.Builders;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace GameInterface.AutoSync;
public interface IAutoSyncBuilder
{
    void AddField(FieldInfo field);
    void AddProperty(PropertyInfo property);
    Type Build();
    void SwitchPacket(AutoSyncFieldPacket packet);
}
internal class AutoSyncBuilder : IAutoSyncBuilder, IDisposable
{
    private readonly List<FieldInfo> fields = new List<FieldInfo>();
    private readonly List<PropertyInfo> properties = new List<PropertyInfo>();
    private readonly IObjectManager objectManager;
    private readonly Harmony harmony;
    private readonly List<(MethodInfo, MethodInfo)> patchedMethods = new List<(MethodInfo, MethodInfo)>();

    private ITypeSwitcher PacketSwitcher;

    public AutoSyncBuilder(IObjectManager objectManager, Harmony harmony)
    {
        this.objectManager = objectManager;
        this.harmony = harmony;
    }

    public void AddField(FieldInfo field)
    {
        if (fields.Contains(field)) return;
        fields.Add(field);
    }

    public void AddProperty(PropertyInfo property)
    {
        if (properties.Contains(property)) return;
        properties.Add(property);
    }


    public Type Build()
    {
        UnpatchAll();

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("AutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("AutoSyncAsm");

        AllowPrivateAccess(assemblyBuilder);

        // TODO same thing for properties
        var fieldMap = ConvertFields();

        var types = fieldMap.Keys.ToArray();

        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var transpilerType = CreateTranspiler(moduleBuilder, type, i, fieldMap[type].ToArray());

            var transpilerMethod = transpilerType.Method("Transpiler");

            foreach (var method in AccessTools.GetDeclaredMethods(type))
            {
                harmony.Patch(method, transpiler: new HarmonyMethod(transpilerMethod));
                patchedMethods.Add((method, transpilerMethod));
            }
        }

        var typeSwitchCreator = new TypeSwitchCreator(moduleBuilder, objectManager);

        var typeSwitchType = typeSwitchCreator.Build(fieldMap);

        PacketSwitcher = (ITypeSwitcher)Activator.CreateInstance(typeSwitchType, objectManager);

        return typeSwitchType;
    }

    private Type CreateTranspiler(ModuleBuilder moduleBuilder, Type classType, int typeId, FieldInfo[] fieldsToIntercept)
    {
        var builder = new FieldTranspilerCreator(moduleBuilder, classType, typeId, fieldsToIntercept);

        return builder.Build();
    }

    private void AllowPrivateAccess(AssemblyBuilder assemblyBuilder)
    {
        var assemblies = fields.Select(field => field.DeclaringType.Assembly).Concat(properties.Select(prop => prop.DeclaringType.Assembly)).Distinct();

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

    private void UnpatchAll()
    {
        foreach (var (patchedMethod, transpiler) in patchedMethods)
        {
            harmony.Unpatch(patchedMethod, transpiler);
        }

        patchedMethods.Clear();
    }

    public void Dispose()
    {
        UnpatchAll();
    }

    public void SwitchPacket(AutoSyncFieldPacket packet)
    {
        if (PacketSwitcher == null) return;

        PacketSwitcher.TypeSwitch(packet);
    }
}
