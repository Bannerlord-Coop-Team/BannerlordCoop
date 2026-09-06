#if DEBUG
using Common.Commands;
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

    private static CoopCommandResult Succeeded(string output) =>

        new CoopCommandResult(true, output);


    private static CoopCommandResult Failed(string output) =>

        new CoopCommandResult(false, output, "command_failed");


    public sealed class HeroConversationOpenCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_conversation";

        public string Name => "open";

        public string Description => "Opens a conversation with a registered hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id to converse with."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run coop.debug.hero_conversation.open on a client.");
            if (!TryGetHero(args[0], out var hero, out var error)) return Failed(error);
            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                return Failed("A conversation is already active.");
            if (PlayerEncounter.Current != null) return Failed("A player encounter is already active.");

            Campaign.Current.CurrentConversationContext = ConversationContext.Default;
            CampaignMapConversation.OpenConversation(
                new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true),
                new ConversationCharacterData(
                    hero.CharacterObject,
                    hero.PartyBelongedTo?.Party,
                    noHorse: true));

            return Succeeded(GetState(hero));

        }
    }

    public sealed class HeroConversationStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_conversation";

        public string Name => "state";

        public string Description => "Reports the current hero conversation state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The optional registered hero id to compare with the active conversation.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run coop.debug.hero_conversation.state on a client.");

            Hero hero;
            if (args.Count == 1)
            {
                if (!TryGetHero(args[0], out hero, out var error)) return Failed(error);
            }
            else
            {
                hero = Hero.OneToOneConversationHero;
                if (hero == null) return Succeeded("CONVERSATION_STATE active=false heroId=none");
            }

            return Succeeded(GetState(hero));

        }
    }

    public sealed class HeroConversationCloseCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_conversation";

        public string Name => "close";

        public string Description => "Closes the active hero conversation.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run coop.debug.hero_conversation.close on a client.");

            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                Campaign.Current.ConversationManager.EndConversation();
            Campaign.Current.PlayerEncounter = null;

            return Succeeded("CONVERSATION_CLOSED");

        }
    }

    public sealed class HeroConversationSetHasMetCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_conversation";

        public string Name => "set_has_met";

        public string Description => "Sets whether the local player has met a hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
            new ExpectedArgs("has_met", "True when the hero should be marked as met."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!bool.TryParse(args[1], out var hasMet))
                return Failed($"Unable to parse {args[1]} as a boolean.");
            if (!TryGetHero(args[0], out var hero, out var error)) return Failed(error);

            if (hasMet)
                hero.SetHasMet();
            else
                hero.HasMet = false;

            return Succeeded($"HERO_HAS_MET side={(ModInformation.IsServer ? "server" : "client")} " +
                $"heroId={args[0]} hasMet={hero.HasMet}");

        }
    }

    public sealed class HeroConversationMeetingStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_conversation";

        public string Name => "meeting_state";

        public string Description => "Reports cached meeting state for two heroes.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("player_hero_id", "The registered player hero id."),
            new ExpectedArgs("met_hero_id", "The registered met hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.hero_conversation.meeting_state on the server.");
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var sessionProvider))
                return Failed($"Unable to get {nameof(ICoopSessionProvider)}");

            long lastMeetingTicks = 0;
            var playerLastMeetingTimes = sessionProvider.CoopSession?.HeroMeetingData?.PlayerLastMeetingTimes;
            bool hasEntry = playerLastMeetingTimes != null &&
                playerLastMeetingTimes.TryGetValue(args[0], out var meetingTimes) &&
                meetingTimes != null &&
                meetingTimes.TryGetValue(args[1], out lastMeetingTicks);

            return Succeeded($"HERO_MEETING_DATA playerHeroId={args[0]} metHeroId={args[1]} " +
                $"hasEntry={hasEntry} lastMeetingTicks={(hasEntry ? lastMeetingTicks.ToString(CultureInfo.InvariantCulture) : "none")}");

        }
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
