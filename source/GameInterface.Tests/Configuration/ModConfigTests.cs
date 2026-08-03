using GameInterface.Configuration;
using System;
using System.IO;
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

    public void Dispose()
    {
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
        Assert.Null(config.Difficulty.PlayerReceivedDamage);
        Assert.Null(config.Difficulty.BirthAndDeath);
    }

    [Fact]
    public void ShippedTemplate_ShipsEveryKeyCommentedOut()
    {
        // The template must stay behaviorally identical to no file at all, so
        // seeding into an existing deployment can never change a live world.
        File.Copy(ShippedTemplatePath, ConfigPath);

        var config = NewModConfig().Data;

        Assert.Null(config.Difficulty.PlayerReceivedDamage);
        Assert.Null(config.Difficulty.PlayerTroopsReceivedDamage);
        Assert.Null(config.Difficulty.CombatAIDifficulty);
        Assert.Null(config.Difficulty.BattleDeath);
        Assert.Null(config.Difficulty.BirthAndDeath);
        Assert.Null(config.Difficulty.AutoAllocateClanMemberPerks);
        Assert.True(config.UnknownKeys == null || config.UnknownKeys.Count == 0);
        Assert.True(config.Difficulty.UnknownKeys == null || config.Difficulty.UnknownKeys.Count == 0);
    }

    [Fact]
    public void SeededTemplate_ParsesBackAsAllDefaults()
    {
        _ = NewModConfig().Data;                     // seeds

        var config = NewModConfig().Data;            // fresh instance reads the seeded file

        Assert.Null(config.Difficulty.PlayerReceivedDamage);
        Assert.Null(config.Difficulty.PlayerTroopsReceivedDamage);
        Assert.Null(config.Difficulty.BirthAndDeath);
        Assert.Null(config.Difficulty.AutoAllocateClanMemberPerks);
        Assert.True(config.UnknownKeys == null || config.UnknownKeys.Count == 0);
        Assert.True(config.Difficulty.UnknownKeys == null || config.Difficulty.UnknownKeys.Count == 0);
    }

    /// <summary>
    /// The options a session runs on BEFORE any config is loaded (server) or received (client) must
    /// already be the documented defaults. <see cref="ModOptions"/> is a struct with no parameterless
    /// constructor — deliberately, so protobuf keeps deserializing it from a zeroed instance — so a
    /// plain <c>new ModOptions()</c> is just <c>default</c>: the property initializers never run and
    /// every option silently reads back false/0 (no auto-pause, no AI joining player battles, no
    /// looters, no smithing stamina, no clan-tier requirement for a kingdom). The un-loaded default
    /// must therefore be built through the real constructor from an all-absent config.
    /// </summary>
    [Fact]
    public void UnloadedModOptions_AreTheDocumentedDefaults_NotAZeroedStruct()
    {
        var options = ModConfigProvider.ModOptions;

        Assert.True(options.FastForwardEnabled);
        Assert.True(options.AutoPauseEnabled);
        Assert.False(options.ClientsCanUseCheats);
        Assert.True(options.GoldFoodInfluenceChangeInSettlements);
        Assert.Equal(GoldFoodChangeMode.OneDayMax, options.GoldFoodInfluenceChangeInBattles);
        Assert.False(options.GoldFoodInfluenceChangeForDisconnectedPlayers);
        Assert.Equal(24, options.PlayerBattleAiJoinWindowHours);
        Assert.True(options.SpeedLimitWhilePlayersInBattle);
        Assert.Equal(32, options.WandererLimit);
        Assert.False(options.WandererLimitScalesWithPlayers);
        Assert.Equal(4, options.PlayerKingdomClanTierRequired);
        Assert.True(options.SmithingStaminaRecoveryOutsideSettlements);
        Assert.Equal(0.1f, options.SmithingStaminaRecoveryMultiplier);
        Assert.Equal(1f, options.MaximumLootersMultiplier);
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
        Assert.Equal(ModConfigProvider.ModOptions, new ModOptions(config.ModOptions));
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
}");

        var config = NewModConfig().Data;

        Assert.Equal(DifficultyLevel.Realistic, config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(DifficultyLevel.Easy, config.Difficulty.PlayerTroopsReceivedDamage);
        Assert.False(config.Difficulty.BirthAndDeath);
        Assert.Null(config.Difficulty.CombatAIDifficulty);
    }

    [Fact]
    public void InvalidEnumValue_SkipsThatMember_KeepsTheRest()
    {
        File.WriteAllText(ConfigPath, @"{
  ""difficulty"": {
    ""playerReceivedDamage"": ""Medium"",
    ""battleDeath"": ""Easy""
  }
}");

        var config = NewModConfig().Data;

        Assert.Null(config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(DifficultyLevel.Easy, config.Difficulty.BattleDeath);
    }

    [Fact]
    public void BrokenJson_FallsBackToDefaults()
    {
        File.WriteAllText(ConfigPath, "{ this is not json");

        var config = NewModConfig().Data;

        Assert.NotNull(config);
        Assert.Null(config.Difficulty.PlayerReceivedDamage);
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
}
