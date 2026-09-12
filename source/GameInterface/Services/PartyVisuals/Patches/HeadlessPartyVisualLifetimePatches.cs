using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches;

/// <summary>
/// Party-visual lifetime for a HEADLESS server. Clients only build their map visual for a
/// runtime-created party when the server replicates a <see cref="MobilePartyVisual"/> creation
/// (<see cref="Handlers.PartyVisualLifetimeHandler"/>); on a graphical host that publication
/// comes from the MobilePartyVisual constructor patch when the host's own visual manager builds
/// one. A headless server has no visual manager (<see cref="MobilePartyVisualManager.Current"/>
/// is null), so nothing was ever published and every runtime-created party — villagers,
/// caravans, respawned lords — was INVISIBLE on clients. When no visual manager exists, publish
/// the same lifetime messages with a skip-constructed stand-in visual (it only serves as an
/// id-registered identity; nothing headless ever renders or ticks it).
/// </summary>
[HarmonyPatch]
internal class HeadlessPartyVisualLifetimePatches
{
    // Stand-ins by party, so the destroy path publishes the same instance the create path
    // registered. Weak entries die with their parties.
    private static readonly ConditionalWeakTable<PartyBase, MobilePartyVisual> shells = new();

    private static bool IsHeadlessServer => ModInformation.IsServer && MobilePartyVisualManager.Current == null;

    // The same moment a graphical host builds the visual: the party's registration at the end of
    // the MobileParty constructor — the party is already id-registered (its creation prefix runs
    // at constructor entry) and Party is assigned, so the create handler can resolve both.
    [HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.AddMobileParty))]
    [HarmonyPostfix]
    private static void AddMobilePartyPostfix(MobileParty party)
    {
        if (!IsHeadlessServer) return;

        var partyBase = party?.Party;
        if (partyBase == null) return;
        if (shells.TryGetValue(partyBase, out _)) return;

        var shell = ObjectHelper.SkipConstructor<MobilePartyVisual>();
        shells.Add(partyBase, shell);
        MessageBroker.Instance.Publish(shell, new PartyVisualCreated(shell, partyBase));
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPostfix]
    private static void RemovePartyPostfix(MobileParty __instance)
    {
        if (!IsHeadlessServer) return;

        var partyBase = __instance?.Party;
        if (partyBase == null) return;
        if (!shells.TryGetValue(partyBase, out var shell)) return;

        shells.Remove(partyBase);
        MessageBroker.Instance.Publish(shell, new PartyVisualDestroyed(shell, __instance));
    }
}
