using System;
using System.Collections.Generic;

namespace GameInterface.Services.Tournaments;

public interface ITournamentNativeRemovalAuthorization : IGameAbstraction
{
    IDisposable Authorize(string townId);
    bool IsAuthorized(string townId);
}

internal sealed class TournamentNativeRemovalAuthorization : ITournamentNativeRemovalAuthorization
{
    private readonly object gate = new();
    private readonly Dictionary<string, int> authorizationCounts = new(StringComparer.Ordinal);

    public IDisposable Authorize(string townId)
    {
        lock (gate)
        {
            authorizationCounts.TryGetValue(townId, out int count);
            authorizationCounts[townId] = count + 1;
        }
        return new AuthorizationScope(this, townId);
    }

    public bool IsAuthorized(string townId)
    {
        lock (gate)
            return townId != null && authorizationCounts.ContainsKey(townId);
    }

    private void Release(string townId)
    {
        lock (gate)
        {
            if (!authorizationCounts.TryGetValue(townId, out int count))
                return;
            if (count <= 1)
                authorizationCounts.Remove(townId);
            else
                authorizationCounts[townId] = count - 1;
        }
    }

    private sealed class AuthorizationScope : IDisposable
    {
        private TournamentNativeRemovalAuthorization owner;
        private readonly string townId;

        public AuthorizationScope(TournamentNativeRemovalAuthorization owner, string townId)
        {
            this.owner = owner;
            this.townId = townId;
        }

        public void Dispose()
        {
            TournamentNativeRemovalAuthorization current = owner;
            owner = null;
            current?.Release(townId);
        }
    }
}