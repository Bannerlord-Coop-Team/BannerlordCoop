using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.PartyVisuals.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Patches;

/// <summary>
/// Replaces the native player-character switch so coop can keep the switched-into party alive.
/// </summary>
[HarmonyPatch(typeof(ChangePlayerCharacterAction))]
internal class ChangePlayerCharacterActionPatches
{
    /// <summary>
    /// Switches the local main hero and party, then redraws the map against the new hero.
    /// </summary>
    [HarmonyPatch("Apply")]
    private static bool Prefix(Hero hero)
    {
        MessageBroker.Instance.Publish(null, new PlayerHeroChanged(Hero.MainHero, hero));

        Hero mainHero = Hero.MainHero;
        MobileParty mainParty = MobileParty.MainParty;
        // The party belonging to the hero we are switching INTO. In coop, SwitchToPlayer
        // pre-points MobileParty.MainParty at this same party before calling Apply, so the
        // "abandoned old party" cleanup below must never target it — otherwise a (re)joining
        // player whose party is empty (0 members) or only holds a wounded leader would have
        // their party destroyed and the character deleted.
        MobileParty targetHeroParty = hero.PartyBelongedTo;
        Game.Current.PlayerTroop = hero.CharacterObject;
        CampaignEventDispatcher.Instance.OnBeforePlayerCharacterChanged(mainHero, hero);
        Campaign.Current.OnPlayerCharacterChanged(out var isMainPartyChanged);

        if (mainParty.IsTransitionInProgress)
        {
            mainParty.CancelNavigationTransition();
        }

        if (mainParty != MobileParty.MainParty && mainParty.IsActive && mainParty != targetHeroParty)
        {
            if (mainParty.MemberRoster.TotalManCount == 0)
            {
                DestroyPartyAction.Apply(null, mainParty);
            }
            else
            {
                mainParty.LordPartyComponent.ChangePartyOwner(Hero.MainHero);
            }
        }

        _ = Hero.MainHero.IsPrisoner;
        if (hero.IsPrisoner)
        {
            PlayerCaptivity.OnPlayerCharacterChanged();
        }

        CampaignEventDispatcher.Instance.OnPlayerCharacterChanged(mainHero, hero, MobileParty.MainParty, isMainPartyChanged);
        PartyBase.MainParty.SetVisualAsDirty();
        mainParty.Party.SetVisualAsDirty();

        // Map markers and nameplates were drawn against the previous main hero, and the native views
        // do not listen for player-character changes, so redraw their friendly/enemy state now.
        if (ContainerProvider.TryResolve<IMapStanceVisualsInterface>(out var stanceVisuals))
            stanceVisuals.RedrawStanceVisuals(previousFaction: mainHero.MapFaction);

        return false;
    }
}
