using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Messages;

/// <summary>Published locally by <see cref="Patches.RemoveCompanionActionPatches"/>'s prefix when a non-host
/// client's own direct call to <c>RemoveCompanionAction.ApplyInternal</c> is blocked, carrying the
/// clan/companion/detail that call would have applied so <see cref="Handlers.RemoveCompanionHandler"/> can
/// forward it to the server as a <see cref="NetworkCompanionRemovalAttempted"/> request.</summary>
public readonly struct CompanionRemovalAttempted : IEvent
{
    public readonly Clan Clan;
    public readonly Hero Companion;
    public readonly RemoveCompanionAction.RemoveCompanionDetail Detail;

    public CompanionRemovalAttempted(Clan clan, Hero companion, RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        Clan = clan;
        Companion = companion;
        Detail = detail;
    }
}
