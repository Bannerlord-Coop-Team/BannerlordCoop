using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;

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
            if (workshop.Owner != newOwner)
            {
                ApplyPredictedWorkshopOwnership(workshop, newOwner);

                var message = new WorkshopOwnerChanged(workshop, newOwner, workshopType, capital, cost);
                MessageBroker.Instance.Publish(null, message);
            }

            ApplyPredictedWorkshopData(workshop, newOwner);
            return false;
        }

        // Replace implementation on server
        ApplyInternalOverride(workshop, newOwner, workshopType, capital, cost);

        return false;
    }

    internal static void ApplyPredictedWorkshopOwnership(Workshop workshop, Hero newOwner)
    {
        if (workshop == null || newOwner == null || workshop.Owner == newOwner) return;

        using (new AllowedThread())
        {
            Hero owner = workshop.Owner;
            if (owner != null)
            {
                owner._ownedWorkshops?.Remove(workshop);
            }

            if (newOwner._ownedWorkshops == null)
            {
                newOwner._ownedWorkshops = new MBList<Workshop>();
            }

            workshop._owner = newOwner;
            if (!newOwner._ownedWorkshops.Contains(workshop))
            {
                newOwner._ownedWorkshops.Add(workshop);
            }
        }
    }

    internal static void ApplyPredictedWorkshopData(Workshop workshop, Hero newOwner)
    {
        if (workshop == null || newOwner == null || newOwner != Hero.MainHero || workshop.Settlement == null) return;

        var workshopsBehavior = Campaign.Current?.GetCampaignBehavior<WorkshopsCampaignBehavior>();
        if (workshopsBehavior == null) return;

        using (new AllowedThread())
        {
            workshopsBehavior.EnsureBehaviorDataSize();
            if (workshopsBehavior.GetDataOfWorkshop(workshop) == null)
            {
                workshopsBehavior.AddNewWorkshopData(workshop);
            }
            workshopsBehavior.AddNewWarehouseDataIfNeeded(workshop.Settlement);
        }
    }

    public static void ApplyInternalOverride(Workshop workshop, Hero newOwner, WorkshopType workshopType, int capital, int cost, Action onApplied = null)
    {
        GameThread.RunSafe(() =>
        {
            try
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
            finally
            {
                onApplied?.Invoke();
            }
        });
    }
}
