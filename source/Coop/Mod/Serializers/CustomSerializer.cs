using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers
{
    public interface ICustomSerializer
    {
        object Deserialize();
    }

    [Serializable]
    public abstract class CustomSerializer : ICustomSerializer
    {
        public Type ObjectType { get; private set; }
        public readonly Dictionary<FieldInfo, object> SerializableObjects = new Dictionary<FieldInfo, object>();
        public readonly List<ICollection> Collections = new List<ICollection>();

        [NonSerialized]
        public readonly List<FieldInfo> NonSerializableObjects = new List<FieldInfo>();
        [NonSerialized]
        public readonly List<ICollection> NonSerializableCollections = new List<ICollection>();

        protected CustomSerializer() { }

        protected CustomSerializer(object obj)
        {
            ObjectType = obj.GetType();
            foreach (FieldInfo field in GetFields())
            {
                if(!field.IsLiteral)
                {
                    if(field.FieldType is ICollection)
                    {
                        Collections.Add((ICollection)field.GetValue(obj));
                    }
                    else if (field.FieldType.IsSerializable)
                    {
                        SerializableObjects.Add(field, field.GetValue(obj));
                    }
                    else
                    {
                        object value = field.GetValue(obj);
                        if (value != null)
                        {
                            NonSerializableObjects.Add(field);
                        }
                    }
                }
            }
        }

        public abstract object Deserialize();

        protected virtual object Deserialize(object newObj)
        {
            foreach (FieldInfo field in SerializableObjects.Keys)
            {
                field.SetValue(newObj, SerializableObjects[field]);
            }
            return newObj;
        }

        protected FieldInfo[] GetFields()
        {
            return ObjectType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        protected PropertyInfo[] GetProperties()
        {
            return ObjectType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
