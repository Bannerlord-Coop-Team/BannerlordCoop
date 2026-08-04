using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.PlayerCaptivityService.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.PlayerCaptivityService;

public sealed class PlayerCaptivityAttackProtectionPersistenceTests : IDisposable
{
    public PlayerCaptivityAttackProtectionPersistenceTests()
    {
        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
    }

    public void Dispose()
    {
        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
    }

    [Fact]
    public void SyncData_RoundTripsServerAttackProtection()
    {
        var attackerParty = CreateMobileParty();
        var targetParty = CreateMobileParty();
        var disabledUntil = new CampaignTime(1200);
        var currentTime = new CampaignTime(1000);
        var records = new Dictionary<string, object>();
        DefaultMobilePartyAIModelPatches.PreventAttacksUntil(attackerParty, targetParty, disabledUntil);

        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: true, records), isClient: false, currentTime);
        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: false, records), isClient: false, currentTime);

        var restored = Assert.Single(DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections());
        Assert.Same(attackerParty, restored.AttackerParty);
        Assert.Same(targetParty, restored.TargetParty);
        Assert.Equal(disabledUntil, restored.DisabledUntil);
    }

    [Fact]
    public void SyncData_ClientConsumesServerRecordWithoutOwningAttackProtection()
    {
        var records = new Dictionary<string, object>();
        DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
            CreateMobileParty(), CreateMobileParty(), new CampaignTime(1200));
        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: true, records), isClient: false, new CampaignTime(1000));

        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: false, records), isClient: true, new CampaignTime(1000));

        Assert.Empty(DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections());
    }

    [Fact]
    public void SyncData_RoundTripsServerFactionAttackProtection()
    {
        var attackerParty = CreateMobileParty();
        var targetFaction = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
        var disabledUntil = new CampaignTime(1200);
        var currentTime = new CampaignTime(1000);
        var records = new Dictionary<string, object>();
        DefaultMobilePartyAIModelPatches.PreventFactionAttacksUntil(
            attackerParty,
            targetFaction,
            disabledUntil);

        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: true, records), isClient: false, currentTime);
        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: false, records), isClient: false, currentTime);

        var restored = Assert.Single(DefaultMobilePartyAIModelPatches.GetPersistedFactionAttackProtections());
        Assert.Same(attackerParty, restored.AttackerParty);
        Assert.Same(targetFaction, restored.TargetFaction);
        Assert.Equal(disabledUntil, restored.DisabledUntil);
    }

    [Fact]
    public void SyncData_PrunesExpiredProtectionBeforeSave()
    {
        var records = new Dictionary<string, object>();
        DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
            CreateMobileParty(), CreateMobileParty(), new CampaignTime(900));

        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: true, records), isClient: false, new CampaignTime(1000));

        Assert.Empty(DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections());
        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
        PlayerCaptivityAttackProtectionPersistencePatches.SyncAttackProtections(
            new TestDataStore(isSaving: false, records), isClient: false, new CampaignTime(1000));
        Assert.Empty(DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections());
    }

    [Fact]
    public void PartyDestroyed_RemovesAttackerAndTargetProtections()
    {
        var destroyedParty = CreateMobileParty();
        var otherAttacker = CreateMobileParty();
        var otherTarget = CreateMobileParty();
        DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
            destroyedParty, otherTarget, new CampaignTime(1200));
        DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
            otherAttacker, destroyedParty, new CampaignTime(1200));

        DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(destroyedParty);

        Assert.Empty(DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections());
        Assert.False(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
            destroyedParty.Ai, out _));
        Assert.False(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
            otherAttacker.Ai, out _));
    }

    private static MobileParty CreateMobileParty()
    {
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party.Ai = new MobilePartyAi(party);
        party.IsActive = true;
        return party;
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;

        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        internal TestDataStore(bool isSaving, Dictionary<string, object> records)
        {
            IsSaving = isSaving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving)
            {
                records[key] = data;
                return true;
            }

            if (!records.TryGetValue(key, out var value)) return false;
            data = (T)value;
            return true;
        }
    }
}
