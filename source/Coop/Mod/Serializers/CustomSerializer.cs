using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MBMultiplayerCampaign.Serializers
{
    public interface ICustomSerializer
    {
        ICustomSerializer Serialize(object obj);

        object Deserialize();
    }

    [Serializable]
    public abstract class CustomSerializer : ICustomSerializer
    {
        public Type ObjectType { get; private set; }
        public readonly List<Tuple<FieldInfo, object>> SerializableObjects = new List<Tuple<FieldInfo, object>>();
        public readonly List<Tuple<FieldInfo, object>> NonSerializableObjects = new List<Tuple<FieldInfo, object>>();
        public readonly List<ICollection> Collections = new List<ICollection>();

        protected CustomSerializer() { }

        protected CustomSerializer(object obj)
        {
            ObjectType = obj.GetType();
            foreach (FieldInfo field in GetFields())
            {
                if(!field.IsLiteral)
                {
                    if(field is ICollection)
                    {
                        Collections.Add((ICollection)field.GetValue(obj));
                    }
                    else if (field.FieldType.IsSerializable)
                    {
                        SerializableObjects.Add(new Tuple<FieldInfo, object>(field, field.GetValue(obj)));
                    }
                    else
                    {
                        NonSerializableObjects.Add(new Tuple<FieldInfo, object>(field, field.GetValue(obj)));
                    }
                }
            }
        }

        public abstract object Deserialize();

        public abstract ICustomSerializer Serialize(object obj);

        protected FieldInfo[] GetFields()
        {
            return ObjectType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
