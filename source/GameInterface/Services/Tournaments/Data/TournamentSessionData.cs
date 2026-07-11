using ProtoBuf;

namespace GameInterface.Services.Tournaments.Data;

public enum TournamentSessionPhase
{
    Native = 0,
    Preparation = 1,
    AwaitingChoices = 2,
    LiveMatch = 3,
    SimulatingMatch = 4,
    Completed = 5
}

public enum TournamentPlayerChoice
{
    None = 0,
    Join = 1,
    Watch = 2,
    Skip = 3
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentPlayerChoiceData
{
    [ProtoMember(1)]
    public readonly string ControllerId;
    [ProtoMember(2)]
    public readonly TournamentPlayerChoice Choice;

    public TournamentPlayerChoiceData(string controllerId, TournamentPlayerChoice choice)
    {
        ControllerId = controllerId;
        Choice = choice;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed partial class TournamentContestantData
{
    [ProtoMember(1)]
    public readonly string SlotId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly int DescriptorSeed;
    [ProtoMember(4)]
    public readonly string ControllerId;
    [ProtoMember(5)]
    public readonly string DisplayName;
    [ProtoMember(6)]
    public readonly bool IsHuman;
    [ProtoMember(7)]
    public readonly bool IsReplaced;
    [ProtoMember(8)]
    public readonly bool IsLord;
    [ProtoMember(9)]
    public readonly string DisplacedCharacterId;
    [ProtoMember(10)]
    public readonly int Score;

    public TournamentContestantData(
        string slotId,
        string characterId,
        int descriptorSeed,
        string controllerId,
        string displayName,
        bool isHuman,
        bool isReplaced,
        bool isLord,
        string displacedCharacterId,
        int score = 0)
    {
        SlotId = slotId;
        CharacterId = characterId;
        DescriptorSeed = descriptorSeed;
        ControllerId = controllerId;
        DisplayName = displayName;
        IsHuman = isHuman;
        IsReplaced = isReplaced;
        IsLord = isLord;
        DisplacedCharacterId = displacedCharacterId;
        Score = score;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentTeamData
{
    [ProtoMember(1)]
    public readonly string TeamId;
    [ProtoMember(2)]
    public readonly string[] ParticipantSlotIds;
    [ProtoMember(3)]
    public readonly int Score;
    [ProtoMember(4)]
    public readonly bool IsWinner;
    [ProtoMember(5)]
    public readonly uint TeamColor;
    [ProtoMember(6)]
    public readonly string BannerCode;
    [ProtoMember(7)]
    public readonly uint TeamColor2;

    public TournamentTeamData(
        string teamId,
        string[] participantSlotIds,
        int score,
        bool isWinner,
        uint teamColor,
        string bannerCode)
        : this(
            teamId,
            participantSlotIds,
            score,
            isWinner,
            teamColor,
            uint.MaxValue,
            bannerCode)
    {
    }

    public TournamentTeamData(
        string teamId,
        string[] participantSlotIds,
        int score,
        bool isWinner,
        uint teamColor,
        uint teamColor2,
        string bannerCode)
    {
        TeamId = teamId;
        ParticipantSlotIds = participantSlotIds ?? new string[0];
        Score = score;
        IsWinner = isWinner;
        TeamColor = teamColor;
        TeamColor2 = teamColor2;
        BannerCode = bannerCode;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed partial class TournamentMatchData
{
    [ProtoMember(1)]
    public readonly string MatchId;
    [ProtoMember(2)]
    public readonly string RoundId;
    [ProtoMember(3)]
    public readonly int State;
    [ProtoMember(4)]
    public readonly int TeamSize;
    [ProtoMember(5)]
    public readonly int NumberOfWinnerParticipants;
    [ProtoMember(6)]
    public readonly TournamentTeamData[] Teams;
    [ProtoMember(7)]
    public readonly string[] WinnerSlotIds;
    [ProtoMember(8)]
    public readonly int QualificationMode;

    public TournamentMatchData(
        string matchId,
        string roundId,
        int state,
        int teamSize,
        int numberOfWinnerParticipants,
        TournamentTeamData[] teams,
        string[] winnerSlotIds,
        int qualificationMode = 0)
    {
        MatchId = matchId;
        RoundId = roundId;
        State = state;
        TeamSize = teamSize;
        NumberOfWinnerParticipants = numberOfWinnerParticipants;
        Teams = teams ?? new TournamentTeamData[0];
        WinnerSlotIds = winnerSlotIds ?? new string[0];
        QualificationMode = qualificationMode;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentRoundData
{
    [ProtoMember(1)]
    public readonly string RoundId;
    [ProtoMember(2)]
    public readonly int RoundIndex;
    [ProtoMember(3)]
    public readonly int CurrentMatchIndex;
    [ProtoMember(4)]
    public readonly TournamentMatchData[] Matches;

    public TournamentRoundData(string roundId, int roundIndex, int currentMatchIndex, TournamentMatchData[] matches)
    {
        RoundId = roundId;
        RoundIndex = roundIndex;
        CurrentMatchIndex = currentMatchIndex;
        Matches = matches ?? new TournamentMatchData[0];
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentSessionSnapshot
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string MissionInstanceId;
    [ProtoMember(3)]
    public readonly string TownId;
    [ProtoMember(4)]
    public readonly string SceneName;
    [ProtoMember(5)]
    public readonly string PrizeItemId;
    [ProtoMember(6)]
    public readonly TournamentSessionPhase Phase;
    [ProtoMember(7)]
    public readonly long Revision;
    [ProtoMember(8)]
    public readonly long BracketRevision;
    [ProtoMember(9)]
    public readonly string CurrentMatchId;
    [ProtoMember(10)]
    public readonly string HostControllerId;
    [ProtoMember(11)]
    public readonly string[] SuccessorControllerIds;
    [ProtoMember(12)]
    public readonly TournamentContestantData[] Contestants;
    [ProtoMember(13)]
    public readonly string[] SpectatorControllerIds;
    [ProtoMember(14)]
    public readonly TournamentPlayerChoiceData[] Choices;
    [ProtoMember(15)]
    public readonly TournamentRoundData[] Rounds;
    [ProtoMember(16)]
    public readonly int ReadyCount;
    [ProtoMember(17)]
    public readonly int SkipCount;
    [ProtoMember(18)]
    public readonly int VoterCount;
    [ProtoMember(19)]
    public readonly bool SkipAllowed;
    [ProtoMember(20)]
    public readonly bool IsCompleted;
    [ProtoMember(21)]
    public readonly string WinnerSlotId;

    public TournamentSessionSnapshot(
        string sessionId,
        string missionInstanceId,
        string townId,
        string sceneName,
        string prizeItemId,
        TournamentSessionPhase phase,
        long revision,
        long bracketRevision,
        string currentMatchId,
        string hostControllerId,
        string[] successorControllerIds,
        TournamentContestantData[] contestants,
        string[] spectatorControllerIds,
        TournamentPlayerChoiceData[] choices,
        TournamentRoundData[] rounds,
        int readyCount,
        int skipCount,
        int voterCount,
        bool skipAllowed,
        bool isCompleted,
        string winnerSlotId)
    {
        SessionId = sessionId;
        MissionInstanceId = missionInstanceId;
        TownId = townId;
        SceneName = sceneName;
        PrizeItemId = prizeItemId;
        Phase = phase;
        Revision = revision;
        BracketRevision = bracketRevision;
        CurrentMatchId = currentMatchId;
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds ?? new string[0];
        Contestants = contestants ?? new TournamentContestantData[0];
        SpectatorControllerIds = spectatorControllerIds ?? new string[0];
        Choices = choices ?? new TournamentPlayerChoiceData[0];
        Rounds = rounds ?? new TournamentRoundData[0];
        ReadyCount = readyCount;
        SkipCount = skipCount;
        VoterCount = voterCount;
        SkipAllowed = skipAllowed;
        IsCompleted = isCompleted;
        WinnerSlotId = winnerSlotId;
    }
}
