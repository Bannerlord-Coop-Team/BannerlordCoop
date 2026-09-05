using Common.Commands;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

public class CoopCommandLegacyAliasesTests
{
    [Fact]
    public void LegacyAliases_AllTargetsExistAndDoNotCollide()
    {
#if DEBUG
        Assert.Equal(173, CoopCommandLegacyAliases.Map.Count);
#else
        Assert.Equal(166, CoopCommandLegacyAliases.Map.Count);
#endif

        HashSet<string> canonicalNames = GetCanonicalFullNames();
        foreach (KeyValuePair<string, string> alias in CoopCommandLegacyAliases.Map)
        {
            Assert.DoesNotContain(alias.Key, canonicalNames);
            Assert.Contains(alias.Value, canonicalNames);
        }
    }

    [Fact]
    public void Registry_AcceptsLegacyTableForAllAssemblyCommands()
    {
        var registry = new CoopCommandRegistry(
            GetAllCommands(),
            Mock.Of<ILogger>(),
            CoopCommandLegacyAliases.Map);

        Assert.True(registry.Contains("coop.debug.hero_developer.add_attribute_points"));
        Assert.True(registry.Contains("coop.debug.herodeveloper.addattributepoints"));
    }

    [Fact]
    public void LegacyAliases_KeepsReportedHeroDeveloperCommandsWorking()
    {
        Assert.Equal(
            "coop.debug.hero_developer.add_attribute_points",
            CoopCommandLegacyAliases.Map["coop.debug.herodeveloper.addattributepoints"]);
        Assert.Equal(
            "coop.debug.hero_developer.add_skill_xp",
            CoopCommandLegacyAliases.Map["coop.debug.herodeveloper.addskillxp"]);
        Assert.Equal(
            "coop.debug.hero_developer.add_focus_points",
            CoopCommandLegacyAliases.Map["coop.debug.herodeveloper.addfocuspoints"]);
        Assert.Equal(
            "coop.debug.hero_developer.reset_skills",
            CoopCommandLegacyAliases.Map["coop.debug.herodeveloper.resetskills"]);
        Assert.Equal(
            "coop.debug.hero_developer.stats",
            CoopCommandLegacyAliases.Map["coop.debug.herodeveloper.stats"]);
    }

    private static ICoopCommand[] GetAllCommands()
    {
        return typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ICoopCommand).IsAssignableFrom(type))
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
            .ToArray();
    }

    private static HashSet<string> GetCanonicalFullNames()
    {
        return new HashSet<string>(
            GetAllCommands().Select(command => $"{command.Prefix}.{command.Name}"),
            StringComparer.Ordinal);
    }
}
