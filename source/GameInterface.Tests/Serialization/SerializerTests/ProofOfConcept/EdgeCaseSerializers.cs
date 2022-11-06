using GameInterface.Serializers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests.Serialization.SerializerTests.ProofOfConcept
{

    [Serializable]
    public class BasicClassBinaryPackage
    {

    }

    public interface ISerializer
    {
        void Pack();
    }

    public class SerializerStore
    {
        static Dictionary<object, ISerializer> Serializers = new Dictionary<object, ISerializer>();

        public static T GetSerializer<T>(object obj)
        {
            return (T)GetSerializer(obj);
        }

        public static ISerializer GetSerializer(object obj)
        {
            if(Serializers.TryGetValue(obj, out ISerializer serializer))
            {
                return serializer;
            }

            ISerializer ser = CreateSerializer(obj);

            Serializers.Add(obj, ser);

            return ser;
        }

        public static void Register(object obj, ISerializer serializer)
        {
            Serializers.Add(obj, serializer);
        }

        private static ISerializer CreateSerializer(object obj)
        {
            if (obj is TestClassA classA)
            {
                return new ClassABinaryPackage(classA);
            }
            else if (obj is TestClassB classB)
            {
                return new ClassBBinaryPackage(classB);
            }
            else
            {
                return null;
            }
        }
    }

    public class ReferenceStore
    {
        private static readonly Dictionary<Guid, object> GuidToObj = new Dictionary<Guid, object>();
        private static readonly Dictionary<object, Guid> ObjToGuid = new Dictionary<object, Guid>();

        private static void AddObject(Guid id, object obj)
        {
            GuidToObj.Add(id, obj);
            ObjToGuid.Add(obj, id);
        }

        private static void RemoveId(Guid id)
        {
            object obj = GuidToObj[id];
            ObjToGuid.Remove(obj);
            GuidToObj.Remove(id);
        }

        private static void RemoveObj(object obj)
        {
            Guid id = ObjToGuid[obj];
            ObjToGuid.Remove(obj);
            GuidToObj.Remove(id);
        }

        public static Guid AddObject(object obj)
        {
            if(ObjToGuid.TryGetValue(obj, out Guid id))
            {
                return id;
            }

            Guid newId = Guid.NewGuid();

            AddObject(id, obj);

            return newId;
        }

        public static void RegisterObject(Guid id, object obj)
        {
            AddObject(id, obj);
        }

        public static bool TryGetObj(Guid id, out object obj)
        {
            return GuidToObj.TryGetValue(id, out obj);
        }

        public static bool TryGetGuid(object obj, out Guid id)
        {
            return ObjToGuid.TryGetValue(obj, out id);
        }

        public static bool ContainsObj(object obj)
        {
            return ObjToGuid.ContainsKey(obj);
        }

        public static bool ContainsId(Guid id)
        {
            return GuidToObj.ContainsKey(id);
        }
    }

    [Serializable]
    internal class ClassABinaryPackage : ISerializer
    {
        [NonSerialized]
        TestClassA Object;

        [NonSerialized]
        private static readonly Type ObjectType = typeof(TestClassA);

        [NonSerialized]
        private bool IsPacked = false;

        ClassBBinaryPackage classBPackage;

        public ClassABinaryPackage(TestClassA classA)
        {
            Object = classA;
        }

        public void Pack()
        {
            if (IsPacked == false)
            {
                IsPacked = true;
                classBPackage = SerializerStore.GetSerializer<ClassBBinaryPackage>(Object.testClassB);
                classBPackage.Pack();
            }
        }

        public TestClassA Deserialize()
        {
            if (Object != null) return Object;

            Object = (TestClassA)FormatterServices.GetUninitializedObject(ObjectType);

            Object.testClassB = classBPackage.Deserialize();

            return Object;
        }

        
    }

    [Serializable]
    internal class ClassBBinaryPackage : ISerializer
    {
        [NonSerialized]
        TestClassB Object;

        [NonSerialized]
        private static readonly Type ObjectType = typeof(TestClassB);

        [NonSerialized]
        private bool IsPacked = false;

        ClassABinaryPackage classAPackage;
        public ClassBBinaryPackage(TestClassB classB)
        {
            Object = classB;
        }

        public void Pack()
        {
            if(IsPacked == false)
            {
                IsPacked = true;
                classAPackage = SerializerStore.GetSerializer<ClassABinaryPackage>(Object.testClassA);
                classAPackage.Pack();
            }
        }

        public TestClassB Deserialize()
        {
            if (Object != null) return Object;

            Object = (TestClassB)FormatterServices.GetUninitializedObject(ObjectType);

            Object.testClassA = classAPackage.Deserialize();

            return Object;
        }
    }
}
