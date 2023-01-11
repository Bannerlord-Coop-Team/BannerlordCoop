using GameInterface.Serialization.Dynamic;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

#if false
namespace GameInterface.Tests.Serialization.Dynamic
{
    public class TemplateSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        public TemplateSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        [Fact]
        public void NominalTemplateObjectSerialization()
        {
            var testModel = MakeTemplateSerializable();

            // TODO change name
            Template template = new Template();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            // TODO change name
            byte[] data = ser.Serialize(template);

            // TODO change name
            Template newTemplate = ser.Deserialize<Template>(data);

            // TODO change name
            Assert.NotNull(newTemplate);
        }

        [Fact]
        public void NullTemplateObjectSerialization()
        {
            var testModel = MakeTemplateSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            // TODO change name
            Template newTemplate = ser.Deserialize<Template>(data);

            Assert.Null(newTemplate);
        }

        private RuntimeTypeModel MakeTemplateSerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            // TODO implement
            generator.CreateDynamicSerializer<Template>(excluded);

            generator.Compile();

            // Verify the type ItemObject can be serialized
            // TODO change name
            Assert.True(testModel.CanSerialize(typeof(Template)));

            return testModel;
        }
    }
}
#endif