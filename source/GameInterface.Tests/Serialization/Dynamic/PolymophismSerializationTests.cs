using GameInterface.Serialization.Dynamic;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using static TaleWorlds.CampaignSystem.Hero;
using System.Reflection;

namespace GameInterface.Tests.Serialization.Dynamic
{

    public class TestClass : TestBaseClass
    {

    }

    public class TestBaseClass
    {
        protected int j;
        private int i;

        public TestBaseClass()
        {
            i = 1;
        }
    }

    public class PolymophismSerializationTests
    {

        static readonly FieldInfo f_i = typeof(TestBaseClass).GetField("i", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void PrivateBaseMemberSerialization()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<TestBaseClass>().AddDerivedType<TestClass>();
            generator.CreateDynamicSerializer<TestClass>();

            generator.Compile();

            TestClass testClass = new TestClass();

            f_i.SetValue(testClass, 5);

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(testClass);
            TestClass newTestClass = ser.Deserialize<TestClass>(data);

            Assert.Equal(f_i.GetValue(testClass), f_i.GetValue(newTestClass));
        }
    }
}
