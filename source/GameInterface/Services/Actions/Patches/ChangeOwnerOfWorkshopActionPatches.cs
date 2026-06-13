using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeOwnerOfWorkshopAction))]
internal class ChangeOwnerOfWorkshopActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<TeleportHeroActionPatches>();

    [HarmonyPatch(nameof(ChangeOwnerOfWorkshopAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(Workshop workshop, Hero newOwner, WorkshopType workshopType, int capital, int cost)
    {
        // Send message to server to manage workshop with new owner if called on client
        if (ModInformation.IsClient)
        {
            var message = new WorkshopOwnerChanged(workshop, newOwner, workshopType, capital, cost);
            MessageBroker.Instance.Publish(null, message);

            return false;
        }

        // Replace implementation on server
        ApplyInternalOverride(workshop, newOwner, workshopType, capital, cost);

        return false;
    }

    public static void ApplyInternalOverride(Workshop workshop, Hero newOwner, WorkshopType workshopType, int capital, int cost)
    {
        Hero owner = workshop.Owner;
        workshop.ChangeOwnerOfWorkshop(newOwner, workshopType, capital);
        if (newOwner.IsPlayerHero())
        {
            GiveGoldAction.ApplyBetweenCharacters(newOwner, owner, cost, false);
        }
        if (owner.IsPlayerHero())
        {
            GiveGoldAction.ApplyBetweenCharacters(null, owner, cost, false);
        }
        CampaignEventDispatcher.Instance.OnWorkshopOwnerChanged(workshop, owner);
    }
}
