using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace E2E.Tests.Services.Issues;

/// <summary>Commits the native picker transfer before an acceptance fixture sends its request.</summary>
internal static class AlternativeSolutionPickerDriver
{
    public static void Commit(EnvironmentInstance client, IssueBase issue, Hero owner, MobileParty party,
        CharacterObject troop, int count)
    {
        using (new MainHeroSubstitutionScope(owner, party))
        {
            Campaign.Current.EncyclopediaManager ??= new TaleWorlds.CampaignSystem.Encyclopedia.EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            client.Resolve<IAlternativeSolutionPicker>().Open(issue);
            var logic = new PartyScreenLogic();
            troop.UpgradeTargets ??= Array.Empty<CharacterObject>();
            logic.Initialize(PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(
                issue.AlternativeSolutionSentTroops, TroopRoster.CreateDummyTroopRoster(),
                PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.NotTransferable,
                PartyScreenLogic.TransferState.Transferable, null, PartyScreenHelper.PartyScreenMode.QuestTroopManage,
                partyPresentationDoneButtonDelegate: PartyScreenHelper.ManageTroopsAndPrisonersDoneHandler));
            logic._partyScreenMode = PartyScreenHelper.PartyScreenMode.QuestTroopManage;
            var transfer = new PartyScreenLogic.PartyCommand();
            var selectedBefore = issue.AlternativeSolutionSentTroops.GetTroopCount(troop);
            Assert.True(party.MemberRoster.GetTroopCount(troop) >= count);
            transfer.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Member, troop, count, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(transfer, false);
            Assert.Equal(selectedBefore + count, issue.AlternativeSolutionSentTroops.GetTroopCount(troop));
            Assert.True(logic.DoneLogic(false));
        }
    }
}
