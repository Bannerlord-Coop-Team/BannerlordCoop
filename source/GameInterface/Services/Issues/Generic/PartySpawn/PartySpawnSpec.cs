using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.PartySpawn;

public delegate bool TryCaptureForForwarding<TQuest, TCapture>(TQuest quest, out TCapture captured);

public sealed record PartySpawnSpec<TQuest, TCapture, TSpawned>(
    PartySpawnTrigger Trigger,
    Func<TQuest, bool> AlreadySpawnedCheck,
    TryCaptureForForwarding<TQuest, TCapture> TryCaptureForForwarding,
    Action<Hero, TCapture> ForwardSpawnRequest,
    Func<Hero, TCapture, TSpawned> CreateOnServer,
    Action<TQuest, TSpawned> ApplySpawnResult
) where TQuest : QuestBase;
