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
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.PlayerReceivedDamage);
        Assert.True(config.Difficulty.BirthAndDeath);
        Assert.Equal(1d, config.Network.MovementOutgoingMiBPerSecond);
        Assert.Equal(1d, config.Network.MovementIncomingMiBPerSecond);
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
        Assert.True(config.UnknownKeys == null || config.UnknownKeys.Count == 0);
        Assert.True(config.Difficulty.UnknownKeys == null || config.Difficulty.UnknownKeys.Count == 0);
        Assert.True(config.Network.UnknownKeys == null || config.Network.UnknownKeys.Count == 0);
    }

    [Fact]
    public void ConfiguredNetworkValuesBind()
    {
        File.WriteAllText(ConfigPath, @"{
  ""network"": {
    ""movementOutgoingMiBPerSecond"": 0.5,
    ""movementIncomingMiBPerSecond"": 2.0,
  },
}");

        var config = NewModConfig().Data;

        Assert.Equal(0.5d, config.Network.MovementOutgoingMiBPerSecond);
        Assert.Equal(2d, config.Network.MovementIncomingMiBPerSecond);
        Assert.True(config.Network.UnknownKeys == null || config.Network.UnknownKeys.Count == 0);
    }

    [Fact]
    public void ExistingCommentedDifficulty_IsMigratedInPlace_WithoutOverwritingUserValues()
    {
        File.WriteAllText(ConfigPath, @"{
  // keep this operator comment
  ""difficulty"": {
    // ""playerReceivedDamage"": ""Realistic"",
    ""battleDeath"": ""Easy"",
    // ""birthAndDeath"": true,
  },
  ""modOptions"": { ""autoPauseEnabled"": false },
}");

        var config = NewModConfig().Data;
        string migrated = File.ReadAllText(ConfigPath);

        Assert.Equal(DifficultyLevel.Realistic, config.Difficulty.PlayerReceivedDamage);
        Assert.Equal(DifficultyLevel.Easy, config.Difficulty.BattleDeath);
        Assert.True(config.Difficulty.BirthAndDeath);
        Assert.False(config.ModOptions.AutoPauseEnabled);
        Assert.Contains("// keep this operator comment", migrated);
        Assert.Contains("\"playerReceivedDamage\": \"Realistic\"", migrated);
        Assert.DoesNotContain("// \"playerReceivedDamage\"", migrated);
        Assert.Contains("\"battleDeath\": \"Easy\"", migrated);
    }

    [Fact]
    public void SiblingServerConfig_IsIgnoredDuringInPlaceMigration()
    {
        File.WriteAllText(ConfigPath, @"{
  ""difficulty"": {
    // ""battleDeath"": ""Easy"",
    // ""birthAndDeath"": true,
  },
  ""modOptions"": { ""autoPauseEnabled"": false },
}");
        File.WriteAllText(Path.Combine(tempDir, "server-config.json"), @"{
  ""battleDeath"": ""Realistic"",
  ""birthAndDeath"": false,
}");

        var config = NewModConfig().Data;

        Assert.Equal(DifficultyLevel.Easy, config.Difficulty.BattleDeath);
        Assert.True(config.Difficulty.BirthAndDeath);
        Assert.False(config.ModOptions.AutoPauseEnabled);
        Assert.Contains("Realistic", File.ReadAllText(Path.Combine(tempDir, "server-config.json")));
    }

    [Fact]
    public void MissingDifficultySettings_ReceiveDefaultsWithoutChangingActiveValues()
    {
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
        const string invalid = "{ \"difficulty\": false }";
        File.WriteAllText(ConfigPath, invalid);

        _ = NewModConfig().Data;

        Assert.Equal(invalid, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void OriginalShippedDifficultyBlock_MigratesEveryCommentedSetting_AndIsIdempotent()
    {
        File.WriteAllText(ConfigPath, @"{
  ""difficulty"": {
    // ""playerReceivedDamage"": ""Realistic"",
    // ""playerTroopsReceivedDamage"": ""VeryEasy"",
    // ""combatAIDifficulty"": ""VeryEasy"",
    // ""recruitmentDifficulty"": ""VeryEasy"",
    // ""playerMapMovementSpeed"": ""VeryEasy"",
    // ""stealthAndDisguiseDifficulty"": ""VeryEasy"",
    // ""persuasionSuccessChance"": ""VeryEasy"",
    // ""clanMemberDeathChance"": ""VeryEasy"",
    // ""battleDeath"": ""VeryEasy"",
    // ""birthAndDeath"": true,
    // ""autoAllocateClanMemberPerks"": false,
  },
  ""modOptions"": { ""clientsCanUseCheats"": true }
}");

        var config = NewModConfig().Data;
        string firstMigration = File.ReadAllText(ConfigPath);
        _ = NewModConfig().Data;
        string secondLoad = File.ReadAllText(ConfigPath);

        Assert.Equal(DifficultyLevel.Realistic, config.Difficulty.PlayerReceivedDamage);
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
        Assert.True(config.ModOptions.ClientsCanUseCheats);
        Assert.DoesNotContain("// \"", firstMigration);
        Assert.Equal(firstMigration, secondLoad);
    }

    [Fact]
    public void MalformedConfig_IsNotChangedByMigration()
    {
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
        Assert.Equal(LordDefectionRetryMode.Vanilla, options.LordDefectionRetries);
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
        Assert.Equal(DifficultyLevel.VeryEasy, config.Difficulty.CombatAIDifficulty);
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
