using Common.Messaging;

namespace GameInterface.Services.Armies.Messages
{
    public record ArmyDisbanded : ICommand
    {
        public string ArmyId { get; }
        public string Reason { get; }

        public ArmyDisbanded(string armyId, string reason)
        {
            ArmyId = armyId;
            Reason = reason;
        }

    }
}
