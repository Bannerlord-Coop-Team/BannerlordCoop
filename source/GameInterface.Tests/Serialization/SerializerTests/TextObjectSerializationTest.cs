using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Collections.Generic;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TextObjectSerializationTest
    {
        [Fact]
        public void TextObject_Serialize()
        {
            TextObject testObject = new TextObject("Test");

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(testObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TextObject_Full_Serialization_NonNested()
        {
            TextObject textObject = new TextObject("Test");

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            TextObject newTextObject = returnedPackage.Unpack<TextObject>();

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
        }

        [Fact]
        public void TextObject_Full_Serialization_int()
        {
            int val = 3;
            TextObject textObject = new TextObject(val);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            TextObject newTextObject = returnedPackage.Unpack<TextObject>();

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
        }

        [Fact]
        public void TextObject_Full_Serialization_float()
        {
            float val = 3;
            TextObject textObject = new TextObject(val);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            TextObject newTextObject = returnedPackage.Unpack<TextObject>();

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
        }

        [Fact]
        public void TextObject_Full_Serialization_BasicNested()
        {
            string var = "{=12345678}Testing with {INSERT}";
            Dictionary<string, object> dict = new Dictionary<string, object>() { ["INSERT"] = new TextObject("simple nests") };
            TextObject textObject = new TextObject(var, dict);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            TextObject newTextObject = returnedPackage.Unpack<TextObject>();

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
            Assert.Equal("Testing with simple nests", newTextObject.ToString());
        }
    }
}
