using Common;
using Common.Logging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Helpers;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MenuHelper))]
internal class EncounterAttackConsequencePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<EncounterAttackConsequencePatch>();

    [HarmonyPatch(nameof(MenuHelper.EncounterAttackConsequence))]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        // Server can run the original consequence normally.
        if (ModInformation.IsServer)
            return true;

        Logger.Information(
            "[PvPBattleEncounterTrace] Battle encounter option clicked: attack; party={PartyId} mapEvent={MapEventId} menu={Menu} encounter={Encounter}",
            BattleTrace.DescribePartyForTrace(MobileParty.MainParty?.Party),
            BattleTrace.DescribeMapEventForTrace(BattleTrace.GetCurrentMapEventForTrace()),
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>",
            PlayerEncounter.Current != null);

        var coordinator = BattleStartCoordinator.Instance;
        if (coordinator == null)
            return true;

        var battle = BattleTrace.GetPlayerEncounterBattleForTrace();
        if (battle == null)
        {
            Logger.Warning("Client tried to start attack mission, but PlayerEncounter.Battle was null");
            return false;
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return true;
        if (!objectManager.TryGetId(battle, out var mapEventId))
            return false;

        // Carry the attacking party so the server can apply the attack's hostile-action consequences.
        objectManager.TryGetId(MobileParty.MainParty, out var attackerPartyId);

        // Block on the server's accept — the consequence frozen mid-call keeps the encounter menu in place during
        // the round trip. On accept the server makes the sides mission-ready and sends NetworkStartAttackMission to
        // open the mission; on reject (an auto-resolve already owns the event) nothing opens and the menu stays.
        coordinator.RequestBlocking(BattleStartMode.Mission, mapEventId, attackerPartyId);
        return false;
    }

}
