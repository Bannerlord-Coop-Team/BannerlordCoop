#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.Armies.Commands;

public sealed class ArmyRemovalFixtureViewCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private static bool showing;
    private static PropertyBasedTooltipVM tooltip;
    private static string partyId;
    private static string viewKind;

    public ArmyRemovalFixtureViewCommand(IObjectManager objectManager) => this.objectManager = objectManager;

    public string Prefix => "coop.debug.army";
    public string Name => "removal_fixture_view";
    public string Description => "Shows and inspects the native extended army or party tooltip without desktop input.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "show, state or clear"),
        new ExpectedArgs("partyId", "Registered party id for show.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsClient || args.Count < 1)
            return new CoopCommandResult(false, "Run on a client with show, state or clear.", "command_failed");
        if (args[0] == "clear" && args.Count == 1)
        {
            InformationManager.HideTooltip();
            return new CoopCommandResult(true, "Native tooltip closed.");
        }
        if (args[0] == "show" && args.Count == 2)
        {
            if (!objectManager.TryGetObject<MobileParty>(args[1], out var party) || party.LeaderHero == null)
                return new CoopCommandResult(false, "The named lord party is missing.", "command_failed");
            var army = party.Army;
            bool joined = army != null && army.DoesLeaderPartyAndAttachedPartiesContain(party);
            partyId = args[1];
            viewKind = joined ? "army" : "party";
            showing = true;
            try
            {
                // Use the native hover view and inspection flag, preserving the actual replicated roster.
                InformationManager.ShowTooltip(joined ? typeof(Army) : typeof(MobileParty),
                    new object[] { joined ? (object)army : party, true, true });
            }
            finally { showing = false; }
        }
        else if (args[0] != "state" || args.Count != 1)
            return new CoopCommandResult(false, "Expected show partyId, state or clear.", "command_failed");
        if (tooltip == null || !tooltip.IsActive || !tooltip.IsExtended)
            return new CoopCommandResult(false, "The native extended tooltip is unavailable.", "command_failed");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            partyId, viewKind, active = tooltip.IsActive, extended = tooltip.IsExtended,
            rows = tooltip.TooltipPropertyList.Select(p => new { definition = p.DefinitionLabel, value = p.ValueLabel }).ToArray(),
        }));
    }

    [HarmonyPatch(typeof(GauntletInformationView), "OnShowTooltip")]
    private static class CaptureNativeTooltip
    {
        [HarmonyPostfix]
        private static void Postfix(TooltipBaseVM ____dataSource)
        {
            if (!showing || !ModInformation.IsClient) return;
            tooltip = ____dataSource as PropertyBasedTooltipVM;
            if (tooltip != null) tooltip.IsExtended = true;
        }
    }

    [HarmonyPatch(typeof(TooltipBaseVM), nameof(TooltipBaseVM.IsExtended), MethodType.Setter)]
    private static class KeepNativeTooltipExtended
    {
        [HarmonyPrefix]
        private static void Prefix(TooltipBaseVM __instance, ref bool value)
        {
            // Native OnTick otherwise collapses the selected view without a held desktop key.
            if (ModInformation.IsClient && ReferenceEquals(__instance, tooltip)) value = true;
        }
    }

    [HarmonyPatch(typeof(GauntletInformationView), "OnHideTooltip")]
    private static class ReleaseNativeTooltip
    {
        [HarmonyPostfix]
        private static void Postfix() => tooltip = null;
    }
}
#endif
