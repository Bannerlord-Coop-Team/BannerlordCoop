using GameInterface.Configuration;
using Common;
using GameInterface.Services.Modules;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace GameInterface.Tests.Configuration;

/// <summary>
/// ModConfig instances are self-contained, but the discovery-rung tests mutate
/// the process-wide BANNERLORD_USER_DIR variable — the collection serializes
/// the class so they can never race another config test.
/// </summary>
[CollectionDefinition("ModConfigSerial", DisableParallelization = true)]
public class ModConfigSerialCollection { }

[Collection("ModConfigSerial")]
public class ModConfigTests : IDisposable
{
    private readonly string tempDir;

    public ModConfigTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "modconfig-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    private readonly bool originalIsServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = originalIsServer;
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }

    private string ConfigPath => Path.Combine(tempDir, "mod-config.json");

    /// <summary>The shipped template (repo deploy\mod-config.default.json),
    /// copied beside the test assembly by the csproj — the same file the module
    /// ships and seeds from.</summary>
    private static string ShippedTemplatePath => Path.Combine(
        Path.GetDirectoryName(typeof(ModConfig).Assembly.Location), "mod-config.default.json");

    private IModConfig NewModConfig() => new ModConfig(tempDir);

    [Fact]
    public void MissingFile_IsSeededFromTheShippedTemplate()
    {
        var config = NewModConfig().Data;

        Assert.True(File.Exists(ConfigPath), "first load should create mod-config.json");
        Assert.Equal(File.ReadAllText(ShippedTemplatePath), File.ReadAllText(ConfigPath));
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PlayerReceivedDamage);
        Assert.True(config.Difficulty.BirthAndDeath);
    }

    [Fact]
    public void ShippedTemplate_ShipsEveryDifficultyKeyActive()
    {
        File.Copy(ShippedTemplatePath, ConfigPath);

        var config = NewModConfig().Data;

        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PlayerTroopsReceivedDamage);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.CombatAIDifficulty);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.RecruitmentDifficulty);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PlayerMapMovementSpeed);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.StealthAndDisguiseDifficulty);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PersuasionSuccessChance);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.ClanMemberDeathChance);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.BattleDeath);
        Assert.True(config.Difficulty.BirthAndDeath);
        Assert.False(config.Difficulty.AutoAllocateClanMemberPerks);
        Assert.Equal(1000, config.ModOptions.BattleSize);
        Assert.True(config.UnknownKeys == null || config.UnknownKeys.Count == 0);
        Assert.True(config.Difficulty.UnknownKeys == null || config.Difficulty.UnknownKeys.Count == 0);
        Assert.True(config.ModOptions.UnknownKeys == null || config.ModOptions.UnknownKeys.Count == 0);
    }

    [Fact]
    public void LegacyNetworkBlock_IsIgnoredWithoutBreakingOtherSettings()
    {
        File.WriteAllText(ConfigPath, @"{
  ""network"": {
    ""movementOutgoingMiBPerSecond"": 1.0,
    ""movementIncomingMiBPerSecond"": 1.0,
  },
  ""modOptions"": { ""autoPauseEnabled"": false },
}");

        var config = NewModConfig().Data;

        Assert.False(config.ModOptions.AutoPauseEnabled);
        Assert.True(config.UnknownKeys.ContainsKey("network"));
    }

    [Fact]
    public void MissingDifficultySettings_ReceiveDefaultsWithoutChangingActiveValues()
    {
        ModInformation.IsServer = true;
        File.WriteAllText(ConfigPath, @"{
  ""difficulty"": {
    // unrelated operator note
    ""battleDeath"": ""Easy"",
  },
  ""modOptions"": { ""clientsCanUseCheats"": true },
}");

        var config = NewModConfig().Data;
        string firstMigration = File.ReadAllText(ConfigPath);
        _ = NewModConfig().Data;

        Assert.Equal(DifficultyLevel.Easy, config.Difficulty.BattleDeath);
        Assert.True(config.Difficulty.BirthAndDeath);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.CombatAIDifficulty);
        Assert.True(config.ModOptions.ClientsCanUseCheats);
        Assert.Contains("// unrelated operator note", firstMigration);
        Assert.Equal(firstMigration, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void MissingDifficultyBlock_ReceivesDefaultsAndIsIdempotent()
    {
        ModInformation.IsServer = true;
        File.WriteAllText(ConfigPath, @"{
  // preserve this operator file
  ""modOptions"": { ""clientsCanUseCheats"": true },
}");
        var config = NewModConfig().Data;
        string firstMigration = File.ReadAllText(ConfigPath);
        _ = NewModConfig().Data;

        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.BattleDeath);
        Assert.True(config.Difficulty.BirthAndDeath);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.CombatAIDifficulty);
        Assert.True(config.ModOptions.ClientsCanUseCheats);
        Assert.Contains("// preserve this operator file", firstMigration);
        Assert.Contains("\"difficulty\"", firstMigration);
        Assert.Equal(firstMigration, File.ReadAllText(ConfigPath));
        Assert.Empty(Directory.GetFiles(tempDir, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void InvalidDifficultyBlockIsNotDuplicatedByMigration()
    {
        ModInformation.IsServer = true;
        File.WriteAllText(ConfigPath, "{ \"difficulty\": false }");

        _ = NewModConfig().Data;
        string merged = File.ReadAllText(ConfigPath);
        var template = JObject.Parse(File.ReadAllText(ShippedTemplatePath));

        Assert.Single(Regex.Matches(merged, "\"difficulty\""));
        Assert.Contains("\"difficulty\": false", merged);
        Assert.True(JToken.DeepEquals(template["modOptions"], JObject.Parse(merged)["modOptions"]));
    }

    [Fact]
    public void MalformedConfig_IsNotChangedByMigration()
    {
        ModInformation.IsServer = true;
        const string broken = "{ // \"birthAndDeath\": true,\r\n this is not JSON";
        File.WriteAllText(ConfigPath, broken);

        var config = NewModConfig().Data;

        Assert.Equal(broken, File.ReadAllText(ConfigPath));
        Assert.Null(config.Difficulty.BirthAndDeath);
    }

    [Fact]
    public void ConfigFromAnotherDirectory_IsNotMovedOrRead()
    {
        string userRoot = Path.Combine(tempDir, "Mount and Blade II Bannerlord");
        string coopData = Path.Combine(userRoot, "CoopData");
        Directory.CreateDirectory(userRoot);
        string legacyPath = Path.Combine(userRoot, "mod-config.json");
        string migratedPath = Path.Combine(coopData, "mod-config.json");
        const string legacy = @"{
  ""difficulty"": { ""battleDeath"": ""Easy"" },
  ""modOptions"": { ""autoPauseEnabled"": false }
}";
        File.WriteAllText(legacyPath, legacy);

        var config = new ModConfig(coopData).Data;

        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.BattleDeath);
        Assert.True(config.ModOptions.AutoPauseEnabled);
        Assert.Equal(legacy, File.ReadAllText(legacyPath));
        Assert.True(File.Exists(migratedPath), "the selected config location should still be seeded");
        Assert.DoesNotContain("\"battleDeath\": \"Easy\"", File.ReadAllText(migratedPath));
    }

    /// <summary>
    /// The options a session runs on BEFORE any config is loaded (server) or received (client) must
    /// already be the documented defaults. <see cref="ModOptions"/> is a struct declaring no
    /// parameterless constructor, so a plain <c>new ModOptions()</c> is just <c>default</c>: the
    /// property initializers never run and every option silently reads back false/0 (no auto-pause,
    /// no AI joining player battles, no looters, no smithing stamina, no clan-tier requirement for a
    /// kingdom). The un-loaded default must therefore be built through the real constructor from an
    /// all-absent config.
    /// </summary>
    [Fact]
    public void UnloadedModOptions_AreTheDocumentedDefaults_NotAZeroedStruct()
    {
        var options = ModConfigProvider.ModOptions;

        Assert.True(options.FastForwardEnabled);
        Assert.True(options.AutoPauseEnabled);
        Assert.True(options.AllowClientTimeControls);
        Assert.False(options.ClientsCanUseCheats);
        Assert.False(options.GoldFoodInfluenceChangeInSettlements);
        Assert.Equal(GoldFoodChangeMode.Disabled, options.GoldFoodInfluenceChangeInBattles);
        Assert.False(options.GoldFoodInfluenceChangeForDisconnectedPlayers);
        Assert.Equal(6, options.PlayerBattleAiJoinWindowHours);
        Assert.Equal(0, options.PostBattleAiAmnestySeconds);
        Assert.Equal(60, options.KingdomDecisionVoteTimeoutSeconds);
        Assert.True(options.SpeedLimitWhilePlayersInBattle);
        Assert.True(options.EnsureUnaffiliatedWanderers);
        Assert.Equal(32, options.WandererLimit);
        Assert.Equal(1, options.SpawnRateWanderers);
        Assert.False(options.WandererLimitScalesWithPlayers);
        Assert.Equal(4, options.PlayerKingdomClanTierRequired);
        Assert.True(options.SmithingStaminaRecoveryOutsideSettlements);
        Assert.Equal(0.6f, options.SmithingStaminaRecoveryMultiplier);
        Assert.Equal(1f, options.MaximumLootersMultiplier);
        Assert.Equal(LordDefectionRetryMode.Vanilla, options.LordDefectionRetries);
        Assert.True(options.ShowPlayerNameplates);
        Assert.True(options.CoopClansEnabled);
        Assert.False(options.EnableHeroExecutions);
        Assert.False(options.BlockAiWarDeclarationsOnOfflinePlayers);
        Assert.Equal(ClansAfterKingdomDestructionMode.Vanilla, options.ClansAfterKingdomDestruction);
        Assert.Empty(options.PreferredBattleHosts);
        Assert.False(options.PvpBlockLeave);
        Assert.Equal(0, options.PvpResponseTimerSeconds);
        Assert.Equal(0, options.MaxClansPerKingdom);
        Assert.False(options.MaxClansPerKingdomCountsMercenaries);
    }

    /// <summary>
    /// Unlike the difficulty block, the template's modOptions keys ship LIVE — so each one has to
    /// name a real schema property (a typo just lands in the overflow and the option silently never
    /// applies, however carefully the operator edits it), and the values it ships have to be the same
    /// defaults a session runs on with no file at all.
    /// </summary>
    [Fact]
    public void ShippedTemplate_ModOptions_AllBind_AndAreTheDefaults()
    {
        File.Copy(ShippedTemplatePath, ConfigPath);

        var config = NewModConfig().Data;

        Assert.True(config.ModOptions.UnknownKeys == null || config.ModOptions.UnknownKeys.Count == 0,
            "every modOptions key in the template must name a schema property, but these did not: " +
            string.Join(", ", config.ModOptions.UnknownKeys?.Keys ?? Array.Empty<string>()));

        // Read back a value rather than only the overflow: an unparsed block would leave every
        // property null, which the defaults comparison below would accept as a vacuous pass.
        Assert.Equal(1f, config.ModOptions.MaximumLootersMultiplier);
        Assert.Equal(60, config.ModOptions.KingdomDecisionVoteTimeoutSeconds);
        Assert.True(config.ModOptions.CoopClansEnabled);
        Assert.Equivalent(ModConfigProvider.ModOptions, new ModOptions(config.ModOptions), strict: true);
    }

    [Fact]
    public void ConfiguredValues_Read_WithCommentsTrailingCommasAndAnyCase()
    {
        File.WriteAllText(ConfigPath, @"{
  // comment survives
  ""difficulty"": {
    ""PLAYERRECEIVEDDAMAGE"": ""realistic"",
    ""playerTroopsReceivedDamage"": ""Easy"",
    ""birthAndDeath"": false,
  },
  ""modOptions"": {
    ""KINGDOMDECISIONVOTETIMEOUTSECONDS"": 600,
    ""PreferredBattleHosts"": [""76561198000000002"", null, ""76561198000000001"",],
  },
}");

        var config = NewModConfig().Data;

        Assert.Equal(DifficultyLevel.Realistic, config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(DifficultyLevel.Easy, config.Difficulty.PlayerTroopsReceivedDamage);
        Assert.False(config.Difficulty.BirthAndDeath);
        Assert.Equal(600, new ModOptions(config.ModOptions).KingdomDecisionVoteTimeoutSeconds);
        Assert.Equal(new[] { "76561198000000002", "76561198000000001" }, new ModOptions(config.ModOptions).PreferredBattleHosts);
    }

    [Fact]
    public void InvalidEnumValue_SkipsThatMember_KeepsTheRest()
    {
        File.WriteAllText(ConfigPath, @"{
  ""difficulty"": {
    ""playerReceivedDamage"": ""Medium"",
    ""battleDeath"": ""Easy""
  },
  ""modOptions"": {
    ""kingdomDecisionVoteTimeoutSeconds"": ""invalid"",
    ""wandererLimit"": 40,
    ""preferredBattleHosts"": null,
  }
}");

        var config = NewModConfig().Data;

        Assert.Null(config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(60, new ModOptions(config.ModOptions).KingdomDecisionVoteTimeoutSeconds);
        Assert.Empty(new ModOptions(config.ModOptions).PreferredBattleHosts);
        Assert.Equal(40, config.ModOptions.WandererLimit);
        Assert.Equal(DifficultyLevel.Easy, config.Difficulty.BattleDeath);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"yes\"", false)]
    [InlineData("1", true)]
    public void BlockAiWarDeclarationsOnOfflinePlayers_ReadsTheKey_AndSkipsOnlyABadValue(string value, bool expected)
    {
        File.WriteAllText(ConfigPath, @"{
  ""modOptions"": {
    ""blockAiWarDeclarationsOnOfflinePlayers"": " + value + @",
    ""wandererLimit"": 40
  }
}");

        var options = new ModOptions(NewModConfig().Data.ModOptions);

        Assert.Equal(expected, options.BlockAiWarDeclarationsOnOfflinePlayers);
        Assert.Equal(40, options.WandererLimit);
    }

    [Fact]
    public void BrokenJson_FallsBackToDefaults()
    {
        File.WriteAllText(ConfigPath, "{ this is not json");

        var config = NewModConfig().Data;

        Assert.NotNull(config);
        Assert.Null(config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(60, new ModOptions(config.ModOptions).KingdomDecisionVoteTimeoutSeconds);
    }

    [Fact]
    public void UnknownKeys_LandInOverflow_BothLevels()
    {
        File.WriteAllText(ConfigPath, @"{
  ""bogusTopLevel"": 1,
  ""difficulty"": { ""bogusNested"": ""x"" }
}");

        var config = NewModConfig().Data;

        Assert.True(config.UnknownKeys.ContainsKey("bogusTopLevel"));
        Assert.True(config.Difficulty.UnknownKeys.ContainsKey("bogusNested"));
    }

    [Fact]
    public void SameInstance_CachesTheRead()
    {
        var modConfig = NewModConfig();
        var first = modConfig.Data;                  // seeds and loads

        File.WriteAllText(ConfigPath, @"{ ""difficulty"": { ""battleDeath"": ""Easy"" } }");

        Assert.Same(first, modConfig.Data);          // lazy: no re-read on the same instance
        Assert.Equal(DifficultyLevel.Easy, NewModConfig().Data.Difficulty.BattleDeath); // new container would
    }

    [Fact]
    public void NoDiscoverableLocation_RunsDefaults_WritesNothing()
    {
        string savedData = Environment.GetEnvironmentVariable("COOP_DATA_DIR");
        string savedEnv = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
        Environment.SetEnvironmentVariable("COOP_DATA_DIR", null);
        Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", null);
        try
        {
            // Guard the premise: with a platform helper installed in-process this
            // test would resolve to a REAL user directory and quietly seed a file
            // there. Fail loudly instead of writing outside the test sandbox.
            Assert.Null(TaleWorlds.Library.Common.PlatformFileHelper);

            // Production constructor: no override, no env vars, no platform helper
            // — every rung comes up empty.
            var config = new ModConfig().Data;

            Assert.NotNull(config);
            Assert.False(File.Exists(ConfigPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COOP_DATA_DIR", savedData);
            Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", savedEnv);
        }
    }

    [Fact]
    public void CoopDataDirRung_WinsOverUserDirRung()
    {
        string savedData = Environment.GetEnvironmentVariable("COOP_DATA_DIR");
        string savedEnv = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
        string decoy = Path.Combine(tempDir, "decoy-user-dir");
        Directory.CreateDirectory(decoy);
        Environment.SetEnvironmentVariable("COOP_DATA_DIR", tempDir);
        Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", decoy);
        try
        {
            _ = new ModConfig().Data;

            Assert.True(File.Exists(ConfigPath), "COOP_DATA_DIR should win the discovery");
            Assert.False(File.Exists(Path.Combine(decoy, "mod-config.json")),
                "BANNERLORD_USER_DIR must not be consulted when COOP_DATA_DIR is set");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COOP_DATA_DIR", savedData);
            Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", savedEnv);
        }
    }

    [Fact]
    public void EnvVarRung_WinsWhenNoOverride()
    {
        string savedData = Environment.GetEnvironmentVariable("COOP_DATA_DIR");
        string savedEnv = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
        Environment.SetEnvironmentVariable("COOP_DATA_DIR", null);
        Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", tempDir);
        try
        {
            _ = new ModConfig().Data;

            Assert.True(File.Exists(ConfigPath), "env-var discovery should seed into BANNERLORD_USER_DIR");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COOP_DATA_DIR", savedData);
            Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", savedEnv);
        }
    }

    // War Sails isn't officially supported, so the shipped template and an absent key both leave it off.
    [Fact]
    public void ShippedTemplate_ShipsWarSailsOff()
    {
        File.Copy(ShippedTemplatePath, ConfigPath);

        var config = NewModConfig().Data;

        Assert.False(config.ModOptions.WarSails);
        Assert.Equal(File.ReadAllText(ShippedTemplatePath), File.ReadAllText(ConfigPath));
    }

    // The shipped template's settings are all added to an old file, and an operator's warSails value wins, whatever its casing.
    [Fact]
    public void ShippedTemplate_IsMergedIntoOldFile_KeepingExistingWarSails()
    {
        ModInformation.IsServer = true;
        File.WriteAllText(ConfigPath, "{ \"modOptions\": { \"WarSails\": true } }");

        var config = NewModConfig().Data;
        var merged = JObject.Parse(File.ReadAllText(ConfigPath));
        var template = JObject.Parse(File.ReadAllText(ShippedTemplatePath));

        foreach (var block in template.Properties())
        {
            Assert.NotNull(merged.GetValue(block.Name, StringComparison.OrdinalIgnoreCase));
        }
        var modOptions = (JObject)merged.GetValue("modOptions", StringComparison.OrdinalIgnoreCase);
        foreach (var key in ((JObject)template["modOptions"]).Properties())
        {
            Assert.NotNull(modOptions.GetValue(key.Name, StringComparison.OrdinalIgnoreCase));
        }
        Assert.True(config.ModOptions.WarSails);
        Assert.DoesNotContain("\"warSails\": false", File.ReadAllText(ConfigPath));
    }

    private const string MergeTemplate = @"{
  ""voice"": {
    ""mapMaximumDistance"": 20
  },

  // A category added in a later version.
  ""newCategory"": {
    // A setting in the new category.
    ""newSetting"": true,

    // A nested group, copied with its block.
    ""nestedSettings"": {
      ""first"": true,
      ""second"": false
    }
  },

  ""modOptions"": {
    // Enables proximity voice for every player.
    ""voiceEnabled"": true,

    // Toggle War Sails (NavalDLC) co-op on a dedicated server.
    ""warSails"": false
  }
}";

    private IModConfig NewModConfigWithTemplate(string template)
    {
        string templatePath = Path.Combine(tempDir, "template.json");
        File.WriteAllText(templatePath, template);
        return new ModConfig(tempDir, templatePath);
    }

    // On a server, blocks and keys new in the template are copied in with their comments and nothing else changes.
    [Fact]
    public void ServerLoad_AddsNewTemplateSettingsWithComments_AndIsIdempotent()
    {
        ModInformation.IsServer = true;
        File.WriteAllText(ConfigPath, @"{
  // operator note
  ""voice"": {
    ""mapMaximumDistance"": 35
  },
  ""difficulty"": { ""battleDeath"": ""Easy"" },
  ""modOptions"": {
    // Enables proximity voice for every player.
    ""voiceEnabled"": false
  }
}");

        var config = NewModConfigWithTemplate(MergeTemplate).Data;
        string firstLoad = File.ReadAllText(ConfigPath);
        _ = NewModConfigWithTemplate(MergeTemplate).Data;

        Assert.Equal(@"{
  // operator note
  ""voice"": {
    ""mapMaximumDistance"": 35
  },

  // A category added in a later version.
  ""newCategory"": {
    // A setting in the new category.
    ""newSetting"": true,

    // A nested group, copied with its block.
    ""nestedSettings"": {
      ""first"": true,
      ""second"": false
    }
  },

  ""difficulty"": { ""battleDeath"": ""Easy"" },
  ""modOptions"": {
    // Enables proximity voice for every player.
    ""voiceEnabled"": false,

    // Toggle War Sails (NavalDLC) co-op on a dedicated server.
    ""warSails"": false
  }
}", firstLoad);
        Assert.Equal(firstLoad, File.ReadAllText(ConfigPath));
        Assert.False(config.ModOptions.VoiceEnabled);
        Assert.False(config.ModOptions.WarSails);
    }

    // A key the operator commented out stays commented out while a key the file really lacks is added.
    [Fact]
    public void ServerLoad_TreatsCommentedOutKeyAsPresent()
    {
        ModInformation.IsServer = true;
        File.WriteAllText(ConfigPath, @"{
  ""modOptions"": {
    ""voiceEnabled"": true,
    // ""warSails"": true,
  }
}");

        _ = NewModConfigWithTemplate(@"{
  ""modOptions"": {
    ""voiceEnabled"": true,
    // Toggle War Sails (NavalDLC) co-op on a dedicated server.
    ""warSails"": false,
    ""newSetting"": true
  }
}").Data;
        string merged = File.ReadAllText(ConfigPath);

        Assert.Contains("\"newSetting\": true", merged);
        Assert.Contains("// \"warSails\": true,", merged);
        Assert.Single(Regex.Matches(merged, "\"warSails\""));
    }
}
