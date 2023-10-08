using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Kingdoms.Messages
{
    /// <summary>
    /// Request to declare war
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkDeclareWarRequest : ICommand
    {
        [ProtoMember(1)]
        public string Faction1Id { get; }
        [ProtoMember(2)]
        public string Faction2Id { get;}
        [ProtoMember(3)]
        public int Detail { get; }

        public NetworkDeclareWarRequest(string faction1Id, string faction2Id, int detail)
        {
            Faction1Id = faction1Id;
            Faction2Id = faction2Id;
            Detail = detail;
        }
    }
}