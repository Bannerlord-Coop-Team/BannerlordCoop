using SharedData;
using System;
using Xunit;

namespace MissionTests
{
    public class ClientInfoTests
    {
        [Fact]
        public void NominalClientInfoStringify()
        {
            ClientInfo clientInfo = new ClientInfo(Guid.NewGuid(), new Version(1,1,1,1));

            string strClientInfo = clientInfo.ToString();

            ClientInfo deserializedClientInfo;
            bool isDeserialized = ClientInfo.TryParse(strClientInfo, out deserializedClientInfo);

            Assert.True(isDeserialized);
            Assert.Equal(clientInfo.ClientId, deserializedClientInfo.ClientId);
            Assert.Equal(clientInfo.ModVersion, deserializedClientInfo.ModVersion);
        }

        [Fact]
        public void InvalidClientInfoStringify()
        {
            Assert.Throws<ArgumentNullException>(() => 
            {
                new ClientInfo(Guid.Empty, /* Invalid */
                               new Version(1, 1, 1, 1));
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                new ClientInfo(Guid.NewGuid(), 
                               null /* Invalid */);
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData("8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%")]
        [InlineData("%1.1.1.1")]
        [InlineData("8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%1.1.1.1%")]
        [InlineData("%8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%1.1.1.1")]
        public void InvalidClientInfoFromString(string data)
        {
            bool isDeserialized = ClientInfo.TryParse(data, out _);

            Assert.False(isDeserialized);
        }
    }
}
