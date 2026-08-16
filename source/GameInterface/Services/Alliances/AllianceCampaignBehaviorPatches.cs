using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Alliances.Messages;
using GameInterface.Services.Clans.Handlers;
using GameInterface.Services.Kingdoms.Extentions;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.Alliances;

[HarmonyPatch]
internal class AllianceCampaignBehaviorPatches
{
    [ThreadStatic]
    public static Hero PendingPayingHero;
    private static readonly ILogger Logger = LogManager.GetLogger<VassalServiceHandler>();
    [HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.StartAlliance))]
    private static bool Prefix(AllianceCampaignBehavior __instance, Kingdom proposerKingdom, Kingdom receiverKingdom)
    {
        if (ModInformation.IsClient)
        {
            MessageBroker.Instance.Publish(__instance, new AllianceAcceptRequested(proposerKingdom, receiverKingdom));
            return false;
        }
        if (!__instance.IsAllyWithKingdom(proposerKingdom, receiverKingdom))
        {
            StanceLink stanceWith = proposerKingdom.GetStanceWith(receiverKingdom);
            if (stanceWith.GetDailyTributeToPay(proposerKingdom) != 0)
            {
                stanceWith.SetDailyTributePaid(proposerKingdom, 0, 0);
            }
            if (stanceWith.GetDailyTributeToPay(proposerKingdom) != 0)
            {
                stanceWith.SetDailyTributePaid(proposerKingdom, 0, 0);
            }
            __instance.AddAlliance(proposerKingdom, receiverKingdom);
            CampaignEventDispatcher.Instance.OnAllianceStarted(proposerKingdom, receiverKingdom);
            foreach (IFaction faction in proposerKingdom.FactionsAtWarWith.WhereQ((IFaction f) => f.IsKingdomFaction && !f.IsAtWarWith(receiverKingdom)).ToList<IFaction>())
            {
                ProposeCallToWarAgreementDecision kingdomDecision = new ProposeCallToWarAgreementDecision(proposerKingdom.RulingClan, receiverKingdom, (Kingdom)faction);
                proposerKingdom.AddDecision(kingdomDecision, true);
            }
            foreach (IFaction faction2 in receiverKingdom.FactionsAtWarWith.WhereQ((IFaction f) => f.IsKingdomFaction && !f.IsAtWarWith(proposerKingdom)).ToList<IFaction>())
            {
                ProposeCallToWarAgreementDecision kingdomDecision2 = new ProposeCallToWarAgreementDecision(receiverKingdom.RulingClan, proposerKingdom, (Kingdom)faction2);
                receiverKingdom.AddDecision(kingdomDecision2, true);
            }
            MessageBroker.Instance.Publish(__instance, new AllianceStarted(proposerKingdom, receiverKingdom));
        }
        return false;
    }
    [HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.EndAlliance))]
    [HarmonyPostfix]
    private static void EndAlliancePostfix(AllianceCampaignBehavior __instance, Kingdom kingdom1, Kingdom kingdom2)
    {
        if (ModInformation.IsClient) return;
        MessageBroker.Instance.Publish(__instance, new AllianceEnded(kingdom1, kingdom2));
    }

    [HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.StartCallToWarAgreement))]
    [HarmonyPrefix]
    private static bool Prefix_StartCallToWarAgreement(AllianceCampaignBehavior __instance, Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, int callToWarCost, bool isPlayerPaying)
    {
        if (ModInformation.IsClient)
        {
            MessageBroker.Instance.Publish(__instance, new CallToWarAcceptRequested(callingKingdom, calledKingdom, kingdomToCallToWarAgainst, Hero.MainHero, isPlayerPaying));
            return false;
        }

        if (__instance.IsAllyWithKingdom(callingKingdom, calledKingdom) && !calledKingdom.IsAtWarWith(kingdomToCallToWarAgainst))
        {
            var agreement = __instance.AddCallToWarAgreement(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
            __instance.UpdateAllianceEndTime(callingKingdom, calledKingdom, agreement.EndTime);

            if (isPlayerPaying)
            {
                PendingPayingHero?.ChangeHeroGold(-callToWarCost);
                calledKingdom.CallToWarWallet += callToWarCost;
            }
            else
            {
                callingKingdom.CallToWarWallet -= callToWarCost;
                calledKingdom.CallToWarWallet += callToWarCost;
            }

            CampaignEventDispatcher.Instance.OnCallToWarAgreementStarted(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
            __instance.ApplyAcceptingCallToWarOfferBonus(callingKingdom, calledKingdom);
            DeclareWarAction.ApplyByCallToWarAgreement(calledKingdom, kingdomToCallToWarAgainst);

            MessageBroker.Instance.Publish(__instance, new CallToWarAgreementStarted(callingKingdom, calledKingdom, kingdomToCallToWarAgainst));
        }
        return false;
    }

    [HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.EndCallToWarAgreement))]
    [HarmonyPostfix]
    private static void EndCallToWarAgreementPostfix(AllianceCampaignBehavior __instance, Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new CallToWarAgreementEnded(callingKingdom, calledKingdom, kingdomToCallToWarAgainst));
    }

    [HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.ApplyDenyingCallToWarOfferPenalty))]
    [HarmonyPrefix]
    private static bool ApplyDenyingCallToWarOfferPenaltyPrefix(AllianceCampaignBehavior __instance, Kingdom callingKingdom, Kingdom calledKingdom)
    {
        if (ModInformation.IsClient)
        {
            MessageBroker.Instance.Publish(__instance, new CallToWarOfferDenied(callingKingdom, calledKingdom));
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    private static bool DailyTickClanPrefix(AllianceCampaignBehavior __instance, Clan clan)
    {
        if (!clan.IsEliminated)
        {
            clan.Aggressiveness -= 1f;
            if (clan.Kingdom != null && clan.Kingdom.RulingClan == clan)
            {
                Kingdom kingdom = clan.Kingdom;
                if (!kingdom.AlliedKingdoms.IsEmpty<Kingdom>())
                {
                    for (int i = kingdom.AlliedKingdoms.Count - 1; i > -1; i--)
                    {
                        Kingdom kingdom2 = kingdom.AlliedKingdoms[i];
                        AllianceCampaignBehavior.Alliance alliance;
                        if (__instance.TryGetAlliance(kingdom2, kingdom, out alliance))
                        {
                            List<AllianceCampaignBehavior.CallToWarAgreement> callToWarAgreements = __instance.GetCallToWarAgreements(kingdom, kingdom2);
                            for (int j = callToWarAgreements.Count - 1; j > -1; j--)
                            {
                                AllianceCampaignBehavior.CallToWarAgreement callToWarAgreement = callToWarAgreements[j];
                                if (callToWarAgreement.EndTime.IsPast)
                                {
                                    __instance.EndCallToWarAgreement(callToWarAgreement.CallingKingdom, callToWarAgreement.CalledKingdom, callToWarAgreement.KingdomToCallToWarAgainst);
                                }
                            }
                            if (alliance.EndTime.IsPast)
                            {
                                __instance.EndAlliance(kingdom, kingdom2);
                                if (kingdom.IsPlayerKingdom())
                                {
                                    __instance.AddAllianceDecision(kingdom, kingdom2);
                                }
                                else
                                {
                                    __instance.AddAllianceDecision(kingdom2, kingdom);
                                }
                            }
                        }
                    }
                }
            }
        }
        return false;
    }
    [HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix(AllianceCampaignBehavior __instance, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        if (faction1.IsKingdomFaction && faction2.IsKingdomFaction)
        {
            Kingdom kingdom = (Kingdom)faction1;
            Kingdom kingdom2 = (Kingdom)faction2;
            if (kingdom.IsAllyWith(kingdom2))
            {
                __instance.ApplyBrokenAlliancePenalty(kingdom, kingdom2, detail);
                __instance.EndAlliance(kingdom, kingdom2);
            }
            foreach (Kingdom kingdom3 in kingdom.AlliedKingdoms.ToList<Kingdom>())
            {
                if (!kingdom3.IsAtWarWith(kingdom2))
                {
                    ProposeCallToWarAgreementDecision kingdomDecision = new ProposeCallToWarAgreementDecision(kingdom.RulingClan, kingdom3, kingdom2);
                    kingdom.AddDecision(kingdomDecision, true);
                }
            }
            foreach (Kingdom kingdom4 in kingdom2.AlliedKingdoms.ToList<Kingdom>())
            {
                if (!kingdom4.IsAtWarWith(kingdom))
                {
                    ProposeCallToWarAgreementDecision kingdomDecision2 = new ProposeCallToWarAgreementDecision(kingdom2.RulingClan, kingdom4, kingdom);
                    kingdom2.AddDecision(kingdomDecision2, true);
                }
            }
        }
        return false;
    }
}
