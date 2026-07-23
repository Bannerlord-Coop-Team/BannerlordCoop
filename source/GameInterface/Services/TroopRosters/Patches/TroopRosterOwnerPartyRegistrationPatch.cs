using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches;

/// <summary>
/// Registers party member/prison <see cref="TroopRoster"/> instances on creation.
///
/// The <c>TroopRoster(PartyBase)</c> constructor is only 14 bytes of IL (chain to <c>: this()</c>
/// then <c>set_OwnerParty</c>) and gets JIT-inlined into its callers — <c>PartyBase..ctor</c>,
/// <c>BanditPartyComponent.InitializationArgs.InitializeBanditOnCreation</c>,
/// <c>SmugglersIssueQuest.InitializePartyState</c>, etc. Because the call is inlined, Harmony's
/// AutoRegistry lifetime prefix on that ctor never runs and the roster is never registered.
///
/// <c>set_OwnerParty</c> is the single call every such ctor makes, it has no other callers (so it
/// fires exactly once per party roster at construction), and it is reliably patched (not inlined —
/// the AutoSync setter prefix used to fire here). That makes it the dependable chokepoint for
/// registration. Dummy rosters (parameterless ctor, no OwnerParty) are handled by
/// <see cref="TroopRosterCreateDummyPatch"/>; load-time rosters by <c>TroopRosterRegistry.RegisterAllObjects</c>.
/// </summary>
[HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.OwnerParty), MethodType.Setter)]
internal class TroopRosterOwnerPartyRegistrationPatch
{
    [HarmonyPostfix]
    private static void Postfix(TroopRoster __instance)
    {
        // Skip replays (AllowedThread) and client-side construction; clients receive both the roster
        // creation and the owner assignment from the server.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient) return;

        // Register the roster first (publishes InstanceCreated -> NetworkCreateInstance) so its id
        // exists before the owner-set message is resolved. CreatePrefix only reads __instance.
        var roster = __instance;
        LifetimePatches<TroopRoster>.CreatePrefix(ref roster);

        // OwnerParty is no longer an AutoSync property; replicate the back-reference explicitly so
        // each client's roster points at its owning party.
        MessageBroker.Instance.Publish(__instance, new PartyOwnerSet(__instance, __instance.OwnerParty));
    }
}
