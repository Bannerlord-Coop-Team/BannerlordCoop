using Common.Messaging;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Messages;

/// <summary>
/// The player pressed Done on a party screen and vanilla's done handler accepted it.
/// Published before the screen resets, so the logic still holds the player's edits.
/// </summary>
internal readonly struct PartyScreenCommitAttempted : IEvent
{
    public readonly PartyScreenLogic Logic;
    public readonly Hero MainHero;
    public readonly PartyScreenHelper.PartyScreenMode Mode;
    public readonly bool IsDonating;
    public readonly string ForceTransferId;

    public PartyScreenCommitAttempted(
        PartyScreenLogic logic,
        Hero mainHero,
        PartyScreenHelper.PartyScreenMode mode,
        bool isDonating,
        string forceTransferId)
    {
        Logic = logic;
        MainHero = mainHero;
        Mode = mode;
        IsDonating = isDonating;
        ForceTransferId = forceTransferId;
    }
}
