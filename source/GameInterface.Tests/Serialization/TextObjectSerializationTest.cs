using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using Common.Serialization;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TextObjectSerializationTest
    {
        IContainer container;
        public TextObjectSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void TextObject_Serialize()
        {
            TextObject testObject = new TextObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(testObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TextObject_Full_Serialization_NonNested()
        {
            TextObject textObject = new TextObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            TextObject newTextObject = returnedPackage.Unpack<TextObject>(deserializeFactory);

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
        }

        [Fact]
        public void TextObject_Full_Serialization_int()
        {
            int val = 3;
            TextObject textObject = new TextObject(val);

            var factory = container.Resolve<IBinaryPackageFactory>();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            TextObject newTextObject = returnedPackage.Unpack<TextObject>(deserializeFactory);

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
        }

        [Fact]
        public void TextObject_Full_Serialization_float()
        {
            float val = 3;
            TextObject textObject = new TextObject(val);

            var factory = container.Resolve<IBinaryPackageFactory>();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            TextObject newTextObject = returnedPackage.Unpack<TextObject>(deserializeFactory);

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
        }

        [Fact]
        public void TextObject_Full_Serialization_BasicNested()
        {
            string var = "{=12345678}Testing with {INSERT}";
            Dictionary<string, object> dict = new Dictionary<string, object>() { ["INSERT"] = new TextObject("simple nests") };
            TextObject textObject = new TextObject(var, dict);

            var factory = container.Resolve<IBinaryPackageFactory>();
            TextObjectBinaryPackage package = new TextObjectBinaryPackage(textObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TextObjectBinaryPackage>(obj);

            TextObjectBinaryPackage returnedPackage = (TextObjectBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            TextObject newTextObject = returnedPackage.Unpack<TextObject>(deserializeFactory);

            Assert.Equal(textObject.ToString(), newTextObject.ToString());
            Assert.True(textObject.HasSameValue(newTextObject));
        }
    }
}
