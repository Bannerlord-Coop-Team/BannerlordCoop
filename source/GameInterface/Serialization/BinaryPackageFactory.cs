using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;

namespace GameInterface.Serialization
{
    public class BinaryPackageFactory
    {
        readonly static Dictionary<object, IBinaryPackage> InstantiatedPackages = new Dictionary<object, IBinaryPackage>();
        readonly static Dictionary<Type, Type> PackagesTypes = new Dictionary<Type, Type>();


        static BinaryPackageFactory()
        {
            CollectBinaryPackageTypes();
        }

        private static void CollectBinaryPackageTypes()
        {
            foreach (Type type in AppDomain.CurrentDomain.GetDomainTypes())
            {
                if (type.IsClass == false) continue;
                if (type.IsAbstract) continue;
                if (type.BaseType == null) continue;
                if (type.BaseType.IsGenericType == false) continue;
                if (type.BaseType.GetGenericTypeDefinition() != typeof(BinaryPackageBase<>)) continue;

                PackagesTypes.Add(type.BaseType.GenericTypeArguments.Single(), type);
            }
        }

        public T GetBinaryPackage<T>(object obj)
        {
            return (T)GetBinaryPackage(obj);
        }

        public IBinaryPackage GetBinaryPackage(object obj)
        {
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

        public void Register(object obj, IBinaryPackage serializer)
        {
            if (InstantiatedPackages.ContainsKey(obj)) throw new DuplicateKeyException(
                $"{obj} already has a registered serializer.");

            InstantiatedPackages.Add(obj, serializer);
        }
    }
}
