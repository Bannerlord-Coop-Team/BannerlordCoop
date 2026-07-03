using GameInterface.Services.MobileParties.Messages;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties
{
    public class UpdateVolunteersSerializationTest
    {
        [Fact]
        public void UpdateVolunteers_PreservesEmptySlotsAndIndices()
        {
            var original = new UpdateVolunteers(new Dictionary<string, string[]>
            {
                ["Hero_notable"] = new[] { "recruit_a", "", "recruit_b", "", "", "" },
                ["Hero_empty"] = new[] { "", "", "", "", "", "" },
            });

            var copy = RoundTrip(original);

            Assert.Equal(
                new[] { "recruit_a", "", "recruit_b", "", "", "" },
                copy.UpdatedVolunteerTypeIds["Hero_notable"]);
            Assert.Equal(
                new[] { "", "", "", "", "", "" },
                copy.UpdatedVolunteerTypeIds["Hero_empty"]);
        }

        private static T RoundTrip<T>(T original)
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, original);
            stream.Position = 0;
            return Serializer.Deserialize<T>(stream);
        }
    }
}
