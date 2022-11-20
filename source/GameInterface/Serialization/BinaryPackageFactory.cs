using Common.Extensions;
using GameInterface.Serialization.Native;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;

namespace GameInterface.Serialization
{
    public interface IBinaryPackageFactory
    {
        T GetBinaryPackage<T>(object obj);
        IBinaryPackage GetBinaryPackage(object obj);
    }

    public class BinaryPackageFactory : IBinaryPackageFactory
    {
        readonly Dictionary<object, IBinaryPackage> InstantiatedPackages = new Dictionary<object, IBinaryPackage>();
        static readonly Dictionary<Type, Type> PackagesTypes = new Dictionary<Type, Type>();

        static BinaryPackageFactory()
        {
            CollectBinaryPackageTypes();
        }

        private static void CollectBinaryPackageTypes()
        {
            RegisterEnumerableBinaryPackage();

            foreach (Type type in AppDomain.CurrentDomain.GetDomainTypes())
            {
                if (type.IsClass == false) continue;
                if (type.IsAbstract) continue;
                if (type.BaseType == null) continue;
                if (type.BaseType.IsGenericType == false) continue;

                if (type.BaseType.GetGenericTypeDefinition() == typeof(BinaryPackageBase<>)) 
                    RegisterNormalBinaryPackage(type);
            }
        }

        private static void RegisterNormalBinaryPackage(Type type)
        {
            Type coveredType = type.BaseType.GenericTypeArguments.Single();

            if (PackagesTypes.ContainsKey(coveredType)) throw new Exception(
                $"{coveredType.Name} already has a registered binary package while trying to register {type.Name}.");

            PackagesTypes.Add(coveredType, type);
        }

        private static  void RegisterEnumerableBinaryPackage()
        {
            foreach(var kvp in NativeBinaryPackageCollection.CollectTypes())
            {
                PackagesTypes.Add(kvp.Key, kvp.Value);
            }
        }

        public T GetBinaryPackage<T>(object obj)
        {
            return (T)GetBinaryPackage(obj);
        }

        public IBinaryPackage GetBinaryPackage(object obj)
        {
            if (obj == null) return new NullBinaryPackage();

            Type type = obj.GetType();

            if (type.IsFullySerializable()) return new PrimitiveBinaryPackage(obj);
            //if (obj is IEnumerable) return EnumerableBinaryPackageFactory.GetBinaryPackage(obj);

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) return new KeyValuePairBinaryPackage(obj, this);

            if (InstantiatedPackages.TryGetValue(obj, out IBinaryPackage serializer))
            {
                return serializer;
            }

            IBinaryPackage package = CreateBinaryPackage(obj);
            Register(obj, package);
            return package;
        }

        private IBinaryPackage CreateBinaryPackage(object obj)
        {
            Type objectType = obj.GetType();
            if (PackagesTypes.TryGetValue(objectType, out Type packageType) == false) throw new Exception(
                $"No binary package exists for {objectType}");

            return (IBinaryPackage)Activator.CreateInstance(packageType, new object[] { obj, this });
        }

        private void Register(object obj, IBinaryPackage serializer)
        {
            if (InstantiatedPackages.ContainsKey(obj)) throw new DuplicateKeyException(
                $"{obj} already has a registered serializer.");

            InstantiatedPackages.Add(obj, serializer);
        }
    }
}
