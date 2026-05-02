using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.ItemRosters.Messages;

[BatchLogMessage]
public readonly struct ItemRosterCleared : ICommand
{
    public readonly PartyBase PartyBase;

    public ItemRosterCleared(PartyBase partyBase)
    {
        PartyBase = partyBase;
    }
}