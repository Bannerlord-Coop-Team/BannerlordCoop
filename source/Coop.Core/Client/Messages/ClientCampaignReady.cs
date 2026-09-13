using Common.Messaging;

namespace Coop.Core.Client.Messages;

/// <summary>The local client has completed campaign join synchronization.</summary>
public record ClientCampaignReady : IEvent;
