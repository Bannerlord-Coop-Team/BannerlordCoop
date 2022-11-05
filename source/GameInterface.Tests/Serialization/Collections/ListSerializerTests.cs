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
    public class ListSerializerTests
    {
        [Fact]
        public void EmptyListTest()
        {
            List<string> myStings = new List<string>();

            ListSerializer<string> preSer = new ListSerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            List<string> newStrings = ser.Deserialize<ListSerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }

        [Fact]
        public void AllNullListTest()
        {
            List<string> myStings = new List<string> { null, null, null, null };

            ListSerializer<string> preSer = new ListSerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            List<string> newStrings = ser.Deserialize<ListSerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }

        [Fact]
        public void SomeNullListTest()
        {
            List<string> myStings = new List<string> { null, "Hi", "Yo", null };

            ListSerializer<string> preSer = new ListSerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            List<string> newStrings = ser.Deserialize<ListSerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }

        [Fact]
        public void NoNullListTest()
        {
            List<string> myStings = new List<string> { "Hi", "Yo" };

            ListSerializer<string> preSer = new ListSerializer<string>();

            preSer.Pack(myStings);

            ProtobufSerializer ser = new ProtobufSerializer();

            byte[] bytes = ser.Serialize(preSer);

            List<string> newStrings = ser.Deserialize<ListSerializer<string>>(bytes).Unpack();

            Assert.Equal(myStings, newStrings);
        }
    }
}
