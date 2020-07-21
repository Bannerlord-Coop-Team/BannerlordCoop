using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Xunit;

namespace Coop.Tests.Network
{
    enum ConnectionStates
    {
        Disconnected,
        Connected
    }

    enum GameStates
    {
        MainMenu,
        Playing
    }

    public class State_Test
    {
        [Fact]
        private void EnumGenerics()
        {
            Enum Connected = ConnectionStates.Connected;
            Enum Disconnected = ConnectionStates.Disconnected;
            Enum Playing = GameStates.Playing;
            bool has = Connected.HasFlag(ConnectionStates.Disconnected);

            Assert.NotEqual(Connected, Disconnected);

            Assert.Equal((int)ConnectionStates.Connected, (int)GameStates.Playing);
            Assert.NotEqual(Playing, Disconnected);

            Type csType = Connected.GetType();
            Type csType2 = Connected.GetType();
            
            Assert.Equal(csType, csType2);
            Assert.True(has);

            Type gsType = GameStates.MainMenu.GetType();
            Assert.NotEqual(gsType, csType);
        }
    }
}
