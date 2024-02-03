using Common.Messaging;

namespace GameInterface.Services.Armies.Messages
{
    public record DisbandArmy : ICommand
    {
        public string ArmyId { get; }
        public string Reason { get; }

        public DisbandArmy(string armyId, string reason)
        {
            ArmyId = armyId;
            Reason = reason;
        }

    }
}
