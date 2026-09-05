using Common.Util;
using GameInterface.Services.Clans;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

[Collection(nameof(CampaignCurrentCollection))]
public class SharedClanMembersVMTests
{
    [Fact]
    public void RebuiltMemberLists_KeepEveryMemberOnceAndAllowSelectingBothNewGroups()
    {
        var previousGame = Game.Current;
        var previousTexts = GameTexts._gameTextManager;
        try
        {
            var viewer = CreateMember();
            var player = CreateMember();
            var relative = CreateMember();
            var otherRelative = CreateMember();
            var companion = CreateMember();
            var allLords = new[] { player, relative, viewer, otherRelative };

            var character = ObjectHelper.SkipConstructor<CharacterObject>();
            character._heroObject = viewer.GetHero();
            var game = ObjectHelper.SkipConstructor<Game>();
            game.PlayerTroop = character;
            Game.Current = game;
            GameTexts._gameTextManager = new GameTextManager();
            var module = new XmlDocument();
            module.Load(Path.Combine(AppContext.BaseDirectory, "coop_SubModule.xml"));
            var registration = Assert.Single(module.SelectNodes(
                "/Module/Xmls/XmlNode/XmlName[@id='GameText' and @path='global_strings']")!.Cast<XmlElement>());
            Assert.Null(registration.ParentNode!.SelectSingleNode("IncludedGameTypes"));
            foreach (var filename in new[] { "native_module_strings.xml", registration.GetAttribute("path") + ".xml" })
            {
                var strings = new XmlDocument();
                strings.Load(Path.Combine(AppContext.BaseDirectory, filename));
                GameTexts._gameTextManager.LoadFromXML(strings);
            }
            Assert.Equal("Player", GameTexts.FindText("str_coop_clan_player").ToString());
            Assert.Equal("Player · Clan leader", GameTexts.FindText("str_coop_clan_player_leader").ToString());

            var grouping = new Mock<IClanMemberGrouping>();
            grouping.Setup(service => service.GetGroup(viewer.GetHero(), viewer.GetHero())).Returns(ClanMemberGroup.Players);
            grouping.Setup(service => service.GetGroup(player.GetHero(), viewer.GetHero())).Returns(ClanMemberGroup.Players);
            grouping.Setup(service => service.GetGroup(otherRelative.GetHero(), viewer.GetHero())).Returns(ClanMemberGroup.OtherFamilies);

            var members = ObjectHelper.SkipConstructor<SharedClanMembersVM>();
            members.Family = new MBBindingList<ClanLordItemVM>();
            members.Companions = new MBBindingList<ClanLordItemVM> { companion };
            AccessTools.Field(typeof(SharedClanMembersVM), "grouping").SetValue(members, grouping.Object);
            AccessTools.Field(typeof(SharedClanMembersVM), "<Players>k__BackingField")
                .SetValue(members, new MBBindingList<ClanLordItemVM>());
            AccessTools.Field(typeof(SharedClanMembersVM), "<OtherFamilies>k__BackingField")
                .SetValue(members, new MBBindingList<ClanLordItemVM>());

            var changedProperties = new List<string>();
            members.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
            Assert.False(members.HasPlayers);
            Assert.False(members.HasOtherFamilies);

            for (int refresh = 0; refresh < 2; refresh++)
            {
                // RefreshMembersList rebuilds vanilla Family before the grouping postfix runs.
                members.Family.Clear();
                foreach (var lord in allLords) members.Family.Add(lord);
                members.RegroupMembers();

                Assert.Same(relative, Assert.Single(members.Family));
                Assert.Equal(new[] { viewer, player }, members.Players);
                Assert.Same(otherRelative, Assert.Single(members.OtherFamilies));
                Assert.Same(companion, Assert.Single(members.Companions));
                var visible = members.Family.Concat(members.Players).Concat(members.OtherFamilies).Concat(members.Companions).ToArray();
                Assert.Equal(5, visible.Length);
                Assert.Equal(5, visible.Distinct().Count());
                Assert.Equal("Players (2)", members.PlayersText);
                Assert.Equal("Other Families (1)", members.OtherFamiliesText);
                Assert.True(members.HasPlayers);
                Assert.True(members.HasOtherFamilies);

                Assert.True(members.SelectAdditionalMember(player.GetHero()));
                Assert.Same(player, members.CurrentSelectedMember);
                Assert.True(members.SelectAdditionalMember(otherRelative.GetHero()));
                Assert.Same(otherRelative, members.CurrentSelectedMember);
                Assert.False(player.IsSelected);
                Assert.True(otherRelative.IsSelected);
                Assert.True(members.SelectAdditionalMember(viewer.GetHero()));
                Assert.Same(viewer, members.CurrentSelectedMember);
                Assert.False(otherRelative.IsSelected);
                Assert.True(viewer.IsSelected);

                members.Family.Clear();
                members.Family.Add(viewer);
                members.Family.Add(relative);
                members.Family.Add(otherRelative);
                changedProperties.Clear();
                members.RegroupMembers();
                Assert.Equal(new[] { viewer, relative, otherRelative }, members.Family);
                Assert.Empty(members.Players);
                Assert.Empty(members.OtherFamilies);
                Assert.Same(companion, Assert.Single(members.Companions));
                Assert.Equal("Family (3)", members.FamilyText);
                Assert.False(members.HasPlayers);
                Assert.False(members.HasOtherFamilies);
                Assert.False(members.SelectAdditionalMember(viewer.GetHero()));
                Assert.Contains(nameof(members.HasPlayers), changedProperties);
                Assert.Contains(nameof(members.HasOtherFamilies), changedProperties);

                members.Family.Clear();
                members.RegroupMembers();
                Assert.False(members.HasPlayers);
                Assert.False(members.HasOtherFamilies);
            }
        }
        finally
        {
            Game.Current = previousGame;
            GameTexts._gameTextManager = previousTexts;
        }
    }

    private static ClanLordItemVM CreateMember()
    {
        var member = ObjectHelper.SkipConstructor<ClanLordItemVM>();
        AccessTools.Field(typeof(ClanLordItemVM), nameof(ClanLordItemVM._hero))
            .SetValue(member, ObjectHelper.SkipConstructor<Hero>());
        return member;
    }
}
