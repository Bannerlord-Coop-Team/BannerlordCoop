using Serilog;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.AutoSyncPOC;

public interface IFieldRegistry
{
    IReadOnlyList<FieldInfo> Fields { get; }

    bool TryAddField(FieldInfo field);

    bool TryGetField(int id, out FieldInfo field);
    bool TryGetId(FieldInfo field, out int id);
    bool TryGetSetter<TInstance, TField>(int id, out Action<TInstance, TField> setter);
    bool TryGetSetter<TInstance, TField>(FieldInfo field, out Action<TInstance, TField> setter);
}

public class FieldRegistry : IFieldRegistry
{
    private readonly Dictionary<FieldInfo, int> _fieldIds = new Dictionary<FieldInfo, int>();
    private readonly List<FieldInfo> _fields = new List<FieldInfo>();

    private readonly List<object> _typedSetters = new List<object>();
    private readonly ILogger logger;

    public IReadOnlyList<FieldInfo> Fields => _fields;

    public FieldRegistry(ILogger logger)
    {
        this.logger = logger;
    }

    private static Action<TInstance, TField> CreateSetter<TInstance, TField>(FieldInfo field)
    {
        var instanceParam = Expression.Parameter(typeof(TInstance), "instance");
        var valueParam = Expression.Parameter(typeof(TField), "value");

        var typedInstance = Expression.Convert(instanceParam, field.DeclaringType);
        var fieldExpr = Expression.Field(typedInstance, field);
        var assignExpr = Expression.Assign(fieldExpr, valueParam);

        return Expression
            .Lambda<Action<TInstance, TField>>(assignExpr, instanceParam, valueParam)
            .Compile();
    }

    public bool TryAddField(FieldInfo field)
    {
        if (field is null)
            return false;

        if (field.IsStatic)
            return false;

        if (_fieldIds.ContainsKey(field))
            return false;

        int id = _typedSetters.Count;
        _fieldIds[field] = id;
        _fields.Add(field);

        MethodInfo factory = typeof(FieldRegistry)
            .GetMethod(nameof(CreateSetter), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(field.DeclaringType, field.FieldType);

        var setter = factory.Invoke(null, new object[] { field });
        _typedSetters.Add(setter);

        return true;
    }

    public bool TryGetField(int id, out FieldInfo field)
    {
        field = null;
        if (id < 0 || id >= _fieldIds.Count)
            return false;

        field = _fields[id];
        return true;
    }

    public bool TryGetId(FieldInfo field, out int id)
    {
        id = -1;
        if (field is null)
            return false;

        if (!_fieldIds.ContainsKey(field))
            return false;

        id = _fieldIds[field];
        return true;
    }

    public bool TryGetSetter<TInstance, TField>(FieldInfo field, out Action<TInstance, TField> setter)
    {
        setter = null;
        if (!TryGetId(field, out var id))
            return false;

        return TryGetSetter(id, out setter);
    }
    public bool TryGetSetter<TInstance, TField>(int id, out Action<TInstance, TField> setter)
    {
        setter = null;
        if (id < 0 || id >= _typedSetters.Count)
            return false;

        setter = _typedSetters[id] as Action<TInstance, TField>;

        if (setter == null)
        {
            logger.Error(
                "Failed to cast setter to {setterType}, " +
                "stored setter type {storedSetterType}",
                typeof(Action<TInstance, TField>),
                _typedSetters[id].GetType());
            return false;
        }

        return true;
    }
}
