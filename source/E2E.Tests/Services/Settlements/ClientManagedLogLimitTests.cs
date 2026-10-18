using Common.Logging;
using E2E.Tests.Environment;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

public sealed class ClientManagedLogLimitTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private readonly List<string> capturedLogs = new();
    private readonly object logGate = new();
    private readonly Action<string> captureLog;

    public ClientManagedLogLimitTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        captureLog = CaptureLog;
        OutputSinkManager.AddLogCallback(captureLog);
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(captureLog);
        testEnvironment.Dispose();
    }

    [Fact]
    public void ClientWageLimitSetter_LogsOncePerCallerWithoutNamingArmy()
    {
        var settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        var client = testEnvironment.Clients.First();
        ClearCapturedLogs();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            for (int i = 0; i < 200; i++)
            {
                settlement.SetGarrisonWagePaymentLimit(i);
            }
            Assert.Equal(199, settlement.GarrisonWagePaymentLimit);
        });

        var logs = GetCapturedLogs();
        var wageLog = Assert.Single(logs, log => log.Contains("GarrisonWagePaymentLimit to"));
        Assert.Contains("GarrisonWagePaymentLimit to 0", wageLog);
        Assert.Contains("caller=", wageLog);
        Assert.Contains(nameof(ClientManagedLogLimitTests), wageLog);
        Assert.DoesNotContain(logs, log => log.Contains("TaleWorlds.CampaignSystem.Army"));
    }

    [Fact]
    public void ClientCreatedManagedNonEquipment_LogsOncePerCaller()
    {
        var settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        var client = testEnvironment.Clients.First();
        ClearCapturedLogs();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject(settlementId, out Settlement settlement));
            for (int i = 0; i < 200; i++)
            {
                _ = new Alley(settlement, "alley_" + i, new TextObject("alley"));
            }
        });

        var alleyLog = Assert.Single(GetCapturedLogs(), log =>
            log.Contains("Client created managed") && log.Contains(typeof(Alley).FullName!) && log.Contains("caller="));
        Assert.Contains("caller=", alleyLog);
        Assert.Contains(nameof(ClientManagedLogLimitTests), alleyLog);
    }

    private void CaptureLog(string message)
    {
        lock (logGate)
        {
            capturedLogs.Add(message);
        }
    }

    private void ClearCapturedLogs()
    {
        lock (logGate)
        {
            capturedLogs.Clear();
        }
    }

    private string[] GetCapturedLogs()
    {
        lock (logGate)
        {
            return capturedLogs.ToArray();
        }
    }
}
