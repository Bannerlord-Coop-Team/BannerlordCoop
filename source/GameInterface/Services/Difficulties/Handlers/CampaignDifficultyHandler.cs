using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.GameState.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Difficulties.Handlers;

/// <summary>
/// Applies the mod-config.json "difficulty" block to the hosted world once the
/// campaign is up (the CampaignOptions statics silently no-op without one).
/// Hosting side only: the CampaignOptions values live IN the save, so joining
/// clients inherit them with the join-time save transfer and autosaves persist
/// them — a client applying its own file would fight the server's world. A
/// configured key overrides the loaded world on every boot; an absent key leaves
/// the save's value alone. playerReceivedDamage is different twice over: it is
/// engine config (BannerlordConfig), not save state, and the mod pushes the
/// host's value to every client as a join-time ServerOption — on a HEADLESS host
/// (BANNERLORD_USER_DIR set) the profile defaults it to VeryEasy, which would
/// force quarter damage on every player, so unset there means Realistic. On a
/// graphical host the player's own setting stays in charge unless configured.
/// (Ported from the dedicated server's CampaignDifficulty, which owned this
/// while the keys lived in server-config.json.)
/// </summary>
internal class CampaignDifficultyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignDifficultyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IModConfig modConfig;

    public CampaignDifficultyHandler(IMessageBroker messageBroker, IModConfig modConfig)
    {
        this.messageBroker = messageBroker;
        this.modConfig = modConfig;
        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    internal void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        if (ModInformation.IsClient) return;

        Apply();
    }

    /// <summary>Internal (not private) so tests can re-apply on demand.</summary>
    internal void Apply()
    {
        if (Campaign.Current?.Options == null)
        {
            Logger.Information("campaign difficulty NOT applied — the loaded game has no campaign options");
            return;
        }

        DifficultyConfigData config = modConfig.Data.Difficulty ?? new DifficultyConfigData();
        var effective = new List<string>();

        // Unset on a headless host still means Realistic — see the class summary.
        DifficultyLevel? playerReceivedDamage = config.PlayerReceivedDamage;
        if (playerReceivedDamage == null && IsHeadlessHost)
        {
            playerReceivedDamage = DifficultyLevel.Realistic;
        }

        ApplyOption("playerReceivedDamage", playerReceivedDamage, effective,
            (CampaignOptions.Difficulty)BannerlordConfig.PlayerReceivedDamageDifficulty,
            v => BannerlordConfig.PlayerReceivedDamageDifficulty = (int)v);
        ApplyOption("playerTroopsReceivedDamage", config.PlayerTroopsReceivedDamage, effective,
            CampaignOptions.PlayerTroopsReceivedDamage, v => CampaignOptions.PlayerTroopsReceivedDamage = v);
        ApplyOption("combatAIDifficulty", config.CombatAIDifficulty, effective,
            CampaignOptions.CombatAIDifficulty, v => CampaignOptions.CombatAIDifficulty = v);
        ApplyOption("recruitmentDifficulty", config.RecruitmentDifficulty, effective,
            CampaignOptions.RecruitmentDifficulty, v => CampaignOptions.RecruitmentDifficulty = v);
        ApplyOption("playerMapMovementSpeed", config.PlayerMapMovementSpeed, effective,
            CampaignOptions.PlayerMapMovementSpeed, v => CampaignOptions.PlayerMapMovementSpeed = v);
        ApplyOption("stealthAndDisguiseDifficulty", config.StealthAndDisguiseDifficulty, effective,
            CampaignOptions.StealthAndDisguiseDifficulty, v => CampaignOptions.StealthAndDisguiseDifficulty = v);
        ApplyOption("persuasionSuccessChance", config.PersuasionSuccessChance, effective,
            CampaignOptions.PersuasionSuccessChance, v => CampaignOptions.PersuasionSuccessChance = v);
        ApplyOption("clanMemberDeathChance", config.ClanMemberDeathChance, effective,
            CampaignOptions.ClanMemberDeathChance, v => CampaignOptions.ClanMemberDeathChance = v);
        ApplyOption("battleDeath", config.BattleDeath, effective,
            CampaignOptions.BattleDeath, v => CampaignOptions.BattleDeath = v);

        // Positive polarity for the operator: birthAndDeath=true means the world
        // ages (births, natural deaths) — the engine flag is the inverse.
        ApplyToggle("birthAndDeath", config.BirthAndDeath, effective,
            !CampaignOptions.IsLifeDeathCycleDisabled, v => CampaignOptions.IsLifeDeathCycleDisabled = !v);
        ApplyToggle("autoAllocateClanMemberPerks", config.AutoAllocateClanMemberPerks, effective,
            CampaignOptions.AutoAllocateClanMemberPerks, v => CampaignOptions.AutoAllocateClanMemberPerks = v);

        Logger.Information("difficulty (effective): {Effective}", string.Join(" ", effective));
    }

    /// <summary>Headless/container hosts set BANNERLORD_USER_DIR (the same marker
    /// CoopSaveManager keys its data root on); graphical hosts never do.</summary>
    private static bool IsHeadlessHost =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR")) == false;

    /// <summary>By name, not ordinal — the game enum's numbering is not ours to rely on.</summary>
    private static CampaignOptions.Difficulty ToCampaign(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.VeryEasy: return CampaignOptions.Difficulty.VeryEasy;
            case DifficultyLevel.Easy: return CampaignOptions.Difficulty.Easy;
            default: return CampaignOptions.Difficulty.Realistic;
        }
    }

    private static void ApplyOption(string key, DifficultyLevel? configuredLevel, List<string> effective,
        CampaignOptions.Difficulty current, Action<CampaignOptions.Difficulty> set)
    {
        if (configuredLevel != null)
        {
            CampaignOptions.Difficulty configured = ToCampaign(configuredLevel.Value);
            if (configured != current)
            {
                set(configured);
                Logger.Information("difficulty: {Key} {Current} -> {Configured} (mod-config.json)", key, current, configured);
                current = configured;
            }
        }
        effective.Add(key + "=" + current);
    }

    private static void ApplyToggle(string key, bool? configured, List<string> effective,
        bool current, Action<bool> set)
    {
        if (configured != null && configured.Value != current)
        {
            set(configured.Value);
            Logger.Information("difficulty: {Key} {Current} -> {Configured} (mod-config.json)", key, OnOff(current), OnOff(configured.Value));
            current = configured.Value;
        }
        effective.Add(key + "=" + OnOff(current));
    }

    private static string OnOff(bool value) => value ? "on" : "off";
}
