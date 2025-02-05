using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkChangeClanName : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string Name { get; }
        [ProtoMember(3)]
        public string InformalName { get; }

        public NetworkChangeClanName(string clanId, string name, string informalName)
        {
            ClanId = clanId;
            Name = name;
            InformalName = informalName;
        }
    }
}
