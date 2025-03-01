using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using System.Reflection;

namespace E2E.Tests.Services.StanceLinks;
public class StanceLinkFieldsTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public StanceLinkFieldsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }
    public void Dispose()
    {
        TestEnvironment.Dispose();
    }
}