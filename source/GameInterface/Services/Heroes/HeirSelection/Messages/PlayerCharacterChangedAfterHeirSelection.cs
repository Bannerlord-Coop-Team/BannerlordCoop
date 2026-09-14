using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct PlayerCharacterChangedAfterHeirSelection : IEvent
{
    public readonly Hero OldPlayer;
    public readonly Hero NewPlayer;
    public readonly MobileParty NewMainParty;
    public readonly bool IsMainPartyChanged;

    public PlayerCharacterChangedAfterHeirSelection(
        Hero oldPlayer,
        Hero newPlayer,
        MobileParty newMainParty,
        bool isMainPartyChanged)
    {
        OldPlayer = oldPlayer;
        NewPlayer = newPlayer;
        NewMainParty = newMainParty;
        IsMainPartyChanged = isMainPartyChanged;
    }
}
