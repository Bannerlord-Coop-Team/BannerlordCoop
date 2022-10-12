using IntroducationServer.Data;
using System;
using Xunit;

namespace IntroductionServerTests
{
    public class ClientInfoTests
    {
        [Fact]
        public void NominalClientInfoStringify()
        {
            ClientInfo clientInfo = new ClientInfo(Guid.NewGuid(), new Version(1,1,1,1), "some_instance");

            string strClientInfo = clientInfo.ToString();

            ClientInfo deserializedClientInfo;
            bool isDeserialized = ClientInfo.TryParse(strClientInfo, out deserializedClientInfo);

            Assert.True(isDeserialized);
            Assert.Equal(clientInfo.ClientId, deserializedClientInfo.ClientId);
            Assert.Equal(clientInfo.ModVersion, deserializedClientInfo.ModVersion);
            Assert.Equal(clientInfo.InstanceName, deserializedClientInfo.InstanceName);
        }

        [Fact]
        public void InvalidClientInfoStringify()
        {
            Assert.Throws<ArgumentNullException>(() => 
            {
                new ClientInfo(Guid.Empty, /* Invalid */
                               new Version(1, 1, 1, 1), 
                               "some_instance");
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                new ClientInfo(Guid.NewGuid(), 
                               null, /* Invalid */
                               "some_instance");
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                new ClientInfo(Guid.NewGuid(), 
                               new Version(1, 1, 1, 1),
                               null /* Invalid */);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                new ClientInfo(Guid.NewGuid(), 
                               new Version(1, 1, 1, 1), 
                               "" /* Invalid */);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                new ClientInfo(Guid.NewGuid(), 
                               new Version(1, 1, 1, 1), 
                               string.Empty /* Invalid */);
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData("%8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%some_instance")]
        [InlineData("%1.1.1.1%some_instance")]
        [InlineData("8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%%some_instance")]
        [InlineData("8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%1.1.1.1%")]
        [InlineData("8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%1.1.1.1%some_instance%")]
        [InlineData("%8e8a09e4-eddf-4c33-a1d8-1e399b763fd4%1.1.1.1%some_instance")]
        public void InvalidClientInfoFromString(string data)
        {
            bool isDeserialized = ClientInfo.TryParse(data, out _);

            Assert.False(isDeserialized);
        }
    }
}
