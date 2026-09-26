#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Commands;

internal static class SiegeBreakOutDebugCommands
{
    private const string SettlementId = "town_ES1";

    public sealed class JoinDefenseCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "break_out_join_defense";
        public string Description => "Joins Danustica's defense through the siege preparation menu consequence.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer || Campaign.Current == null)
                return Failed("client_campaign_required");
            var settlement = Settlement.CurrentSettlement;
            if (MobileParty.MainParty?.CurrentSettlement != settlement ||
                settlement?.StringId != SettlementId || settlement.SiegeEvent == null ||
                PlayerEncounter.Current == null ||
                Campaign.Current.CurrentMenuContext?.GameMenu?.StringId !=
                    "encounter_interrupted_siege_preparations" ||
                !settlement.SiegeEvent.CanPartyJoinSide(PartyBase.MainParty, BattleSideEnum.Defender))
                return Failed("danustica_defense_option_required");
            var behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
            if (behavior == null) return Failed("encounter_behavior_unavailable");
            behavior.game_menu_encounter_interrupted_siege_preparations_join_defend_on_consequence(null);
            return Succeeded("defense_joined");
        }
    }

    public sealed class GateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "break_out_gate";
        public string Description => "Selects the land breakout gate through its menu consequence.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetBehavior("menu_siege_strategies", out var behavior, out var reason))
                return Failed(reason);
            behavior.menu_defender_siege_break_out_from_gate_on_consequence(null);
            return Succeeded("gate_selected");
        }
    }

    public sealed class AcceptCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "break_out_accept";
        public string Description => "Accepts the land breakout through its menu consequence.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetBehavior("break_out_menu", out var behavior, out var reason))
                return Failed(reason);
            if (behavior._isBreakingOutFromPort) return Failed("port_breakout_not_supported");
            behavior.break_out_menu_accept_on_consequence(null);
            return Succeeded("accept_invoked");
        }
    }

    public sealed class ContinueCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "break_out_continue";
        public string Description => "Continues the land breakout debrief through its menu consequence.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetBehavior("break_out_debrief_menu", out var behavior, out var reason))
                return Failed(reason);
            if (behavior._isBreakingOutFromPort) return Failed("port_breakout_not_supported");
            behavior.break_out_debrief_continue_on_consequence(null);
            return Succeeded("continue_invoked");
        }
    }

    public sealed class StateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "break_out_state";
        public string Description => "Reports the party roster, settlement, siege and local menu state.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("partyId", "Registered defender party id.")
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (Campaign.Current == null ||
                !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
                !objects.TryGetObject<MobileParty>(args[0], out var party))
                return Failed("party_unavailable");

            bool isLocal = ModInformation.IsClient && ReferenceEquals(party, MobileParty.MainParty);
            var menu = isLocal ? Campaign.Current.CurrentMenuContext?.GameMenu : null;
            var behavior = isLocal ? Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>() : null;
            var roster = party.MemberRoster;
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
            {
                success = true,
                partyId = party.StringId,
                currentSettlementId = party.CurrentSettlement?.StringId,
                besiegedSettlementId = party.BesiegedSettlement?.StringId,
                besiegerCampSettlementId = party.BesiegerCamp?.SiegeEvent?.BesiegedSettlement?.StringId,
                hasMapEvent = party.MapEvent != null,
                hasArmy = party.Army != null,
                memberCount = roster.TotalManCount,
                regularCount = roster.TotalRegulars,
                members = roster.GetTroopRoster().Select(element => new
                {
                    characterId = element.Character.StringId,
                    count = element.Number,
                    wounded = element.WoundedNumber
                }).ToArray(),
                localPlayer = isLocal,
                menuId = menu?.StringId,
                menuText = menu?.GetText()?.ToString(),
                encounterSettlementId = isLocal ? PlayerEncounter.EncounterSettlement?.StringId : null,
                playerSiegeSettlementId = isLocal ? PlayerSiege.PlayerSiegeEvent?.BesiegedSettlement?.StringId : null,
                playerSiegeSide = isLocal && PlayerSiege.PlayerSiegeEvent != null
                    ? PlayerSiege.PlayerSide.ToString() : null,
                debriefCasualties = behavior?._breakInOutCasualties?.GetTroopRoster()
                    .Select(element => new
                    {
                        characterId = element.Character.StringId,
                        count = element.Number
                    }).ToArray(),
                debriefArmyCasualties = behavior?._breakInOutArmyCasualties
            }));
        }
    }

    private static bool TryGetBehavior(string expectedMenu, out EncounterGameMenuBehavior behavior, out string reason)
    {
        behavior = null;
        reason = null;
        if (ModInformation.IsServer || Campaign.Current == null)
            reason = "client_campaign_required";
        else if (MobileParty.MainParty?.CurrentSettlement?.StringId != SettlementId ||
                 PlayerSiege.PlayerSiegeEvent?.BesiegedSettlement?.StringId != SettlementId ||
                 PlayerSiege.PlayerSide != BattleSideEnum.Defender ||
                 PlayerEncounter.Current == null)
            reason = "danustica_defender_encounter_required";
        else if (Campaign.Current.CurrentMenuContext?.GameMenu?.StringId != expectedMenu)
            reason = "expected_menu_" + expectedMenu;
        else
            behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
        if (behavior == null && reason == null) reason = "encounter_behavior_unavailable";
        return reason == null;
    }

    private static CoopCommandResult Succeeded(string action) =>
        new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            success = true,
            action,
            menuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
            settlementId = MobileParty.MainParty?.CurrentSettlement?.StringId
        }));

    private static CoopCommandResult Failed(string reason) =>
        new CoopCommandResult(false, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            success = false,
            reason
        }), "fixture_failed");
}
#endif
