using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace MBMultiplayerCampaign.Serializers
{
    class MBSerializer
    {
        public void test()
        {
            Game.Current.ObjectManager.GetObject<CharacterObject>(character => character.Id == Game.Current.PlayerTroop.Id);
                // if Id exists use replace GUID with obj
                // if not add object
                // cleanup circular refrences
        }

        public static SerializableObject Serialize(MBObjectBase obj)
        {
            return new SerializableObject(obj);
        }

        public static MBObjectBase DeserializeObject(SerializableObject obj, Func<string, SerializableObject> missingGUIDCallback = null)
        {
            return obj.Deserialize(missingGUIDCallback);
        }
    }

    

    [Serializable]
    public class SerializableObject
    {
        public static readonly Dictionary<Type, ICustomSerializer> CustomSerializers = new Dictionary<Type, ICustomSerializer>();
        private static bool customSerializersRegistered = false;

        public void RegisterSerializers()
        {
            CustomSerializers.Add(typeof(Equipment), new EquipmentSerializer());
            CustomSerializers.Add(typeof(EquipmentElement), new EquipmentElementSerializer());
            CustomSerializers.Add(typeof(Hero), new HeroSerializer());
            //CustomSerializers.Add(typeof(CharacterObject), new CharacterObjectSerializer());
            CustomSerializers.Add(typeof(CampaignTime), new CampaignTimeSerializer());

            customSerializersRegistered = true;
        }

        public string ObjectType { get; private set; }
        public readonly List<Tuple<FieldInfo, object>> SerializableObjects = new List<Tuple<FieldInfo, object>>();
        public readonly List<Tuple<FieldInfo, string>> NonSerializableObjects = new List<Tuple<FieldInfo, string>>();
        public readonly List<ICollection> NonSerializableCollections = new List<ICollection>();
        public SerializableObject(object obj)
        {

            if (!customSerializersRegistered)
            {
                RegisterSerializers();
            }

            ObjectType = obj.GetType().Name;

            FieldInfo[] fieldInfos = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (FieldInfo field in fieldInfos)
            {
                if (field.FieldType.IsSerializable && !field.IsLiteral && !(field.GetValue(obj) is ICollection))
                {
                    SerializableObjects.Add(new Tuple<FieldInfo, object>(field, field.GetValue(obj)));
                }
                else if(!field.IsLiteral)
                {
                    object value = field.GetValue(obj);
                    if (value is IList<MBObjectBase>)
                    {
                        NonSerializableCollections.Add(SerializeCollection(value as IList<MBObjectBase>));
                    }
                    else if (value is ICollection)
                    {
                        if ((value as ICollection).Count == 0)
                        {
                            continue;
                        }
                        else if (value.GetType().GetElementType().IsSerializable)
                        {
                            SerializableObjects.Add(new Tuple<FieldInfo, object>(field, field.GetValue(value)));
                        }

                        if (value.GetType().GetElementType().GetInterface("ISerializableObject") != null)
                        {
                            NonSerializableCollections.Add(SerializeCollection(value as ICollection<ISerializableObject>));
                        }
                        else
                        {
                            throw new Exception(value.GetType().GetElementType().ToString());
                        }
                    }
                    else if (CustomSerializers.ContainsKey(field.FieldType))
                    {
                        ICustomSerializer serializer = CustomSerializers[field.FieldType].Serialize(value);
                        SerializableObjects.Add(new Tuple<FieldInfo, object>(field, serializer));
                    }
                    else if (value is MBObjectBase)
                    {
                        NonSerializableObjects.Add(new Tuple<FieldInfo, string>(field, (value as MBObjectBase).StringId));
                    }
                    else if (value == null) { }
                    else
                    {
                        throw new Exception(value.ToString());
                    }
                }
            }
        }

        public MBObjectBase Deserialize(Func<string, SerializableObject> missingGUIDCallback)
        {
            Type type = Assembly.GetExecutingAssembly().GetType(ObjectType);
            MBObjectBase obj = Activator.CreateInstance(type) as MBObjectBase;
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach(Tuple<FieldInfo, object> tuple in SerializableObjects)
            {
                tuple.Item1.SetValue(obj, tuple.Item2);
            }

            foreach(Tuple<FieldInfo, string> tuple in NonSerializableObjects)
            {
                try
                {
                    object existingObj = typeof(MBObjectManager)
                        .GetMethod("GetObject", new[] { typeof(string) })
                        .MakeGenericMethod(tuple.Item1.FieldType)
                        .Invoke(Game.Current.ObjectManager, new object[] { tuple.Item2 });
                    tuple.Item1.SetValue(obj, existingObj);
                }
                catch (MBTypeNotRegisteredException)
                {
                    object newObj = missingGUIDCallback?.Invoke(tuple.Item2).Deserialize(missingGUIDCallback);
                    tuple.Item1.SetValue(obj, newObj);
                    //RegisterObject(newObj);
                }
            }

            //RegisterObject(obj);

            return obj;
        }

        private void RegisterObject(object obj)
        {
            MethodInfo method = typeof(MBObjectManager)
                .GetMethod("RegisterObject");
            method = method.MakeGenericMethod(obj.GetType());
            method.Invoke(MBObjectManager.Instance, new object[] { obj });
        }

        //private List<T> SerializeCollection<T>(T item)
        //{
        //    List<T> collectionAsGUID = new List<T>();
        //    foreach (IList<T> item in collection)
        //    {
        //        collectionAsGUID.Add(SerializeCollection<IList<T>>(item));
        //    }
        //    return collectionAsGUID;
        //}

        public List<MBGUID> SerializeCollection(IList<MBObjectBase> collection)
        {
            List<MBGUID> collectionAsGUID = new List<MBGUID>();
            foreach (MBObjectBase item in collection)
            {
                if (item is MBObjectBase)
                {
                    collectionAsGUID.Add(item.Id);
                }
                else if (item == null) { }
                else
                {
                    throw new Exception(item.ToString());
                }
            }
            return collectionAsGUID;
        }


        public List<string> SerializeCollection(ICollection<ISerializableObject> collection)
        {
            if(collection == null) { return new List<string>(); }
            List<string> collectionAsString = new List<string>();
            foreach (ISerializableObject item in collection)
            {
                StringWriter stringWriter = new StringWriter();
                item.SerializeTo(stringWriter);
                collectionAsString.Add(stringWriter.Data);
            }
            return collectionAsString;
        }
    }
}
