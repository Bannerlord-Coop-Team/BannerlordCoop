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

        //TODO: This can't be tested because of a missing reference to GalaxyCSharpGlue.dll. Can uncomment once that reference is added
        //[Fact]
        //public void GetPlayerID_ForGOG_ReturnsValidPlayerID()
        //{
        //    friendListService.Setup(fls => fls.GetServiceName()).Returns("GOG");
        //    platformServices.SetupGet(ps => ps.UserId).Returns("46987673193854369");
        //    PlayerId playerId = m_PlatformAPI.GetPlayerID();

        //    Assert.True(playerId.IsValid);
        //}


        [Fact]
        public void GetPlayerID_ForSteam_ReturnsValidPlayerID()
        {
            friendListService.Setup(fls => fls.GetServiceName()).Returns("Steam");
            platformServices.SetupGet(ps => ps.UserId).Returns("76561198025172135");
            PlayerId playerId = m_PlatformAPI.GetPlayerID();

            Assert.True(playerId.IsValid);
        }

        //TODO: This can't be tested because of a missing reference to EOSSDK.dll. Can uncomment once that reference is added
        //[Fact]
        //public void GetPlayerID_ForEpic_ReturnsValidPlayerID()
        //{
        //    friendListService.Setup(fls => fls.GetServiceName()).Returns("Epic");
        //    platformServices.SetupGet(ps => ps.UserId).Returns("9668a87b73484b889f4902ca89365f1f");
        //    PlayerId playerId = m_PlatformAPI.GetPlayerID();

        //    Assert.True(playerId.IsValid);
        //}

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
