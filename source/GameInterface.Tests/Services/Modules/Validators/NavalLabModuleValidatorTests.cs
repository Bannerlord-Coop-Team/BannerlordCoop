#if DEBUG
using Common;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Modules.Validators;

[Collection(ModInformationRoleCollection.Name)]
public sealed class NavalLabModuleValidatorTests : IDisposable
{
    private readonly string previous = ModInformation.NavalLabCapability;
    private const string OptIn = "new-campaign:571cda18-4f3b-4787-9ac0-5997f17f083e";
    private static readonly ApplicationVersion Version = new(ApplicationVersionType.Release, 1, 4, 7, 352);

    public NavalLabModuleValidatorTests() => typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!.SetValue(null, null);

    private static List<ModuleInfo> Modules(string? scope, bool dlc = true)
    {
        var modules = new List<ModuleInfo> { new("Native", true, false, Version), new("Coop", false, false, Version) };
        if (scope != null) modules.Add(new(ModInformation.NavalLabCapabilityPrefix + scope, false, false, Version));
        if (dlc) modules.Add(new("NavalDLC", true, true, Version));
        return modules;
    }

    [Fact]
    public void OptInAbsent_RunTokenAloneDoesNotPermitDlc()
    {
        ModInformation.ConfigureNavalLab(null, "run", true);
        Assert.False(ModInformation.IsNavalLab);
        Assert.False(new ModuleValidator().Validate(Modules(null), Modules(null), out _));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("run", false)]
    [InlineData("invalid scope", true)]
    public void OptIn_RequiresValidScopeAndActiveDlc(string? scope, bool dlc)
    {
        Assert.Throws<InvalidOperationException>(() => ModInformation.ConfigureNavalLab(OptIn, scope, dlc));
        Assert.False(ModInformation.IsNavalLab);
    }

    [Fact]
    public void MalformedOptIn_FailsRatherThanLoadingDefaultSave()
    {
        Assert.Throws<InvalidOperationException>(() => ModInformation.ConfigureNavalLab("true", "run", true));
        Assert.False(ModInformation.IsNavalLab);
    }

    [Fact]
    public void MatchingLabPeers_AcceptOnlyNavalDlcAndKeepVersionChecks()
    {
        ModInformation.ConfigureNavalLab(OptIn, "run", true);
        Assert.EndsWith(".run", ModInformation.NavalLabCapability);
        var validator = new ModuleValidator();
        Assert.True(validator.Validate(Modules("same"), Modules("same"), out _));
        Assert.False(validator.ValidateNoDlc(Modules("same"), out _));
        var client = Modules("same");
        client.Add(new("OtherDlc", true, true, Version));
        Assert.False(validator.Validate(Modules("same"), client, out _));
        client = Modules("same");
        client[3] = new("NavalDLC", true, true, new(ApplicationVersionType.Release, 1, 4, 7, 353));
        Assert.False(validator.Validate(Modules("same"), client, out _));
        client = Modules("same");
        client[0] = new("Native", true, false, new(ApplicationVersionType.Release, 1, 4, 8, 352));
        Assert.False(validator.Validate(Modules("same"), client, out _));
        client = Modules("same");
        client.RemoveAt(1);
        Assert.False(validator.Validate(Modules("same"), client, out _));
    }

    [Theory]
    [InlineData("same", null, true, true)]
    [InlineData(null, "same", true, true)]
    [InlineData("nonce.run1", "nonce.run2", true, true)]
    [InlineData("nonce1.run", "nonce2.run", true, true)]
    [InlineData("same", "same", true, false)]
    [InlineData("same", "same", false, true)]
    public void MixedCapabilitiesOrMissingDlc_Reject(string? server, string? client, bool serverDlc, bool clientDlc)
    {
        Assert.False(new ModuleValidator().Validate(Modules(server, serverDlc), Modules(client, clientDlc), out _));
    }

    public void Dispose() => typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!.SetValue(null, previous);
}
#endif
