using GameInterface.Serialization.Dynamic;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class MBBodyPropertyObjectSerializationTests
    {
        private readonly ITestOutputHelper output;
        public MBBodyPropertyObjectSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NominalMBBodyPropertyObjectSerialization()
        {
            var testModel = MakeMBBodyPropertySerializable();

            MBBodyProperty mbBodyProperty = new MBBodyProperty();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(mbBodyProperty);

            MBBodyProperty newMBBodyProperty = ser.Deserialize<MBBodyProperty>(data);

            Assert.NotNull(newMBBodyProperty);
        }

        [Fact]
        public void NullMBBodyPropertyObjectSerialization()
        {
            var testModel = MakeMBBodyPropertySerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            MBBodyProperty newMBBodyProperty = ser.Deserialize<MBBodyProperty>(data);

            Assert.Null(newMBBodyProperty);
        }

        private RuntimeTypeModel MakeMBBodyPropertySerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<MBBodyProperty>(excluded);

            generator.AssignSurrogate<BodyProperties, SurrogateStub<BodyProperties>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(MBBodyProperty)));

            return testModel;
        }
    }
}
