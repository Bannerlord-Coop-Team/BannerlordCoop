#if DEBUG
using Common;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Heroes.Commands;

internal class HeroConversationDebugCommand
{
    private static readonly Regex SafeSaveName = new("^[A-Za-z0-9_-]{1,64}$");

    [CommandLineArgumentFunction("open", "coop.debug.hero_conversation")]
    public static string Open(List<string> args)
    {
        if (ModInformation.IsServer) return "Run coop.debug.hero_conversation.open on a client.";
        if (args.Count != 1) return "Usage: coop.debug.hero_conversation.open <heroId>";
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

    [CommandLineArgumentFunction("meeting_state", "coop.debug.hero_conversation")]
    public static string MeetingState(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.hero_conversation.meeting_state on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.hero_conversation.meeting_state <playerHeroId> <metHeroId>";
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

    [CommandLineArgumentFunction("fixture_capture", "coop.debug.hero_conversation")]
    public static string FixtureCapture(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.hero_conversation.fixture_capture on the server.";
        if (args.Count != 3 || !SafeSaveName.IsMatch(args[2]))
            return "Usage: coop.debug.hero_conversation.fixture_capture <controllerId> <metHeroId> <saveName>";
        if (!TryResolveFixture(args[0], args[1], out var playerHeroId, out var metHeroId, out var playerHero, out var metHero, out var meetingData, out var error))
            return error;

        bool hasPlayerEntry = meetingData.PlayerLastMeetingTimes.TryGetValue(playerHeroId, out var meetingTimes) &&
            meetingTimes != null;
        long lastMeetingTicks = 0;
        bool hasMeetingEntry = hasPlayerEntry && meetingTimes.TryGetValue(metHeroId, out lastMeetingTicks);
        GetSavePaths(args[2], out var savePath, out var sidecarPath);

        return JsonResult(new
        {
            controllerId = args[0],
            playerHeroId,
            metHeroId,
            relation = CharacterRelationManager.GetHeroRelation(playerHero, metHero),
            serverHasMet = metHero._hasMet,
            serverLastMeetingTicks = metHero.LastMeetingTimeWithPlayer._numTicks,
            hasPlayerEntry,
            hasMeetingEntry,
            lastMeetingTicks = hasMeetingEntry ? lastMeetingTicks : 0L,
            saveName = args[2],
            saveExists = File.Exists(savePath),
            sidecarExists = File.Exists(sidecarPath)
        });
    }

    [CommandLineArgumentFunction("fixture_apply", "coop.debug.hero_conversation")]
    public static string FixtureApply(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.hero_conversation.fixture_apply on the server.";
        if (args.Count != 3 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var relation))
            return "Usage: coop.debug.hero_conversation.fixture_apply <controllerId> <metHeroId> <relation>";
        if (!TryResolveFixture(args[0], args[1], out var playerHeroId, out var metHeroId, out var playerHero, out var metHero, out _, out var error))
            return error;

        CharacterRelationManager.SetHeroRelation(playerHero, metHero, relation);
        return JsonResult(new
        {
            playerHeroId,
            metHeroId,
            relation = CharacterRelationManager.GetHeroRelation(playerHero, metHero)
        });
    }

    [CommandLineArgumentFunction("fixture_restore", "coop.debug.hero_conversation")]
    public static string FixtureRestore(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.hero_conversation.fixture_restore on the server.";
        if (!TryParseFixtureExpected(args, out var expected, out var error)) return error;
        if (!TryResolveFixture(expected.ControllerId, expected.MetHeroId, out var playerHeroId, out var metHeroId, out var playerHero, out var metHero, out var meetingData, out error))
            return error;
        if (expected.SaveExists || expected.SidecarExists)
            return "The fixture refuses to overwrite pre-existing save artifacts.";

        CharacterRelationManager.SetHeroRelation(playerHero, metHero, expected.Relation);
        metHero._hasMet = expected.ServerHasMet;
        metHero.LastMeetingTimeWithPlayer = new CampaignTime(expected.ServerLastMeetingTicks);

        if (expected.HasPlayerEntry)
        {
            if (!meetingData.PlayerLastMeetingTimes.TryGetValue(playerHeroId, out var meetingTimes) || meetingTimes == null)
            {
                meetingTimes = new Dictionary<string, long>();
                meetingData.PlayerLastMeetingTimes[playerHeroId] = meetingTimes;
            }
            if (expected.HasMeetingEntry)
                meetingTimes[metHeroId] = expected.LastMeetingTicks;
            else
                meetingTimes.Remove(metHeroId);
        }
        else
        {
            meetingData.PlayerLastMeetingTimes.Remove(playerHeroId);
        }

        GetSavePaths(expected.SaveName, out var savePath, out var sidecarPath);
        DeleteIfPresent(savePath);
        DeleteIfPresent(sidecarPath);
        return FixtureVerify(args);
    }

    [CommandLineArgumentFunction("fixture_verify", "coop.debug.hero_conversation")]
    public static string FixtureVerify(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.hero_conversation.fixture_verify on the server.";
        if (!TryParseFixtureExpected(args, out var expected, out var error)) return error;
        if (!TryResolveFixture(expected.ControllerId, expected.MetHeroId, out var playerHeroId, out var metHeroId, out var playerHero, out var metHero, out var meetingData, out error))
            return error;

        bool hasPlayerEntry = meetingData.PlayerLastMeetingTimes.TryGetValue(playerHeroId, out var meetingTimes) &&
            meetingTimes != null;
        long lastMeetingTicks = 0;
        bool hasMeetingEntry = hasPlayerEntry && meetingTimes.TryGetValue(metHeroId, out lastMeetingTicks);
        GetSavePaths(expected.SaveName, out var savePath, out var sidecarPath);
        int relation = CharacterRelationManager.GetHeroRelation(playerHero, metHero);
        bool saveExists = File.Exists(savePath);
        bool sidecarExists = File.Exists(sidecarPath);
        bool matches = relation == expected.Relation &&
            metHero._hasMet == expected.ServerHasMet &&
            metHero.LastMeetingTimeWithPlayer._numTicks == expected.ServerLastMeetingTicks &&
            hasPlayerEntry == expected.HasPlayerEntry &&
            hasMeetingEntry == expected.HasMeetingEntry &&
            (!hasMeetingEntry || lastMeetingTicks == expected.LastMeetingTicks) &&
            saveExists == expected.SaveExists &&
            sidecarExists == expected.SidecarExists;

        return JsonResult(new
        {
            matches,
            relation,
            serverHasMet = metHero._hasMet,
            serverLastMeetingTicks = metHero.LastMeetingTimeWithPlayer._numTicks,
            hasPlayerEntry,
            hasMeetingEntry,
            lastMeetingTicks = hasMeetingEntry ? lastMeetingTicks : 0L,
            saveExists,
            sidecarExists
        });
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

    private static bool TryResolveFixture(
        string controllerId,
        string metHeroId,
        out string playerHeroId,
        out string canonicalMetHeroId,
        out Hero playerHero,
        out Hero metHero,
        out HeroMeetingData meetingData,
        out string error)
    {
        playerHeroId = null;
        canonicalMetHeroId = null;
        playerHero = null;
        metHero = null;
        meetingData = null;
        error = null;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(controllerId, out var player))
        {
            error = $"Unable to find player with controller id: {controllerId}";
            return false;
        }
        playerHeroId = player.HeroId;
        if (!TryGetHero(playerHeroId, out playerHero, out error) ||
            !TryGetHero(metHeroId, out metHero, out error))
            return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetId(metHero, out canonicalMetHeroId))
        {
            error = $"Unable to get canonical id for hero: {metHeroId}";
            return false;
        }
        if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var sessionProvider) ||
            sessionProvider.CoopSession?.HeroMeetingData?.PlayerLastMeetingTimes == null)
        {
            error = "Unable to get hero meeting fixture data.";
            return false;
        }

        meetingData = sessionProvider.CoopSession.HeroMeetingData;
        return true;
    }

    private static bool TryParseFixtureExpected(List<string> args, out FixtureExpected expected, out string error)
    {
        expected = default;
        error = "Usage: coop.debug.hero_conversation.fixture_restore <controllerId> <metHeroId> <relation> " +
            "<serverHasMet> <serverLastMeetingTicks> <hasPlayerEntry> <hasMeetingEntry> <lastMeetingTicks> " +
            "<saveName> <saveExists> <sidecarExists>";
        if (args.Count != 11 ||
            !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var relation) ||
            !bool.TryParse(args[3], out var serverHasMet) ||
            !long.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var serverLastMeetingTicks) ||
            !bool.TryParse(args[5], out var hasPlayerEntry) ||
            !bool.TryParse(args[6], out var hasMeetingEntry) ||
            !long.TryParse(args[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastMeetingTicks) ||
            !SafeSaveName.IsMatch(args[8]) ||
            !bool.TryParse(args[9], out var saveExists) ||
            !bool.TryParse(args[10], out var sidecarExists))
            return false;

        expected = new FixtureExpected(
            args[0],
            args[1],
            relation,
            serverHasMet,
            serverLastMeetingTicks,
            hasPlayerEntry,
            hasMeetingEntry,
            lastMeetingTicks,
            args[8],
            saveExists,
            sidecarExists);
        error = null;
        return true;
    }

    private static void GetSavePaths(string saveName, out string savePath, out string sidecarPath)
    {
        string saveRoot = ResolveSaveRoot();
        savePath = Path.Combine(saveRoot, saveName + ".sav");
        sidecarPath = Path.Combine(saveRoot, saveName + ".json");
    }

    private static string ResolveSaveRoot()
    {
        string userDir = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
        if (!string.IsNullOrEmpty(userDir)) return Path.Combine(userDir, "Game Saves");

        if (TaleWorlds.Library.Common.PlatformFileHelper is PlatformFileHelperPC fileHelper)
        {
            var nativeSaveDirectory = new PlatformDirectoryPath(
                PlatformFileType.User,
                "Game Saves" + Path.DirectorySeparatorChar);
            string probePath = fileHelper.GetFileFullPath(
                new PlatformFilePath(nativeSaveDirectory, "fixture.path"));
            return Path.GetDirectoryName(probePath);
        }

        return "./saves/";
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path)) throw new IOException($"Failed to remove test save artifact: {path}");
    }

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private readonly struct FixtureExpected
    {
        public string ControllerId { get; }
        public string MetHeroId { get; }
        public int Relation { get; }
        public bool ServerHasMet { get; }
        public long ServerLastMeetingTicks { get; }
        public bool HasPlayerEntry { get; }
        public bool HasMeetingEntry { get; }
        public long LastMeetingTicks { get; }
        public string SaveName { get; }
        public bool SaveExists { get; }
        public bool SidecarExists { get; }

        public FixtureExpected(
            string controllerId,
            string metHeroId,
            int relation,
            bool serverHasMet,
            long serverLastMeetingTicks,
            bool hasPlayerEntry,
            bool hasMeetingEntry,
            long lastMeetingTicks,
            string saveName,
            bool saveExists,
            bool sidecarExists)
        {
            ControllerId = controllerId;
            MetHeroId = metHeroId;
            Relation = relation;
            ServerHasMet = serverHasMet;
            ServerLastMeetingTicks = serverLastMeetingTicks;
            HasPlayerEntry = hasPlayerEntry;
            HasMeetingEntry = hasMeetingEntry;
            LastMeetingTicks = lastMeetingTicks;
            SaveName = saveName;
            SaveExists = saveExists;
            SidecarExists = sidecarExists;
        }
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
