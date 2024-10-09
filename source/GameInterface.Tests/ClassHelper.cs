using Common.Util;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests;
public class ClassHelper
{
    private readonly ITestOutputHelper output;

    public ClassHelper(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void PrintAllFields()
    {
        foreach (var field in AccessTools.GetDeclaredFields(typeof(Equipment)))
        {
            if (field.Name.Contains("k__BackingField")) continue;
            if (field.IsLiteral) continue;

            output.WriteLine($"| {field.Name} | TODO | |");
        }
    }

    [Fact]
    public void PrintAllProperties()
    {
        foreach (var property in AccessTools.GetDeclaredProperties(typeof(Equipment)))
        {
            if (property.CanWrite == false) continue;

            if (property.Name.Contains("k__BackingField")) continue;

            output.WriteLine($"| {property.Name} | TODO | |");
        }
    }
}