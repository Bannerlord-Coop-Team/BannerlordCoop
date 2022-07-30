using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Serialization
{
    public class DummyDataClass
    {
        public int MyInt;
        public int MyInt2;
        public string MyString;
        public string MyStringProp { get; set; }
    }

    public static class TestModelFactory
    {
        public static void CreateModel(Type T)
        {
            var protobufModel = RuntimeTypeModel.Default.Add(T, false);

            int counter = 1;
            foreach (FieldInfo field in T.GetFields())
            {
                protobufModel.AddField(counter++, field.Name);
            }

            foreach (PropertyInfo prop in T.GetProperties())
            {
                protobufModel.AddField(counter++, prop.Name);
            }
        }
    }

    public class ProtobufDynamicModel_Test
    {
        [Fact]
        public void DynamicCreate_Test()
        {
            TestModelFactory.CreateModel(typeof(DummyDataClass));

            DummyDataClass preSerialize = new DummyDataClass()
            {
                MyInt = 1,
                MyInt2 = 2,
                MyString = "Hi",
                MyStringProp = "This is a property",
            };

            DummyDataClass postSerialize;


            string proto = Serializer.GetProto<DummyDataClass>();

            byte[] data;

            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, preSerialize);
                data = ms.ToArray();
                
            }

            using (var ms = new MemoryStream(data))
            {
                postSerialize = Serializer.Deserialize<DummyDataClass>(ms);
            }

            Assert.Equal(preSerialize.MyInt, postSerialize.MyInt);
            Assert.Equal(preSerialize.MyInt2, postSerialize.MyInt2);
            Assert.Equal(preSerialize.MyString, postSerialize.MyString);
            Assert.False(Object.ReferenceEquals(preSerialize, postSerialize));
        }
    }
}
