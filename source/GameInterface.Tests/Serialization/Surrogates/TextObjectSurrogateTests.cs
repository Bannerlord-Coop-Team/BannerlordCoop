using Coop.Serialization;
using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Serialization.Surrogates
{
    public class TextObjectSurrogateTests
    {
        [Fact]
        public void NominalTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(TextObject)));

            TextObject textObject = new TextObject("Test");

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(textObject);
            TextObject newTextObject = ser.Deserialize<TextObject>(data);

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
        }

        [Fact]
        public void NullTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(TextObject)));

            // Set to null
            TextObject textObject = null;

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(textObject);
            TextObject newTextObject = ser.Deserialize<TextObject>(data);

            Assert.Null(newTextObject);
        }
    }
}
