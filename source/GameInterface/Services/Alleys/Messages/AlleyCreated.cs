using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Alleys.Messages;
internal record AlleyCreated(Alley Instance, Settlement Settlement, string Tag, string Name) : IEvent
{
    public Alley Instance { get; } = Instance;
    public Settlement Settlement { get; } = Settlement;
    public string Tag { get; } = Tag;
    public string Name { get; } = Name;
}
