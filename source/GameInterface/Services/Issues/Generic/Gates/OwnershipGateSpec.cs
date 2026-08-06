using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.Gates;

public sealed record OwnershipGateSpec<TInstance>(Func<TInstance, Hero> QuestGiverSelector);
