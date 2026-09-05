using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

/// <summary>Exercises the native militia setter and its replicated reserve.</summary>
[Collection(ModInformationRoleCollection.Name)]
public class MilitiaSettlementPatchTests
{
    static MilitiaSettlementPatchTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Theory]
    [InlineData(100, true, 100.5f, 0.5f)]
    [InlineData(100, true, 99.5f, -0.5f)]
    [InlineData(100, true, 100f, 0f)]
    [InlineData(100, false, 0.5f, 0.5f)]
    [InlineData(0, false, 0.5f, 0.5f)]
    public void NativeSetter_ReplicatesOnlyReserve(
        int troops, bool active, float total, float reserve)
    {
        var serverSettlement = CreateSettlement(troops, active);
        var messages = SetMilitia(serverSettlement, total, isServer: true, allowOriginal: false);

        Assert.Equal(total, serverSettlement.Militia);
        Assert.Equal(reserve, serverSettlement._readyMilitia);
        var message = Assert.Single(messages);
        Assert.Same(serverSettlement, message.Settlement);
        Assert.Equal(reserve, message.Militia);

        // The roster has its own replication; applying reserve must not count it twice.
        var clientSettlement = CreateSettlement(troops, active);
        GameThread.Run(() => MilitiaSettlementPatch.RunMiltiaChange(clientSettlement, message.Militia), blocking: true);
        Assert.Equal(total, clientSettlement.Militia);
        Assert.Equal(reserve, clientSettlement._readyMilitia);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void NativeSetter_WhenNotSynchronizing_PreservesNativeChangeWithoutPublishing(
        bool isServer, bool allowOriginal)
    {
        var settlement = CreateSettlement(100, true);
        var messages = SetMilitia(settlement, 100.5f, isServer, allowOriginal);

        Assert.Empty(messages);
        Assert.Equal(100.5f, settlement.Militia);
        Assert.Equal(0.5f, settlement._readyMilitia);
    }

    private static List<SettlementChangedMilitia> SetMilitia(
        Settlement settlement, float total, bool isServer, bool allowOriginal)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new TestSyncPolicy(allowOriginal)).As<ISyncPolicy>();
        using var container = builder.Build();
        var harmony = new Harmony("Coop.Tests.MilitiaReserve");
        var setter = AccessTools.PropertySetter(typeof(Settlement), nameof(Settlement.Militia));
        var existing = Harmony.GetPatchInfo(setter);
        var prefixes = existing?.Prefixes.Where(p => p.PatchMethod.DeclaringType == typeof(MilitiaSettlementPatch)).ToArray()
            ?? Array.Empty<Patch>();
        var postfixes = existing?.Postfixes.Where(p => p.PatchMethod.DeclaringType == typeof(MilitiaSettlementPatch)).ToArray()
            ?? Array.Empty<Patch>();
        var messages = new List<SettlementChangedMilitia>();
        void Capture(MessagePayload<SettlementChangedMilitia> payload)
        {
            if (ReferenceEquals(payload.What.Settlement, settlement)) messages.Add(payload.What);
        }
        bool wasServer = ModInformation.IsServer;
        bool hadContainer = ContainerProvider.TryGetContainer(out var previousContainer);

        MessageBroker.Instance.Subscribe<SettlementChangedMilitia>(Capture);
        try
        {
            ModInformation.IsServer = isServer;
            // The patch-all smoke test leaves its owners installed. Exercise one copy here.
            foreach (var method in prefixes.Concat(postfixes).Select(p => p.PatchMethod).Distinct())
                harmony.Unpatch(setter, method);
            harmony.CreateClassProcessor(typeof(MilitiaSettlementPatch)).Patch();
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                settlement.Militia = total;
            }
            return messages;
        }
        finally
        {
            try
            {
                harmony.UnpatchAll(harmony.Id);
                foreach (var patch in prefixes)
                    new Harmony(patch.owner).Patch(setter, prefix: RestoreMetadata(patch));
                foreach (var patch in postfixes)
                    new Harmony(patch.owner).Patch(setter, postfix: RestoreMetadata(patch));
            }
            finally
            {
                MessageBroker.Instance.Unsubscribe<SettlementChangedMilitia>(Capture);
                ModInformation.IsServer = wasServer;
                if (hadContainer) ContainerProvider.SetContainer(previousContainer);
                else ContainerProvider.Clear();
            }
        }
    }

    private static HarmonyMethod RestoreMetadata(Patch patch) => new(patch.PatchMethod)
    {
        priority = patch.priority,
        before = patch.before,
        after = patch.after,
        debug = patch.debug,
    };

    private static Settlement CreateSettlement(int troops, bool active)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        if (troops == 0) return settlement;

        var roster = ObjectHelper.SkipConstructor<TroopRoster>();
        roster._totalRegulars = troops;
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        party.MemberRoster = roster;
        var mobileParty = ObjectHelper.SkipConstructor<MobileParty>();
        mobileParty.Party = party;
        mobileParty.IsActive = active;
        var component = ObjectHelper.SkipConstructor<MilitiaPartyComponent>();
        component.MobileParty = mobileParty;
        settlement.MilitiaPartyComponent = component;
        return settlement;
    }

    /// <summary>Supplies the existing lifecycle policy for a native setter call.</summary>
    private sealed class TestSyncPolicy : ISyncPolicy
    {
        private readonly bool allowOriginal;
        public TestSyncPolicy(bool allowOriginal) => this.allowOriginal = allowOriginal;
        public bool AllowOriginal() => allowOriginal;
    }
}
