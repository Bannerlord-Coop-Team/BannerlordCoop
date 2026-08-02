#if DEBUG
using Common;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Heroes.Commands;

internal class HeroConversationDebugCommand
{
    [CommandLineArgumentFunction("open", "coop.debug.hero_conversation")]
    public static string Open(List<string> args)
    {
        if (ModInformation.IsServer) return "Run coop.debug.hero_conversation.open on a client.";
        if (args.Count != 1) return "Usage: coop.debug.hero_conversation.open <heroId>";
        if (!TryGetHero(args[0], out var hero, out var error)) return error;
        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            return "A conversation is already active.";
        if (PlayerEncounter.Current != null) return "A player encounter is already active.";

        PlayerEncounter.Start();
        Campaign.Current.CurrentConversationContext = ConversationContext.PartyEncounter;
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true),
            new ConversationCharacterData(
                hero.CharacterObject,
                hero.PartyBelongedTo?.Party ?? PartyBase.MainParty,
                noHorse: true));

        return GetState(hero);
    }

    [CommandLineArgumentFunction("state", "coop.debug.hero_conversation")]
    public static string State(List<string> args)
    {
        if (ModInformation.IsServer) return "Run coop.debug.hero_conversation.state on a client.";
        if (args.Count > 1) return "Usage: coop.debug.hero_conversation.state [heroId]";

        Hero hero;
        if (args.Count == 1)
        {
            if (!TryGetHero(args[0], out hero, out var error)) return error;
        }
        else
        {
            hero = Hero.OneToOneConversationHero;
            if (hero == null) return "CONVERSATION_STATE active=false heroId=none";
        }

        return GetState(hero);
    }

    [CommandLineArgumentFunction("close", "coop.debug.hero_conversation")]
    public static string Close(List<string> args)
    {
        if (ModInformation.IsServer) return "Run coop.debug.hero_conversation.close on a client.";
        if (args.Count != 0) return "Usage: coop.debug.hero_conversation.close";

        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            Campaign.Current.ConversationManager.EndConversation();
        Campaign.Current.PlayerEncounter = null;

        return "CONVERSATION_CLOSED";
    }

    [CommandLineArgumentFunction("set_has_met", "coop.debug.hero_conversation")]
    public static string SetHasMet(List<string> args)
    {
        if (args.Count != 2 || !bool.TryParse(args[1], out var hasMet))
            return "Usage: coop.debug.hero_conversation.set_has_met <heroId> <true|false>";
        if (!TryGetHero(args[0], out var hero, out var error)) return error;

        if (hasMet)
            hero.SetHasMet();
        else
            hero.HasMet = false;

        return $"HERO_HAS_MET side={(ModInformation.IsServer ? "server" : "client")} " +
            $"heroId={args[0]} hasMet={hero.HasMet}";
    }

    private static bool TryGetHero(string heroId, out Hero hero, out string error)
    {
        hero = null;
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            error = $"Unable to get {nameof(IObjectManager)}";
            return false;
        }
        if (!objectManager.TryGetObject(heroId, out hero))
        {
            error = $"Unable to find hero with id: {heroId}";
            return false;
        }
        return true;
    }

    private static string GetState(Hero hero)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to get {nameof(IObjectManager)}";
        string heroId = objectManager.TryGetId(hero, out var id) ? id : "unregistered";
        bool active = Campaign.Current.ConversationManager.IsConversationInProgress;
        bool selected = Hero.OneToOneConversationHero == hero;
        bool first = active && selected && Campaign.Current.ConversationManager.CurrentConversationIsFirst;
        int relation = Hero.MainHero.GetRelation(hero);

        return $"CONVERSATION_STATE active={active} selected={selected} heroId={heroId} " +
            $"heroName='{hero.Name}' first={first} hasMet={hero.HasMet} relation={relation}";
    }
}
#endif
