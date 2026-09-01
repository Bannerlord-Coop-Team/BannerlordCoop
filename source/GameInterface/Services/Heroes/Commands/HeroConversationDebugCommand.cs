#if DEBUG
using Common;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Commands;

internal class HeroConversationDebugCommand
{

    public static string Open(List<string> args)
    {
        if (ModInformation.IsServer) return "Run coop.debug.hero_conversation.open on a client.";
        if (!TryGetHero(args[0], out var hero, out var error)) return error;
        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            return "A conversation is already active.";
        if (PlayerEncounter.Current != null) return "A player encounter is already active.";

        Campaign.Current.CurrentConversationContext = ConversationContext.Default;
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true),
            new ConversationCharacterData(
                hero.CharacterObject,
                hero.PartyBelongedTo?.Party,
                noHorse: true));

        return GetState(hero);
    }

    public static string State(List<string> args)
    {
        if (ModInformation.IsServer) return "Run coop.debug.hero_conversation.state on a client.";

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

    public static string Close(List<string> args)
    {
        if (ModInformation.IsServer) return "Run coop.debug.hero_conversation.close on a client.";

        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            Campaign.Current.ConversationManager.EndConversation();
        Campaign.Current.PlayerEncounter = null;

        return "CONVERSATION_CLOSED";
    }

    public static string SetHasMet(List<string> args)
    {
        if (!bool.TryParse(args[1], out var hasMet))
            return "Usage: coop.debug.hero_conversation.set_has_met <heroId> <true|false>";
        if (!TryGetHero(args[0], out var hero, out var error)) return error;

        if (hasMet)
            hero.SetHasMet();
        else
            hero.HasMet = false;

        return $"HERO_HAS_MET side={(ModInformation.IsServer ? "server" : "client")} " +
            $"heroId={args[0]} hasMet={hero.HasMet}";
    }

    public static string MeetingState(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.hero_conversation.meeting_state on the server.";
        if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var sessionProvider))
            return $"Unable to get {nameof(ICoopSessionProvider)}";

        long lastMeetingTicks = 0;
        var playerLastMeetingTimes = sessionProvider.CoopSession?.HeroMeetingData?.PlayerLastMeetingTimes;
        bool hasEntry = playerLastMeetingTimes != null &&
            playerLastMeetingTimes.TryGetValue(args[0], out var meetingTimes) &&
            meetingTimes != null &&
            meetingTimes.TryGetValue(args[1], out lastMeetingTicks);

        return $"HERO_MEETING_DATA playerHeroId={args[0]} metHeroId={args[1]} " +
            $"hasEntry={hasEntry} lastMeetingTicks={(hasEntry ? lastMeetingTicks.ToString(CultureInfo.InvariantCulture) : "none")}";
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
