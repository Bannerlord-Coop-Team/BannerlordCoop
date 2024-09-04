using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Messages
{
    public record WorkshopCreated : ICommand
    {
        public Workshop Workshop { get; }
        public Settlement Settlement { get; }
        public string Tag { get; }

        public WorkshopCreated(Workshop workshop, Settlement settlement, string tag)
        {
            Workshop = workshop;
            Settlement = settlement;
            Tag = tag;
        }
    }
}
