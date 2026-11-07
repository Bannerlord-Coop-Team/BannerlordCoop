using System;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Lets one user action that changes many barter items publish a single offer.
internal interface IPlayerPartyOfferBatchTracker
{
    // Opens a batch until the returned scope is disposed.
    IDisposable Open(Action publish);

    // Holds an offer back when a batch is open, returns false when the caller should publish it.
    bool TryHoldBack();
}

// Keeps the batch state per client, because E2E clients share one process.
internal class PlayerPartyOfferBatchTracker : IPlayerPartyOfferBatchTracker
{
    private bool isOpen;
    private bool hasHeldBackOffer;

    // Opens a batch that remembers the previous state, so a nested batch leaves the outer one open.
    public IDisposable Open(Action publish)
    {
        var scope = new BatchScope(this, isOpen, publish);
        isOpen = true;
        return scope;
    }

    // Records that the open batch owes an offer.
    public bool TryHoldBack()
    {
        if (!isOpen) return false;

        hasHeldBackOffer = true;
        return true;
    }

    // Restores the previous state and publishes the held back offer once the outermost batch closes.
    private void Close(bool wasOpen, Action publish)
    {
        isOpen = wasOpen;
        if (isOpen || !hasHeldBackOffer) return;

        hasHeldBackOffer = false;
        publish();
    }

    // Closes its batch on dispose.
    private sealed class BatchScope : IDisposable
    {
        private readonly PlayerPartyOfferBatchTracker tracker;
        private readonly bool wasOpen;
        private readonly Action publish;

        // Captures what closing the batch needs.
        public BatchScope(PlayerPartyOfferBatchTracker tracker, bool wasOpen, Action publish)
        {
            this.tracker = tracker;
            this.wasOpen = wasOpen;
            this.publish = publish;
        }

        // Closes the batch.
        public void Dispose() => tracker.Close(wasOpen, publish);
    }
}
