using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    public class Player
    {
        public Guid PlayerId { get; private set; }
        public Hero PlayerHero { get; private set; }
        public NetPeer PlayerPeer { get; private set; }

        public Player(Guid playerId, Hero playerHero, NetPeer playerPeer)
        {
            PlayerId = playerId;
            PlayerHero = playerHero;
            PlayerPeer = playerPeer;
        }
    }
}
