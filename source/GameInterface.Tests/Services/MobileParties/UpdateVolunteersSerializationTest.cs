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
            var original = new UpdateVolunteers(new Dictionary<uint, uint[]>
            {
                [1] = new uint[] { 3, 0, 4, 0, 0, 0 },
                [2] = new uint[] { 0, 0, 0, 0, 0, 0 },
            });

            var copy = RoundTrip(original);

            Assert.Equal(
                new uint[] { 3, 0, 4, 0, 0, 0 },
                copy.UpdatedVolunteerTypeIds[1]);
            Assert.Equal(
                new uint[] { 0, 0, 0, 0, 0, 0 },
                copy.UpdatedVolunteerTypeIds[2]);
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
