using Common.Messaging;

namespace Coop.Core.Client.Messages;

/// <summary>
/// Raised locally once the client has fully entered the campaign (save loaded, objects registered, local hero
/// switched) and is therefore ready to instantiate remote player heroes. The persistent
/// <c>RemotePlayerHeroHandler</c> uses this to drain any heroes it deferred during loading and to start
/// instantiating further ones immediately.
/// </summary>
public record ClientCampaignEntered : IEvent;
