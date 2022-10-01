using Coop.Serialization;
using GameInterface.Serialization.Collections;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Serialization.Collections
{
    public class ArraySerializerTests
    {
        [Fact]
        public void EmptyArrayTest()
        {
            string[] myStings = new string[0];

            ArraySerializer<string> preSer = new ArraySerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            string[] newStrings = ser.Deserialize<ArraySerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }

        [Fact]
        public void AllNullArrayTest()
        {
            string[] myStings = new string[] { null, null, null, null };

            ArraySerializer<string> preSer = new ArraySerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            string[] newStrings = ser.Deserialize<ArraySerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }

        [Fact]
        public void SomeNullArrayTest()
        {
            string[] myStings = new string[] { null, "Hi", "Yo", null };

            ArraySerializer<string> preSer = new ArraySerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            string[] newStrings = ser.Deserialize<ArraySerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }

        [Fact]
        public void NoNullArrayTest()
        {
            string[] myStings = new string[] { "Hi", "Yo" };

            ArraySerializer<string> preSer = new ArraySerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            string[] newStrings = ser.Deserialize<ArraySerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }

        [Fact]
        public void JaggedArrayTest()
        {
            string[][] myStings = new string[2][];
            myStings[0] = new string[] { "Hi", "Hello" };
            myStings[1] = new string[] { "Yo" };

            // This will be done using manual packing and unpacking
            ArraySerializer<string>[] arraySerializers = new ArraySerializer<string>[myStings.Length];

            for (int i = 0; i < myStings.Length; i++)
            {
                var arrSer = new ArraySerializer<string>();
                arrSer.Pack(myStings[i]);
                arraySerializers[i] = arrSer;
            }

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(arraySerializers);

            List<ArraySerializer<string>> newArraySerializers = ser.Deserialize<List<ArraySerializer<string>>>(bytes);

            string[][] newStrings = new string[newArraySerializers.Count][];

            for (int i = 0; i < newStrings.Length; i++)
            {
                var arrSer = newArraySerializers[i];
                newStrings[i] = arrSer.Unpack();
            }

            Assert.Equal(myStings, newStrings);
        }
    }
}
