using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Messages;

/// <summary>Requests a client-side visual for a party added by a headless server.</summary>
internal readonly record struct PartyVisualCreationRequested(MobileParty MobileParty) : IEvent;
