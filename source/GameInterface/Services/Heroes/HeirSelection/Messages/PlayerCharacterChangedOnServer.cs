using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct PlayerCharacterChangedOnServer : IEvent
{
    public readonly Hero OldPlayerHero;
    public readonly Hero NewPlayerHero;
    public readonly MobileParty NewMainParty;
    public readonly bool IsMainPartyChanged;

    public PlayerCharacterChangedOnServer(Hero oldPlayerHero, Hero newPlayerHero, MobileParty newMainParty, bool isMainPartyChanged)
    {
        OldPlayerHero = oldPlayerHero;
        NewPlayerHero = newPlayerHero;
        NewMainParty = newMainParty;
        IsMainPartyChanged = isMainPartyChanged;
    }
}
