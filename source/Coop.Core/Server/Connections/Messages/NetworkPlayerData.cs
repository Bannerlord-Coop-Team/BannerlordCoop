using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages
{
    [ProtoContract]
    public readonly struct NetworkPlayerData : INetworkEvent
    {
        public NetworkPlayerData(NewPlayerHeroRegistered registrationData)
        {
            HeroStringId = registrationData.HeroStringId;
            PartyStringId = registrationData.PartyStringId;
            CharacterObjectStringId = registrationData.CharacterObjectStringId;
            ClanStringId = registrationData.ClanStringId;
        }

        [ProtoMember(1)]
        public string HeroStringId { get; }
        [ProtoMember(2)]
        public string PartyStringId { get; }
        [ProtoMember(3)]
        public string CharacterObjectStringId { get; }
        [ProtoMember(4)]
        public string ClanStringId { get; }
    }
}
