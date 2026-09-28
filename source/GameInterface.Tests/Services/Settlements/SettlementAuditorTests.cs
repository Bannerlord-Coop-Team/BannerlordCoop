using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Audit;
using GameInterface.Tests.Services.SiegeEvents;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

/// <summary>
/// Covers how <see cref="SettlementAuditor"/> handles audit responses and timeouts.
/// </summary>
[Collection(nameof(CampaignCurrentCollection))]
public class SettlementAuditorTests : IDisposable
{
    private readonly Campaign previousCampaign = Campaign.Current;
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<INetwork> network = new();
    private readonly Mock<IObjectManager> objectManager = new(MockBehavior.Strict);
    // Strict so a test that forgets the timeout fails instead of reading TimeSpan.Zero
    private readonly Mock<INetworkConfig> configuration = new(MockBehavior.Strict);
    private readonly SettlementAuditor auditor;
    private Action<MessagePayload<SettlementAuditResponse>>? responseHandler;
    private TimeSpan auditTimeout;

    public SettlementAuditorTests()
    {
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<SettlementAuditResponse>>>()))
            .Callback<Action<MessagePayload<SettlementAuditResponse>>>(handler => responseHandler = handler);

        auditor = new SettlementAuditor(messageBroker.Object, network.Object, objectManager.Object, configuration.Object);
    }

    public void Dispose()
    {
        auditor.Dispose();
        Campaign.Current = previousCampaign;
        ModInformation.IsServer = wasServer;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Response_WithoutPendingAudit_IsIgnored(bool isServer)
    {
        ModInformation.IsServer = isServer;
        Campaign.Current = null;

        Assert.Null(Record.Exception(() => Respond()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Response_WithoutPendingAudit_SkipsAuditData(bool isServer)
    {
        ModInformation.IsServer = isServer;
        UseEmptyCampaign();
        Settlement? settlement = null;
        objectManager.Setup(o => o.TryGetObject(It.IsAny<string>(), out settlement)).Returns(false);

        Assert.Null(Record.Exception(() => Respond(ObjectHelper.SkipConstructor<SettlementAuditData>())));
        objectManager.Verify(o => o.TryGetObject(It.IsAny<string>(), out settlement), Times.Never);
    }

    [Fact]
    public void Audit_ReturnsResults_AndIgnoresRepeatedResponse()
    {
        ModInformation.IsServer = false;
        SetAuditTimeout(TimeSpan.FromSeconds(5));
        UseEmptyCampaign();
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback(() => Respond());

        string result = auditor.Audit();

        Assert.Contains("Server Audit Results:", result);
        Assert.Contains("server results", result);
        Assert.Contains("CLient Audit Results:", result);
        Assert.Contains("Auditing 0 objects", result);
        Assert.Null(Record.Exception(() => Respond()));
    }

    [Fact]
    public void Audit_TimesOut_AndIgnoresLateResponse()
    {
        ModInformation.IsServer = false;
        SetAuditTimeout(TimeSpan.FromMilliseconds(50));
        UseEmptyCampaign();

        Assert.Equal("Audit timed out", auditor.Audit());

        Campaign.Current = null;
        Assert.Null(Record.Exception(() => Respond()));
    }

    [Fact]
    public async Task Audit_EarlierAuditTimer_DoesNotCancelLaterAudit()
    {
        ModInformation.IsServer = false;
        UseEmptyCampaign();
        SetAuditTimeout(TimeSpan.FromMilliseconds(100));
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback(() => Respond());
        auditor.Audit();

        // The response arrives after the first audit's timeout would have elapsed
        SetAuditTimeout(TimeSpan.FromSeconds(5));
        Task? lateResponse = null;
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback(() => lateResponse = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            Respond();
        }));

        string result = auditor.Audit();

        Assert.DoesNotContain("Audit timed out", result);
        Assert.Contains("Server Audit Results:", result);
        Assert.Null(await Record.ExceptionAsync(() => lateResponse!));
    }

    [Fact]
    public async Task Audit_ClientAuditFailure_ThrowsWithoutWaitingForTimeout()
    {
        ModInformation.IsServer = false;
        SetAuditTimeout(TimeSpan.FromSeconds(5));
        UseEmptyCampaign();
        Settlement? settlement = null;
        objectManager
            .Setup(o => o.TryGetObject(It.IsAny<string>(), out settlement))
            .Throws(new InvalidOperationException("audit failed"));
        Task? response = null;
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback(() =>
            response = Task.Run(() => Respond(ObjectHelper.SkipConstructor<SettlementAuditData>())));

        var exception = Assert.Throws<AggregateException>(() => auditor.Audit());

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        // Still thrown to the broker so it gets logged
        await Assert.ThrowsAsync<InvalidOperationException>(() => response!);
    }

    private void SetAuditTimeout(TimeSpan timeout)
    {
        auditTimeout = timeout;
        configuration.SetupGet(c => c.AuditTimeout).Returns(() => auditTimeout);
    }

    private static void UseEmptyCampaign()
    {
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.CampaignObjectManager = new CampaignObjectManager
        {
            Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
        };
        Campaign.Current = campaign;
    }

    private void Respond(params SettlementAuditData[] data)
    {
        var response = new SettlementAuditResponse(data, "server results");
        responseHandler!(new MessagePayload<SettlementAuditResponse>(this, response));
    }
}
