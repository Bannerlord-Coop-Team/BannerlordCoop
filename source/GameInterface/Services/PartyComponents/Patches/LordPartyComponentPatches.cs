using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(LordPartyComponent))]
internal class LordPartyComponentPatches
{
}

[HarmonyPatch(typeof(LordPartyComponent.InitializationArgs))]
internal class LordPartyComponentInitializationArgsPatches
{
    [HarmonyPatch(nameof(LordPartyComponent.InitializationArgs.InitializeLordPartyProperties))]
    [HarmonyPrefix]
    public static bool InitializeLordPartyPropertiesPrefix(ref LordPartyComponent.InitializationArgs __instance, MobileParty mobileParty, Hero owner)
    {
        mobileParty.AddElementToMemberRoster(owner.CharacterObject, 1, true);
        if (mobileParty.IsPlayerParty() || owner.Clan.IsPlayerClan())
        {
            mobileParty.InitializeMobilePartyAtPosition(__instance.Position);
        }
        else
        {
            PartyTemplateObject pt = owner.Clan.IsRebelClan ? owner.Clan.Culture.RebelsPartyTemplate : owner.Clan.DefaultPartyTemplate;
            mobileParty.InitializeMobilePartyAroundPosition(pt, __instance.Position, __instance.SpawnRadius, 0f);
        }
        mobileParty.ItemRoster.Add(new ItemRosterElement(DefaultItems.Grain, MBRandom.RandomInt(15, 30), null));
        if (__instance.SpawnSettlement != null)
        {
            MobileParty.NavigationType navigationType = mobileParty.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
            mobileParty.SetMoveGoToSettlement(__instance.SpawnSettlement, navigationType, mobileParty.IsCurrentlyAtSea);
        }

        return false;
    }
}
