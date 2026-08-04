using Common;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Actions.Patches;
using GameInterface.Tests;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Actions
{
    /// <summary>
    /// Tests for the companion removal prefix in <see cref="RemoveCompanionActionPatches"/>.
    /// </summary>
    [Collection(ModInformationRoleCollection.Name)]
    public class RemoveCompanionActionPatchesTests
    {
        private static readonly System.Reflection.FieldInfo CompanionOfField =
            AccessTools.Field(typeof(Hero), "_companionOf");

        private static Clan CreateClan()
        {
            return (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        }

        private static Hero CreateHero(Clan companionOf)
        {
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            // Set the backing field directly rather than going through the CompanionOf property
            // setter, which calls into Clan.OnCompanionAdded/OnCompanionRemoved and NREs against an
            // uninitialized Clan.
            CompanionOfField.SetValue(hero, companionOf);
            return hero;
        }

        private static List<CompanionRemovalAttempted> RunPrefix(
            Clan clan,
            Hero companion,
            RemoveCompanionAction.RemoveCompanionDetail detail,
            bool isServer,
            out bool runOriginal)
        {
            var published = new List<CompanionRemovalAttempted>();
            Action<MessagePayload<CompanionRemovalAttempted>> capture = payload => published.Add(payload.What);
            bool originalIsServer = ModInformation.IsServer;
            MessageBroker.Instance.Subscribe(capture);
            try
            {
                ModInformation.IsServer = isServer;
                runOriginal = RemoveCompanionActionPatches.ApplyInternalPrefix(clan, companion, detail);
            }
            finally
            {
                ModInformation.IsServer = originalIsServer;
                MessageBroker.Instance.Unsubscribe(capture);
            }
            return published;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RemovingStaleOrNonMemberCompanion_SkipsVanillaAndDoesNotForward(bool isServer)
        {
            // A removal can arrive after another sync channel already cleared the companionship
            // (or for a hero that was never this clan's companion at all); vanilla would
            // unconditionally clear CompanionOf regardless, so the prefix must turn it into a
            // no-op without re-announcing or forwarding to the server.
            Clan clan = CreateClan();
            Hero companion = CreateHero(companionOf: null);

            var published = RunPrefix(clan, companion, RemoveCompanionAction.RemoveCompanionDetail.Fire, isServer, out bool runOriginal);

            Assert.False(runOriginal);
            Assert.Empty(published);
        }

        [Fact]
        public void RemovingCompanionOfADifferentClan_SkipsVanillaAndDoesNotForward()
        {
            Clan clan = CreateClan();
            Clan otherClan = CreateClan();
            Hero companion = CreateHero(companionOf: otherClan);

            var published = RunPrefix(clan, companion, RemoveCompanionAction.RemoveCompanionDetail.AfterQuest, isServer: false, out bool runOriginal);

            Assert.False(runOriginal);
            Assert.Empty(published);
        }

        [Fact]
        public void ServerRemovingActualCompanion_RunsVanilla()
        {
            Clan clan = CreateClan();
            Hero companion = CreateHero(companionOf: clan);

            var published = RunPrefix(clan, companion, RemoveCompanionAction.RemoveCompanionDetail.Death, isServer: true, out bool runOriginal);

            Assert.True(runOriginal);
            Assert.Empty(published);
        }

        [Fact]
        public void ClientRemovingActualCompanion_RequestsRemovalInsteadOfApplying()
        {
            Clan clan = CreateClan();
            Hero companion = CreateHero(companionOf: clan);

            var published = RunPrefix(clan, companion, RemoveCompanionAction.RemoveCompanionDetail.ByTurningToLord, isServer: false, out bool runOriginal);

            Assert.False(runOriginal);
            CompanionRemovalAttempted request = Assert.Single(published);
            Assert.Same(clan, request.Clan);
            Assert.Same(companion, request.Companion);
            Assert.Equal(RemoveCompanionAction.RemoveCompanionDetail.ByTurningToLord, request.Detail);
        }
    }
}
