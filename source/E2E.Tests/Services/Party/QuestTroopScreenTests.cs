using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Party.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

public class QuestTroopScreenTests : IDisposable
{
    private const int PartyTroops = 12;
    private const int SentTroops = 10;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public QuestTroopScreenTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private ((int Regulars, int Heroes) BeforeDone, (int Regulars, int Heroes) AfterDone) SendTroopsAndPressDone(
        Helpers.PartyScreenHelper.PartyScreenMode mode)
    {
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        ((int, int), (int, int)) result = default;

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            var sentTroops = TroopRoster.CreateDummyTroopRoster();
            var logic = new PartyScreenLogic();

            using (new AllowedThread())
            {
                Hero.MainHero.PartyBelongedTo = party;
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
                sentTroops.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(troop, PartyTroops);
            }

            logic.Initialize(new PartyScreenLogicInitializationData
            {
                LeftMemberRoster = sentTroops,
                LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
                RightMemberRoster = party.MemberRoster,
                RightPrisonerRoster = party.PrisonRoster,
                RightOwnerParty = party.Party,
                PartyPresentationDoneButtonDelegate = (_, _, _, _, _, _, _, _, _) => true
            });

            var states = Game.Current.GameStateManager;
            var partyState = states.CreateState<PartyState>();
            partyState.PartyScreenMode = mode;
            partyState.PartyScreenLogic = logic;
            states._gameStates.Add(partyState);

            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, SentTroops, 0, -1);

            using (new AllowedThread())
            {
                logic.TransferTroop(command, false);
                logic.RemoveZeroCounts();
            }

            var beforeDone = (sentTroops.TotalRegulars, sentTroops.TotalHeroes);

            Assert.True(logic.DoneLogic(false));

            result = (beforeDone, (sentTroops.TotalRegulars, sentTroops.TotalHeroes));
        });

        return result;
    }

    [Fact]
    public void Done_InQuestMode_KeepsTheTransferredTroopsInTheSentRoster()
    {
        var (beforeDone, afterDone) = SendTroopsAndPressDone(Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage);

        Assert.Equal((SentTroops, 1), beforeDone);
        Assert.Equal(beforeDone, afterDone);
    }

    [Fact]
    public void Done_InOtherModes_StillResetsTheLeftRoster()
    {
        var (beforeDone, afterDone) = SendTroopsAndPressDone(Helpers.PartyScreenHelper.PartyScreenMode.Normal);

        Assert.Equal((SentTroops, 1), beforeDone);
        Assert.Equal((0, 1), afterDone);
    }

    [Fact]
    public void RestoreQuestTroops_PutsBackTheCompanionTheTroopsTheirWoundedAndTheirXp()
    {
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));

            var sentTroops = TroopRoster.CreateDummyTroopRoster();
            var transferred = TroopRoster.CreateDummyTroopRoster();

            using (new AllowedThread())
            {
                sentTroops.AddToCounts(companion.CharacterObject, 1);
                transferred.AddToCounts(companion.CharacterObject, 1);
                transferred.AddToCounts(troop, SentTroops, false, 2, 500);

                PartyScreenLogicPatches.RestoreQuestTroops(sentTroops, transferred);
            }

            var index = sentTroops.FindIndexOfTroop(troop);
            Assert.Equal(SentTroops, sentTroops.GetElementNumber(index));
            Assert.Equal(2, sentTroops.GetElementWoundedNumber(index));
            Assert.Equal(500, sentTroops.GetElementXp(index));
            Assert.Equal(1, sentTroops.TotalHeroes);
        });
    }
}
