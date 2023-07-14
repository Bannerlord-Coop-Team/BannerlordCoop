using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Clan name change is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkClanNameChangeApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string Name { get; }
        [ProtoMember(3)]
        public string InformalName { get; }

        public NetworkClanNameChangeApproved(string clanId, string name, string informalName)
        {
            ClanId = clanId;
            Name = name;
            InformalName = informalName;
        }
    }
}