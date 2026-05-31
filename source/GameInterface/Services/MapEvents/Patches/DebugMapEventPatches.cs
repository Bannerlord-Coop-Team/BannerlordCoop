using Common;
using Common.Logging;
using HarmonyLib;
using Helpers;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class DebugMapEventPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<DebugMapEventPatches>();

    [HarmonyPatch(typeof(TroopUpgradeTracker), nameof(TroopUpgradeTracker.CalculateReadyToUpgradeSafe))]
    [HarmonyPrefix]
    private static bool PrefixBattleState(TroopUpgradeTracker __instance, ref TroopRosterElement el, PartyBase owner, int __result)
    {
        // Disable on client for now
        if (ModInformation.IsClient)
        {
            __result = 0;
            return false;
        }

        int b = 0;
        CharacterObject character = el.Character;
        if (!character.IsHero && character.UpgradeTargets.Length != 0 && MobilePartyHelper.CanTroopGainXp(owner, el.Character, out var _))
        {
            int num = 0;
            for (int i = 0; i < character.UpgradeTargets.Length; i++)
            {
                int upgradeXpCost = character.GetUpgradeXpCost(owner, i);
                if (num < upgradeXpCost)
                {
                    num = upgradeXpCost;
                }
            }
            if (num > 0)
            {
                MapEventParty mapEventParty = __instance._mapEventParties.Find((MapEventParty p) => p.Party == owner);
                int num2 = el.Xp;
                foreach (FlattenedTroopRosterElement troop in mapEventParty.Troops)
                {
                    if (troop.Troop == el.Character && !troop.IsKilled)
                    {
                        num2 += troop.XpGained;
                    }
                }
                b = num2 / num;
            }
        }
        __result = MathF.Max(MathF.Min(el.Number, b), 0);
        return false;
    }

    [HarmonyPatch(typeof(MenuHelper), nameof(MenuHelper.EncounterAttackCondition))]
    [HarmonyPrefix]
    private static bool PrefixEncounterAttackCondition(MenuCallbackArgs args, ref bool __result)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
        if (MapEvent.PlayerMapEvent == null)
        {
            __result = false;
            return false;
        }
        MapEvent playerMapEvent = MapEvent.PlayerMapEvent;
        Settlement mapEventSettlement = playerMapEvent.MapEventSettlement;
        if (mapEventSettlement != null && mapEventSettlement.IsFortification && playerMapEvent.IsSiegeAssault && PlayerSiege.PlayerSiegeEvent != null && !PlayerSiege.PlayerSiegeEvent.BesiegerCamp.IsPreparationComplete)
        {
            __result = false;
            return false;
        }
        bool flag = MapEvent.PlayerMapEvent.PartiesOnSide(PartyBase.MainParty.OpponentSide).Any((MapEventParty party) => party.Party.NumberOfHealthyMembers > 0);
        if (Hero.MainHero.IsWounded)
        {
            args.Tooltip = new TextObject("{=UL8za0AO}You are wounded.");
            args.IsEnabled = false;
        }
        bool flag2 = (playerMapEvent.HasTroopsOnBothSides() || playerMapEvent.IsSiegeAssault) && MapEvent.PlayerMapEvent.GetLeaderParty(PartyBase.MainParty.OpponentSide) != null;
        if (!MobileParty.MainParty.IsInRaftState)
        {
            MobileParty mobileParty = playerMapEvent.PartiesOnSide(PlayerEncounter.Current.OpponentSide)[0].Party.MobileParty;
            if (mobileParty == null || !mobileParty.IsInRaftState)
            {
                goto IL_0125;
            }
        }
        args.Tooltip = new TextObject("{=x9ePfpw5}You are on a raft, in desperate circumstances, and cannot fight");
        args.IsEnabled = false;
        goto IL_0125;
    IL_0125:
        if (flag && !flag2 && !Hero.MainHero.IsWounded)
        {
            Debug.FailedAssert("This encounter case should be investigated", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Helpers.cs", "EncounterAttackCondition", 275);
            __result = false;
            return false;
        }
        if (flag && Game.Current.IsDevelopmentMode && (mapEventSettlement == null || playerMapEvent.IsBlockadeSallyOut || playerMapEvent.IsSallyOut || playerMapEvent.IsSiegeOutside || playerMapEvent.IsBlockade))
        {
            bool isNavalEncounter = PlayerEncounter.IsNavalEncounter();
            MapPatchData mapPatchAtPosition = Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(MobileParty.MainParty.Position);
            string battleSceneForMapPatch = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition, isNavalEncounter);
            args.Tooltip = new TextObject("{=!}[DEV] Scene: (" + battleSceneForMapPatch + ")");
        }
        if (MobileParty.MainParty.IsCurrentlyAtSea)
        {
            MapEvent encounteredBattle = PlayerEncounter.EncounteredBattle;
            if (encounteredBattle != null && encounteredBattle.MapEventSettlement?.IsVillage == true)
            {
                MapEvent encounteredBattle2 = PlayerEncounter.EncounteredBattle;
                if (encounteredBattle2 != null && encounteredBattle2.IsRaid)
                {
                    int minimumNumberOfMenForAttackingVillageViaScene = Campaign.Current.Models.EncounterModel.MinimumNumberOfMenForAttackingVillageViaScene;
                    if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < minimumNumberOfMenForAttackingVillageViaScene)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=*}You should at least have {NUMBER} healthy men in your party to take a hostile action.");
                        args.Tooltip.SetTextVariable("NUMBER", minimumNumberOfMenForAttackingVillageViaScene);
                    }
                    else if (!ShipHelper.GetOrderedNavalRaidShipsOfPlayerParty().AnyQ())
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=*}You don't have any shallow draft ship.");
                    }
                }
            }
        }

        __result = flag;
        return false;
    }
}