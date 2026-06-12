using Common.Messaging;

namespace Coop.Core.Client.Messages;

/// <summary>
/// Instructs the packet gate to replay the network packets it held while the client was
/// joining. Published only after every <see cref="ClientCampaignEntered"/> subscriber has
/// run, so the replayed packets can resolve objects those subscribers create — most
/// importantly the remote player heroes drained by RemotePlayerHeroHandler. Relying on
/// broker subscription order instead would leave the replay racing the hero drain.
/// </summary>
public record ReleaseNetworkBacklog : ICommand;
