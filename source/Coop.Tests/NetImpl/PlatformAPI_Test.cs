using Coop.NetImpl;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.PlatformService;
using TaleWorlds.PlayerServices;
using Xunit;

namespace Coop.Tests.NetImpl
{
    public class PlatformAPI_Test
    {
        private readonly PlatformAPI m_PlatformAPI;
        private readonly Mock<IFriendListService> friendListService;
        private readonly Mock<IPlatformServices> platformServices;

        public PlatformAPI_Test()
        {
            friendListService = new Mock<IFriendListService>();
            platformServices = new Mock<IPlatformServices>();
            m_PlatformAPI = new PlatformAPI(friendListService.Object, platformServices.Object);
        }

        //TODO: This is failing because of a missing reference to GalaxyCSharpGlue, but I'm having issues adding it
        //reason: "please make sure that the file is accessible and that it is a valid assembly or com component"
        [Fact]
        public void GetPlayerID_ForGOG_ReturnsValidPlayerID()
        {
            friendListService.Setup(fls => fls.GetServiceName()).Returns("GOG");
            platformServices.SetupGet(ps => ps.UserId).Returns("46987673193854369");
            PlayerId playerId = m_PlatformAPI.GetPlayerID();

            Assert.True(playerId.IsValid);
        }

        [Fact]
        public void GetPlayerID_ForSteam_ReturnsValidPlayerID()
        {
            friendListService.Setup(fls => fls.GetServiceName()).Returns("Steam");
            platformServices.SetupGet(ps => ps.UserId).Returns("76561198025172135");
            PlayerId playerId = m_PlatformAPI.GetPlayerID();

            Assert.True(playerId.IsValid);
        }

        [Fact]
        public void GetUserID_ReturnsNonEmptyAndNonNull()
        {
            platformServices.SetupGet(ps => ps.UserId).Returns("TestUserID");
            string userId = m_PlatformAPI.GetUserID();

            Assert.False(string.IsNullOrEmpty(userId));
        }

        [Fact]
        public void GetPlayerName_ReturnsNonEmptyAndNonNull()
        {
            platformServices.SetupGet(ps => ps.UserId).Returns("76561198025172135");
            friendListService.Setup(fls => fls.GetUserName(It.IsAny<PlayerId>())).Returns(Task.FromResult("TestUserName"));
            string playerName = m_PlatformAPI.GetPlayerName();

            Assert.False(string.IsNullOrEmpty(playerName));
        }
    }
}
