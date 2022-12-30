using Common.Extensions;
using GameInterface.Serialization.Native;
using System;
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
        readonly Dictionary<ObjectAndType, IBinaryPackage> InstantiatedPackages = new Dictionary<ObjectAndType, IBinaryPackage>();
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

            ObjectAndType wrappedObj = new ObjectAndType(type, obj);

            if (InstantiatedPackages.TryGetValue(wrappedObj, out IBinaryPackage serializer))
            {
                return serializer;
            }

            IBinaryPackage package = CreateBinaryPackage(obj);
            Register(wrappedObj, package);

            package.Pack();

            return package;
        }

        private IBinaryPackage CreateBinaryPackage(object obj)
        {
            Type objectType = obj.GetType();

            objectType = objectType.IsGenericType ? objectType.GetGenericTypeDefinition() : objectType;
            objectType = objectType.IsArray ? typeof(Array) : objectType;
            if (PackagesTypes.TryGetValue(objectType, out Type packageType) == false)
            { 
                throw new Exception($"No binary package exists for {objectType}");
            }  

            return (IBinaryPackage)Activator.CreateInstance(packageType, new object[] { obj, this });
        }

        private void Register(ObjectAndType wrappedObj, IBinaryPackage serializer)
        {
            if (InstantiatedPackages.ContainsKey(wrappedObj)) throw new DuplicateKeyException(
                $"{wrappedObj} already has a registered serializer.");

            InstantiatedPackages.Add(wrappedObj, serializer);
        }
    }

    public class ObjectAndType
    {
        public Type Type { get; private set; }
        public object Object { get; private set; }

        public ObjectAndType(object @object)
        {
            Type = @object.GetType();
            Object = @object;
        }

        public ObjectAndType(Type type, object @object)
        {
            Type = type;
            Object = @object;
        }

        public override bool Equals(object obj)
        {
            if (obj is ObjectAndType oat == false) return false;

            if (ReferenceEquals(Object, oat.Object)) return true;

            return Object.Equals(oat.Object) &&
                   Type == oat.Type;
        }

        public override int GetHashCode()
        {
            return new { Object, Type }.GetHashCode();
        }
    }    
}
