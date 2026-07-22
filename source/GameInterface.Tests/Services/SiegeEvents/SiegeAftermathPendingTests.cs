using GameInterface.Services.SiegeEvents.Patches;
using GameInterface.Services.SiegeEvents.Interfaces;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.SiegeEvents;

public sealed class SiegeAftermathPendingTests : IDisposable
{
    public SiegeAftermathPendingTests()
    {
        SiegeAftermathPatches.PendingAftermaths.Clear();
    }

    public void Dispose()
    {
        SiegeAftermathPatches.PendingAftermaths.Clear();
    }

    [Fact]
    public void OriginalOwnerTransfer_BindsExactCaptureGenerationOnce()
    {
        var previousOwner = CreateUninitialized<Clan>();
        var captureOwner = CreateUninitialized<Clan>();
        var capturerClan = CreateUninitialized<Clan>();
        var laterCapturerClan = CreateUninitialized<Clan>();
        var leaderHero = CreateHero(capturerClan);
        var pending = CreatePending(leaderHero, previousOwner);
        var settlement = CreateSettlement(previousOwner, capturerClan);
        var newOwner = CreateHero(captureOwner);

        Assert.True(pending.IsOriginalCaptureTransition(settlement, newOwner, leaderHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege));
        Assert.True(pending.TryBindCapture(newOwner, leaderHero));
        Assert.False(pending.TryBindCapture(newOwner, leaderHero));

        settlement.Town._ownerClan = captureOwner;
        Assert.True(pending.MatchesCurrentCapture(settlement));

        settlement.Town.LastCapturedBy = laterCapturerClan;
        Assert.False(pending.MatchesCurrentCapture(settlement));

        settlement.Town.LastCapturedBy = capturerClan;
        settlement.Town._ownerClan = previousOwner;
        Assert.False(pending.MatchesCurrentCapture(settlement));
    }

    [Fact]
    public void LaterOwnerTransfer_ResolvesWhileCapturedOwnerIsStillCurrent()
    {
        var previousOwner = CreateUninitialized<Clan>();
        var captureOwner = CreateUninitialized<Clan>();
        var capturerClan = CreateUninitialized<Clan>();
        var leaderHero = CreateHero(capturerClan);
        var pending = CreatePending(leaderHero, previousOwner);
        var settlement = CreateSettlement(captureOwner, capturerClan);
        var newOwner = CreateHero(captureOwner);
        Assert.True(pending.TryBindCapture(newOwner, leaderHero));
        Assert.True(SiegeAftermathPatches.PendingAftermaths.TryAdd(settlement, pending));

        bool applied = false;
        bool resolved = SiegeAftermathPatches.ResolvePending(
            CreateUninitialized<SiegeAftermathCampaignBehavior>(), settlement, "settlement ownership changed",
            (_, party, capturedSettlement) =>
            {
                Assert.Same(pending.LeaderParty, party);
                Assert.Same(settlement, capturedSettlement);
                return SiegeAftermathAction.SiegeAftermath.ShowMercy;
            },
            (party, capturedSettlement, aftermath, oldOwner, contributions) =>
            {
                applied = true;
                Assert.Same(pending.LeaderParty, party);
                Assert.Same(settlement, capturedSettlement);
                Assert.Equal(SiegeAftermathAction.SiegeAftermath.ShowMercy, aftermath);
                Assert.Same(previousOwner, oldOwner);
                Assert.Same(pending.Contributions, contributions);
            });

        Assert.True(resolved);
        Assert.True(applied);
        Assert.Empty(SiegeAftermathPatches.PendingAftermaths);
    }

    [Fact]
    public void SyncData_RoundTripsPendingGenerationWithoutApplyingIt()
    {
        var saved = AddBoundPending();
        var contributor = CreateUninitialized<MobileParty>();
        saved.Pending.Contributions.Add(contributor, 37.5f);
        var records = new Dictionary<string, object>();

        SiegeAftermathPatches.SyncPendingAftermaths(new TestDataStore(isSaving: true, records), isClient: false);

        Assert.Single(SiegeAftermathPatches.PendingAftermaths);
        SiegeAftermathPatches.PendingAftermaths.Clear();
        SiegeAftermathPatches.SyncPendingAftermaths(new TestDataStore(isSaving: false, records), isClient: false);

        var restored = Assert.Single(SiegeAftermathPatches.PendingAftermaths);
        Assert.Same(saved.Settlement, restored.Key);
        Assert.Same(saved.Pending.LeaderParty, restored.Value.LeaderParty);
        Assert.Same(saved.Pending.LeaderHero, restored.Value.LeaderHero);
        Assert.Same(saved.Pending.PreviousOwnerClan, restored.Value.PreviousOwnerClan);
        Assert.Equal(saved.Pending.ParkedAt, restored.Value.ParkedAt);
        Assert.Same(saved.Pending.CaptureOwnerClan, restored.Value.CaptureOwnerClan);
        Assert.Same(saved.Pending.CapturerClan, restored.Value.CapturerClan);
        Assert.Equal(37.5f, restored.Value.Contributions[contributor]);
        Assert.NotSame(saved.Pending.Contributions, restored.Value.Contributions);
    }

    [Fact]
    public void SyncData_ClientConsumesServerRecordWithoutOwningPendingState()
    {
        AddBoundPending();
        var records = new Dictionary<string, object>();
        SiegeAftermathPatches.SyncPendingAftermaths(new TestDataStore(isSaving: true, records), isClient: false);

        SiegeAftermathPatches.PendingAftermaths.Clear();
        SiegeAftermathPatches.SyncPendingAftermaths(new TestDataStore(isSaving: false, records), isClient: true);

        Assert.Empty(SiegeAftermathPatches.PendingAftermaths);
    }

    [Fact]
    public void PromptSnapshot_ContainsOnlyCurrentCaptureGenerations()
    {
        var current = AddBoundPending();
        var recaptured = AddBoundPending();
        recaptured.Settlement.Town.LastCapturedBy = CreateUninitialized<Clan>();

        var prompts = new SiegeEventInterface().GetPendingSiegeAftermathPrompts();

        var prompt = Assert.Single(prompts);
        Assert.Same(current.Settlement, prompt.Settlement);
        Assert.Same(current.Pending.LeaderParty, prompt.LeaderParty);
    }

    [Fact]
    public void NarrationContext_IsParticipantScopedAndIndependentOfChoicePrompt()
    {
        var participantSettlement = CreateSettlement(CreateUninitialized<Clan>(), CreateUninitialized<Clan>());
        var unrelatedSettlement = CreateSettlement(CreateUninitialized<Clan>(), CreateUninitialized<Clan>());
        var siegeEventInterface = new SiegeEventInterface();

        siegeEventInterface.SetLocalAftermathNarrationContext(participantSettlement);
        siegeEventInterface.SetLocalAftermathNarration(unrelatedSettlement,
            (int)SiegeAftermathAction.SiegeAftermath.Devastate);
        Assert.True(siegeEventInterface.HasLocalAftermathNarrationContext(participantSettlement));

        siegeEventInterface.SetLocalAftermathNarration(participantSettlement,
            (int)SiegeAftermathAction.SiegeAftermath.Pillage);
        Assert.False(siegeEventInterface.HasLocalAftermathNarrationContext(participantSettlement));
    }

    private static (Settlement Settlement, SiegeAftermathPatches.PendingAftermath Pending) AddBoundPending()
    {
        var previousOwner = CreateUninitialized<Clan>();
        var captureOwner = CreateUninitialized<Clan>();
        var capturerClan = CreateUninitialized<Clan>();
        var leaderHero = CreateHero(capturerClan);
        var pending = CreatePending(leaderHero, previousOwner);
        Assert.True(pending.TryBindCapture(CreateHero(captureOwner), leaderHero));
        var settlement = CreateSettlement(captureOwner, capturerClan);
        Assert.True(SiegeAftermathPatches.PendingAftermaths.TryAdd(settlement, pending));
        return (settlement, pending);
    }

    private static SiegeAftermathPatches.PendingAftermath CreatePending(Hero leaderHero, Clan previousOwner)
    {
        return new SiegeAftermathPatches.PendingAftermath(
            CreateUninitialized<MobileParty>(), leaderHero, previousOwner,
            new Dictionary<MobileParty, float>(), CampaignTime.Zero);
    }

    private static Hero CreateHero(Clan clan)
    {
        var hero = CreateUninitialized<Hero>();
        hero._clan = clan;
        return hero;
    }

    private static Settlement CreateSettlement(Clan owner, Clan lastCapturedBy)
    {
        var town = CreateUninitialized<Town>();
        town._ownerClan = owner;
        town.LastCapturedBy = lastCapturedBy;

        var settlement = CreateUninitialized<Settlement>();
        settlement.Town = town;
        settlement.Party = CreateUninitialized<PartyBase>();
        return settlement;
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
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
