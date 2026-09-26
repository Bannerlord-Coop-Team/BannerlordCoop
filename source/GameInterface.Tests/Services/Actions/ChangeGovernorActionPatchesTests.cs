using Common;
using Common.Messaging;
using Autofac;
using GameInterface.Policies;
using GameInterface.Services.Clans;
using Moq;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Actions.Patches;
using GameInterface.Tests;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Actions
{
    /// <summary>
    /// Tests for the governor removal prefix in <see cref="ChangeGovernorActionPatches"/>.
    /// </summary>
    [Collection(ModInformationRoleCollection.Name)]
    public class ChangeGovernorActionPatchesTests
    {
        private static Hero CreateHero(bool isGovernor)
        {
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            if (isGovernor)
            {
                hero.GovernorOf = (Town)FormatterServices.GetUninitializedObject(typeof(Town));
            }
            return hero;
        }

        private static List<GovernorRemoved> RunPrefix(Hero hero, bool isServer, out bool runOriginal,
            bool canManage = true, bool allowOriginal = false)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(Mock.Of<ISyncPolicy>(policy => policy.AllowOriginal() == allowOriginal)).As<ISyncPolicy>();
            var permissions = new Mock<ICoopClanPermissions>();
            permissions.Setup(p => p.CanManageClan(It.IsAny<Hero>(), It.IsAny<Clan>())).Returns(canManage);
            builder.RegisterInstance(permissions.Object);
            using var container = builder.Build();
            ContainerProvider.TryGetContainer(out var previousContainer);
            var previousGame = Game.Current;
            var published = new List<GovernorRemoved>();
            Action<MessagePayload<GovernorRemoved>> capture = payload => published.Add(payload.What);
            bool originalIsServer = ModInformation.IsServer;
            MessageBroker.Instance.Subscribe(capture);
            try
            {
                ModInformation.IsServer = isServer;
                ContainerProvider.SetContainer(container);
                Game.Current = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                var character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
                character._heroObject = hero;
                Game.Current.PlayerTroop = character;
                runOriginal = ChangeGovernorActionPatches.ApplyGiveUpInternalPrefix(hero);
            }
            finally
            {
                ModInformation.IsServer = originalIsServer;
                Game.Current = previousGame;
                if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
                else ContainerProvider.Clear();
                MessageBroker.Instance.Unsubscribe(capture);
            }
            return published;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RemovingNonGovernor_SkipsVanillaAndDoesNotAnnounce(bool isServer)
        {
            // A removal can arrive after another sync channel already cleared the
            // governorship; vanilla would NullReference on GovernorOf, so the prefix
            // must turn it into a no-op without re-announcing.
            Hero hero = CreateHero(isGovernor: false);

            var published = RunPrefix(hero, isServer, out bool runOriginal);

            Assert.False(runOriginal);
            Assert.Empty(published);
        }

        [Fact]
        public void ServerRemovingActiveGovernor_RunsVanilla()
        {
            Hero hero = CreateHero(isGovernor: true);

            var published = RunPrefix(hero, isServer: true, out bool runOriginal);

            Assert.True(runOriginal);
            Assert.Empty(published);
        }

        [Fact]
        public void NonLeaderCannotRequestGovernorRemoval()
        {
            var published = RunPrefix(CreateHero(true), false, out bool runOriginal, canManage: false);
            Assert.False(runOriginal);
            Assert.Empty(published);
        }

        [Fact]
        public void ApplyingReceivedGovernorRemovalBypassesLocalPermissions()
        {
            var published = RunPrefix(CreateHero(true), false, out bool runOriginal, canManage: false, allowOriginal: true);
            Assert.True(runOriginal);
            Assert.Empty(published);
        }

        [Fact]
        public void ClientRemovingActiveGovernor_RequestsRemovalInsteadOfApplying()
        {
            Hero hero = CreateHero(isGovernor: true);

            var published = RunPrefix(hero, isServer: false, out bool runOriginal);

            Assert.False(runOriginal);
            GovernorRemoved request = Assert.Single(published);
            Assert.Same(hero, request.Governor);
        }
    }
}
