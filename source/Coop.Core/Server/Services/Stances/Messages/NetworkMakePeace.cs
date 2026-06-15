using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Stances.Messages
{
    /// <summary>
    /// Network message replicating a peace settlement (with daily tribute) between two factions.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkMakePeace : ICommand
    {
        [ProtoMember(1)]
        public string Faction1Id { get; }
        [ProtoMember(2)]
        public string Faction2Id { get; }
        [ProtoMember(3, IsRequired = true)]
        public int DailyTribute { get; }
        [ProtoMember(4, IsRequired = true)]
        public int DailyTributeDuration { get; }
        [ProtoMember(5, IsRequired = true)]
        public int Detail { get; }

        public NetworkMakePeace(string faction1Id, string faction2Id, int dailyTribute, int dailyTributeDuration, int detail)
        {
            Faction1Id = faction1Id;
            Faction2Id = faction2Id;
            DailyTribute = dailyTribute;
            DailyTributeDuration = dailyTributeDuration;
            Detail = detail;
        }
    }
}
