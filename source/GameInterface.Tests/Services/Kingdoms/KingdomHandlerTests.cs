using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;
using KingdomDecisionType = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class KingdomHandlerTests
{
    [Fact]
    public void TryGetCulture_UsesObjectManager()
    {
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        var objectManager = new Mock<IObjectManager>();
        CultureObject resolvedCulture = culture;
        objectManager.Setup(manager => manager.TryGetObject("culture-id", out resolvedCulture)).Returns(true);
        var handler = CreateHandler(objectManager.Object);

        bool result = TryGetCulture(handler, "culture-id", out CultureObject actualCulture);

        Assert.True(result);
        Assert.Same(culture, actualCulture);
        objectManager.Verify(manager => manager.TryGetObject("culture-id", out resolvedCulture), Times.Once);
    }

    [Fact]
    public void TryGetCulture_ReturnsFalseWhenObjectManagerCannotResolveCulture()
    {
        var objectManager = new Mock<IObjectManager>();
        CultureObject missingCulture = null!;
        objectManager.Setup(manager => manager.TryGetObject("culture-id", out missingCulture)).Returns(false);
        var handler = CreateHandler(objectManager.Object);

        bool result = TryGetCulture(handler, "culture-id", out CultureObject culture);

        Assert.False(result);
        Assert.Null(culture);
        objectManager.Verify(manager => manager.TryGetObject("culture-id", out missingCulture), Times.Once);
    }

    [Fact]
    public void CanChangeKingdomName_NullClan_ReturnsFalse()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        bool result = KingdomHandler.CanChangeKingdomName(
            null!,
            kingdom,
            "New Kingdom",
            out string reason);

        Assert.False(result);
        Assert.Equal("clan was null", reason);
    }

    [Fact]
    public void CanChangeKingdomName_NullKingdom_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            null!,
            "New Kingdom",
            out string reason);
        
        Assert.False(result);
        Assert.Equal("kingdom was null", reason);
    }

    [Fact]
    public void CanChangeKingdomName_ClanIsNotMember_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);
        
        Assert.False(result);
        Assert.Equal("clan is not a member of the kingdom", reason);
    }

    [Fact]
    public void CanChangeKingdomName_ClanIsNotRuler_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var otherClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        clan._kingdom = kingdom;
        kingdom._rulingClan = otherClan;
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);
        
        Assert.False(result);
        Assert.Equal("clan is not the ruling clan of the kingdom", reason);
    }

    [Fact]
    public void CanCahngeKingdomName_RulingClanWithName_ReturnsTrue()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        
        clan._kingdom = kingdom;
        kingdom._rulingClan = clan;
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);
        
        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void ClientSettlementClaimantDecision_RegistersAuthoritativeSnapshotBeforeApplying()
    {
        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        Kingdom kingdom = RegisterObject<Kingdom>(objectManager, "kingdom");
        Clan proposerClan = RegisterObject<Clan>(objectManager, "proposer");
        Settlement settlement = RegisterObject<Settlement>(objectManager, "settlement");
        Clan firstClan = RegisterObject<Clan>(objectManager, "first");
        Clan secondClan = RegisterObject<Clan>(objectManager, "second");
        var candidates = new List<SettlementClaimantCandidateData>
        {
            new SettlementClaimantCandidateData(secondClan.StringId, 15f),
            new SettlementClaimantCandidateData(firstClan.StringId, 90f),
        };
        var data = new SettlementClaimantDecisionData(
            proposerClan.StringId, kingdom.StringId, 1, false, true, false,
            settlement.StringId, null, null, candidates);
        var snapshotRegistry = new SettlementClaimantSnapshotRegistry(objectManager);
        SettlementClaimantDecision? appliedDecision = null;
        var kingdomInterface = new Mock<IKingdomInterface>();
        kingdomInterface
            .Setup(value => value.RunAddDecision(kingdom, It.IsAny<KingdomDecisionType>(), false, 0.5f))
            .Callback<Kingdom, KingdomDecisionType, bool, float>((_, decision, _, _) =>
            {
                appliedDecision = Assert.IsType<SettlementClaimantDecision>(decision);
            });
        KingdomHandler handler = CreateHandler(objectManager, kingdomInterface.Object, snapshotRegistry);
        bool wasServer = ModInformation.IsServer;

        try
        {
            ModInformation.IsServer = false;
            ApplyAddDecision(handler, new AddDecision(kingdom.StringId, data, false, 0.5f));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }

        Assert.NotNull(appliedDecision);
        Assert.True(snapshotRegistry.TryCreateOutcomes(appliedDecision, out MBList<DecisionOutcome> outcomes));
        Assert.Collection(
            outcomes,
            outcome => AssertClaimantOutcome(outcome, secondClan, 15f),
            outcome => AssertClaimantOutcome(outcome, firstClan, 90f));
    }

    [Fact]
    public void ClientSettlementClaimantDecision_WithMissingCandidate_DoesNotApply()
    {
        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        Kingdom kingdom = RegisterObject<Kingdom>(objectManager, "kingdom");
        Clan proposerClan = RegisterObject<Clan>(objectManager, "proposer");
        Settlement settlement = RegisterObject<Settlement>(objectManager, "settlement");
        var candidates = new List<SettlementClaimantCandidateData>
        {
            new SettlementClaimantCandidateData("missing", 15f),
        };
        var data = new SettlementClaimantDecisionData(
            proposerClan.StringId, kingdom.StringId, 1, false, true, false,
            settlement.StringId, null, null, candidates);
        var snapshotRegistry = new SettlementClaimantSnapshotRegistry(objectManager);
        var kingdomInterface = new Mock<IKingdomInterface>();
        KingdomHandler handler = CreateHandler(objectManager, kingdomInterface.Object, snapshotRegistry);
        bool wasServer = ModInformation.IsServer;

        try
        {
            ModInformation.IsServer = false;
            ApplyAddDecision(handler, new AddDecision(kingdom.StringId, data, false, 0.5f));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }

        kingdomInterface.Verify(
            value => value.RunAddDecision(It.IsAny<Kingdom>(), It.IsAny<KingdomDecisionType>(), It.IsAny<bool>(), It.IsAny<float>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CanChangeKingdomName_EmptyName_ReturnsFalse(string? requestedName)
    {
        var clan  = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        clan._kingdom = kingdom;
        kingdom._rulingClan = clan;
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            requestedName!,
            out string reason);
        
        Assert.False(result);
        Assert.Equal("kingdom name was empty", reason);
    }
    
    private static KingdomHandler CreateHandler(
        IObjectManager objectManager,
        IKingdomInterface? kingdomInterface = null,
        ISettlementClaimantSnapshotRegistry? settlementClaimantSnapshotRegistry = null)
    {
        return new KingdomHandler(
            new Mock<IMessageBroker>().Object,
            objectManager,
            new Mock<IPlayerManager>().Object,
            new Mock<IKingdomDecisionVoteManager>().Object,
            new Mock<IKingdomMembershipState>().Object,
            kingdomInterface ?? new Mock<IKingdomInterface>().Object,
            new Mock<IKingdomCreator>().Object,
            settlementClaimantSnapshotRegistry ?? new Mock<ISettlementClaimantSnapshotRegistry>().Object);
    }

    private static bool TryGetCulture(KingdomHandler handler, string cultureId, out CultureObject culture)
    {
        MethodInfo methodInfo = typeof(KingdomHandler).GetMethod("TryGetCulture", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NullReferenceException("TryGetCulture method was not found.");
        object[] args = { cultureId, null! };
        bool result = (bool)(methodInfo.Invoke(handler, args) ?? false);
        culture = (CultureObject)args[1];
        return result;
    }

    private static void ApplyAddDecision(KingdomHandler handler, AddDecision payload)
    {
        MethodInfo methodInfo = typeof(KingdomHandler).GetMethod("ApplyAddDecision", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NullReferenceException("ApplyAddDecision method was not found.");
        methodInfo.Invoke(handler, new object[] { payload });
    }

    private static T RegisterObject<T>(ObjectManager objectManager, string id) where T : class
    {
        T value = ObjectHelper.SkipConstructor<T>();
        PropertyInfo stringIdProperty = typeof(T).GetProperty(nameof(Clan.StringId))
            ?? throw new NullReferenceException($"{typeof(T).Name}.{nameof(Clan.StringId)} property was not found.");
        stringIdProperty.SetValue(value, id);
        objectManager.AddExisting(id, value);
        return value;
    }

    private static void AssertClaimantOutcome(DecisionOutcome outcome, Clan clan, float merit)
    {
        var claimantOutcome = Assert.IsType<SettlementClaimantDecision.ClanAsDecisionOutcome>(outcome);
        Assert.Same(clan, claimantOutcome.Clan);
        Assert.Equal(merit, claimantOutcome.InitialMerit);
    }
}
