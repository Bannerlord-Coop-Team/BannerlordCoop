using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server to change clan name
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkClanNameChangeRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string Name { get; }
        [ProtoMember(3)]
        public string InformalName { get; }

        public NetworkClanNameChangeRequest(string clan, string name, string informalName)
        {
            ClanId = clan;
            Name = name;
            InformalName = informalName;
        }
    }
}