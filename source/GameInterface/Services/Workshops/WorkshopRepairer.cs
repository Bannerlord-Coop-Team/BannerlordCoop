using Common;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Handlers;
using GameInterface.Services.Workshops.Interfaces;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops;

/// <summary>
/// Repairs saves poisoned by issue #2373: while MaximumWorkshopsPlayerCanHave returned the vanilla
/// value, WorkshopsCampaignBehavior's fixed-size storage capped out and the native
/// AddNewWorkshopData silently dropped WorkshopData for player-owned workshops. Those saves keep
/// player ownership without WorkshopData, which black-screens the owner's Clan tab
/// (GetOutputDailyChange dereferences the missing data) and faults the server's production tick.
/// </summary>
/// <remarks>
/// Temporary; remove this class once poisoned saves are no longer in circulation. Its only call
/// sites are the two hooks in <see cref="WorkshopsCampaignBehaviorInitializationHandler"/>.
/// </remarks>
internal static class WorkshopRepairer
{
    /// <summary>
    /// Backfills the local behavior storage for every workshop the player hero owns. Runs blocking
    /// so the player cannot open the Clan screen before their own workshops have data again.
    /// </summary>
    internal static void RepairClientWorkshopData(WorkshopsCampaignBehavior workshopsCampaignBehavior, Hero playerHero)
    {
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                workshopsCampaignBehavior.EnsureBehaviorDataSize();
                AddMissingWorkshopData(workshopsCampaignBehavior, WorkshopsCampaignBehaviorInitializationHandler.GetWorkshopsOwnedBy(playerHero));
            }
        }, blocking: true);
    }

    /// <summary>
    /// Backfills the server behavior storage and the session-store warehouse entries for every
    /// workshop the player hero owns. The AddNewWorkshopData server prefix broadcasts each
    /// restored entry to every client.
    /// </summary>
    internal static void RepairServerWorkshopData(
        Hero playerHero,
        string playerHeroId,
        IObjectManager objectManager,
        ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface)
    {
        GameThread.RunSafe(() =>
        {
            WorkshopsCampaignBehavior workshopsCampaignBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            if (workshopsCampaignBehavior == null) return;

            IEnumerable<Workshop> ownedWorkshops = WorkshopsCampaignBehaviorInitializationHandler.GetWorkshopsOwnedBy(playerHero);

            workshopsCampaignBehavior.EnsureBehaviorDataSize();
            AddMissingWorkshopData(workshopsCampaignBehavior, ownedWorkshops);

            // Restore the session-store warehouse entries after every WorkshopData entry is
            // back, so a session-store failure cannot cost workshop data.
            foreach (Workshop workshop in ownedWorkshops)
            {
                if (workshop.Settlement != null && objectManager.TryGetIdWithLogging(workshop.Settlement, out string settlementId))
                {
                    sessionWorkshopPlayerDataInterface.AddNewWarehouseDataIfNeeded(playerHeroId, settlementId);
                }
            }
        }, context: nameof(WorkshopRepairer));
    }

    internal static void AddMissingWorkshopData(WorkshopsCampaignBehavior workshopsCampaignBehavior, IEnumerable<Workshop> ownedWorkshops)
    {
        foreach (Workshop workshop in ownedWorkshops)
        {
            if (workshop == null) continue;

            if (workshopsCampaignBehavior.GetDataOfWorkshop(workshop) == null)
            {
                workshopsCampaignBehavior.AddNewWorkshopData(workshop);
            }
        }
    }
}
