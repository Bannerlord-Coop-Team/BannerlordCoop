using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native;

[Serializable]
public class TupleBinaryPackage : IBinaryPackage
{
    private static readonly Type[] AllowedDefinitions =
    {
        typeof(Tuple<>),
        typeof(Tuple<,>),
        typeof(Tuple<,,>),
        typeof(Tuple<,,,>),
        typeof(Tuple<,,,,>),
        typeof(Tuple<,,,,,>),
        typeof(Tuple<,,,,,,>),
        typeof(Tuple<,,,,,,,>),
    };

    [NonSerialized]
    private IBinaryPackageFactory binaryPackageFactory;

    [NonSerialized]
    private object tuple;

    private string tupleType;
    private IBinaryPackage[] items;

    public TupleBinaryPackage(object tuple, IBinaryPackageFactory binaryPackageFactory)
    {
        this.tuple = tuple;
        this.binaryPackageFactory = binaryPackageFactory;
        tupleType = tuple.GetType().AssemblyQualifiedName;
        ResolveTupleType();
    }

    public void Pack()
    {
        Type type = ResolveTupleType();
        int itemCount = type.GetGenericArguments().Length;
        items = new IBinaryPackage[itemCount];

        for (int index = 0; index < itemCount; index++)
        {
            string propertyName = index == 7 ? "Rest" : $"Item{index + 1}";
            PropertyInfo property = type.GetProperty(propertyName);
            if (property == null)
                throw new SerializationException($"Tuple property {propertyName} was missing on {type}");

            items[index] = binaryPackageFactory.GetBinaryPackage(property.GetValue(tuple));
        }
    }

    public object Unpack(IBinaryPackageFactory binaryPackageFactory)
    {
        this.binaryPackageFactory = binaryPackageFactory;
        Type type = ResolveTupleType();
        if (items == null || items.Length != type.GetGenericArguments().Length)
            throw new SerializationException($"Tuple item count was invalid for {type}");

        object[] unpacked = items.Select(item => item.Unpack(binaryPackageFactory)).ToArray();
        return Activator.CreateInstance(type, unpacked);
    }

    public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
    {
        return (T)Unpack(binaryPackageFactory);
    }

    private Type ResolveTupleType()
    {
        return SerializedTypeResolver.ResolveGenericType(tupleType, AllowedDefinitions);
    }
}
