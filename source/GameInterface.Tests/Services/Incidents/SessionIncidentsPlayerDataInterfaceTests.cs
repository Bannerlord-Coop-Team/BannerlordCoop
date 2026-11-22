using Common.Util;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Incidents.Interfaces;
using GameInterface.Tests.Services.SiegeEvents;
using Moq;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Incidents;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class SessionIncidentsPlayerDataInterfaceTests : IDisposable
{
    private const string PlayerHeroId = "Hero_Player1";
    private const string OtherPlayerHeroId = "Hero_Player2";
    private const string ArrowProofingId = "Incident_incident_arrow_proofing";
    private const string ApplesFromHeavenId = "Incident_incident_apples_from_heaven";

    // CampaignTime.Hours needs CampaignTime.Initialize from a real campaign, so times are raw ticks around a clock pinned at 0
    private static readonly CampaignTime Past = new CampaignTime(-500L);
    private static readonly CampaignTime Future = new CampaignTime(500L);
    private static readonly CampaignTime LaterFuture = new CampaignTime(900L);

    private readonly Campaign previousCampaign;
    private readonly CoopSession session = CoopSession.Empty;
    private readonly SessionIncidentsPlayerDataInterface incidentsDataInterface;

    public SessionIncidentsPlayerDataInterfaceTests()
    {
        // Pinned at tick 0, which is also what GameBootStrap's CampaignTimePatches returns for CampaignTime.Now
        // if another test bootstrapped the game first
        previousCampaign = Campaign.Current;
        Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
        Campaign.Current.MapTimeTracker = new MapTimeTracker(CampaignTime.Zero);

        var sessionProvider = new Mock<ICoopSessionProvider>();
        sessionProvider.SetupGet(provider => provider.CoopSession).Returns(session);
        incidentsDataInterface = new SessionIncidentsPlayerDataInterface(sessionProvider.Object);
    }

    public void Dispose() => Campaign.Current = previousCampaign;

    [Fact]
    public void TryConsumePendingIncident_SecondReplyForSameIncident_IsRejected()
    {
        incidentsDataInterface.SetPendingIncident(PlayerHeroId, ArrowProofingId);

        Assert.True(incidentsDataInterface.TryConsumePendingIncident(PlayerHeroId, ArrowProofingId));
        Assert.False(incidentsDataInterface.TryConsumePendingIncident(PlayerHeroId, ArrowProofingId));
    }

    [Fact]
    public void TryConsumePendingIncident_DifferentIncident_IsRejectedAndKeepsPending()
    {
        incidentsDataInterface.SetPendingIncident(PlayerHeroId, ArrowProofingId);

        Assert.False(incidentsDataInterface.TryConsumePendingIncident(PlayerHeroId, ApplesFromHeavenId));

        Assert.True(incidentsDataInterface.TryGetPendingIncident(PlayerHeroId, out var pendingIncidentId));
        Assert.Equal(ArrowProofingId, pendingIncidentId);
    }

    [Fact]
    public void TryConsumePendingIncident_OtherPlayersPending_IsUnaffected()
    {
        incidentsDataInterface.SetPendingIncident(PlayerHeroId, ArrowProofingId);
        incidentsDataInterface.SetPendingIncident(OtherPlayerHeroId, ArrowProofingId);

        Assert.True(incidentsDataInterface.TryConsumePendingIncident(PlayerHeroId, ArrowProofingId));

        Assert.True(incidentsDataInterface.TryGetPendingIncident(OtherPlayerHeroId, out _));
    }

    [Fact]
    public void AddPlayerKeys_NewPlayer_StartsOnGlobalCooldown()
    {
        incidentsDataInterface.AddPlayerKeys(PlayerHeroId, LaterFuture);

        Assert.True(incidentsDataInterface.IsOnGlobalCooldown(PlayerHeroId));
    }

    [Fact]
    public void AddPlayerKeys_ExistingCooldown_IsNotReplaced()
    {
        // e.g. the heir's 1 hour cooldown set by OnHeirSelectionOver before the client sends its keys
        incidentsDataInterface.SetGlobalCooldown(PlayerHeroId, Future);

        incidentsDataInterface.AddPlayerKeys(PlayerHeroId, LaterFuture);

        Assert.Equal(Future.NumTicks, session.IncidentsPlayerData.PlayerLastGlobalIncidentCooldown[PlayerHeroId]);
    }

    [Fact]
    public void IsOnGlobalCooldown_NoEntry_IsFalse()
    {
        Assert.False(incidentsDataInterface.IsOnGlobalCooldown(PlayerHeroId));
    }

    [Fact]
    public void IsOnGlobalCooldown_CooldownInThePast_IsFalse()
    {
        incidentsDataInterface.SetGlobalCooldown(PlayerHeroId, Past);

        Assert.False(incidentsDataInterface.IsOnGlobalCooldown(PlayerHeroId));
    }

    [Fact]
    public void IsOnGlobalCooldown_CooldownInTheFuture_IsTrue()
    {
        incidentsDataInterface.SetGlobalCooldown(PlayerHeroId, Future);

        Assert.True(incidentsDataInterface.IsOnGlobalCooldown(PlayerHeroId));
    }

    [Fact]
    public void CheckIncidentsOnCooldown_RemovesOnlyExpiredCooldowns()
    {
        incidentsDataInterface.AddIncidentOnCooldown(PlayerHeroId, ArrowProofingId, Past);
        incidentsDataInterface.AddIncidentOnCooldown(PlayerHeroId, ApplesFromHeavenId, Future);

        incidentsDataInterface.CheckIncidentsOnCooldown(PlayerHeroId);

        Assert.False(incidentsDataInterface.IsIncidentOnCooldown(PlayerHeroId, ArrowProofingId));
        Assert.True(incidentsDataInterface.IsIncidentOnCooldown(PlayerHeroId, ApplesFromHeavenId));
    }

    [Fact]
    public void ClearIncidentCooldowns_OnlyClearsThatPlayer()
    {
        incidentsDataInterface.AddIncidentOnCooldown(PlayerHeroId, ArrowProofingId, Future);
        incidentsDataInterface.AddIncidentOnCooldown(OtherPlayerHeroId, ArrowProofingId, Future);

        incidentsDataInterface.ClearIncidentCooldowns(PlayerHeroId);

        Assert.False(incidentsDataInterface.IsIncidentOnCooldown(PlayerHeroId, ArrowProofingId));
        Assert.True(incidentsDataInterface.IsIncidentOnCooldown(OtherPlayerHeroId, ArrowProofingId));
    }

    [Fact]
    public void CanInvokeSettlementEvent_NoEntry_IsFalse()
    {
        Assert.False(incidentsDataInterface.CanInvokeSettlementEvent(PlayerHeroId));

        incidentsDataInterface.SetCanInvokeSettlementEvent(PlayerHeroId, true);

        Assert.True(incidentsDataInterface.CanInvokeSettlementEvent(PlayerHeroId));
    }

    [Fact]
    public void GetActiveIncidentSeed_ReturnsStoredSeedPerPlayer()
    {
        incidentsDataInterface.SetActiveIncidentSeed(PlayerHeroId, 4242L);

        Assert.Equal(4242L, incidentsDataInterface.GetActiveIncidentSeed(PlayerHeroId));
        Assert.Equal(0L, incidentsDataInterface.GetActiveIncidentSeed(OtherPlayerHeroId));
    }

    [Fact]
    public void ProtobufRoundTrip_PreservesAllIncidentsPlayerData()
    {
        incidentsDataInterface.AddIncidentOnCooldown(PlayerHeroId, ArrowProofingId, LaterFuture);
        incidentsDataInterface.SetGlobalCooldown(PlayerHeroId, Future);
        incidentsDataInterface.SetActiveIncidentSeed(PlayerHeroId, 4242L);
        incidentsDataInterface.SetCanInvokeSettlementEvent(PlayerHeroId, true);
        incidentsDataInterface.SetPendingIncident(PlayerHeroId, ApplesFromHeavenId);

        var clone = Serializer.DeepClone(session.IncidentsPlayerData);

        Assert.Equal(LaterFuture.NumTicks, clone.PlayerIncidentsOnCooldown[PlayerHeroId][ArrowProofingId]);
        Assert.Equal(Future.NumTicks, clone.PlayerLastGlobalIncidentCooldown[PlayerHeroId]);
        Assert.Equal(4242L, clone.PlayerActiveIncidentSeed[PlayerHeroId]);
        Assert.True(clone.PlayerCanInvokeSettlementEvent[PlayerHeroId]);
        Assert.Equal(ApplesFromHeavenId, clone.PlayerPendingIncident[PlayerHeroId]);
    }
}
