using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterObjectSerializationTest
    {
        public CharacterObjectSerializationTest()
        {

        }

        public void CharacterObject_Serialize()
        {
            CharacterObject characterObject = new CharacterObject();

            //BinaryFormatterSerializer.Serialize(new CharacterObjectSerializer(characterObject));

            Assert.Fail("To be completed");
        }
    }
}
