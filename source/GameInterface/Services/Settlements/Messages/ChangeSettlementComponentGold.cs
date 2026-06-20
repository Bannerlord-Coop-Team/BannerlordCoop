using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Let the client know to change <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Gold"/>
    /// </summary>
    public record ChangeSettlementComponentGold : ICommand
    {
        public string SettlementComponentId { get; }
        public int Gold { get; }
        public ChangeSettlementComponentGold(string settlementComponentId, int gold)
        {
            SettlementComponentId = settlementComponentId;
            Gold = gold;
        }
    }
}
