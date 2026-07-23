using GameInterface.Services.Tournaments.Data;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Tournaments;

public static class TournamentDescriptorSeedAllocator
{
    public static int ResolveUniqueSeed(
        string characterId,
        int preferredSeed,
        IEnumerable<TournamentContestantData> contestants)
    {
        var usedSeeds = new HashSet<int>((contestants ?? Enumerable.Empty<TournamentContestantData>())
            .Where(contestant => contestant != null && contestant.CharacterId == characterId)
            .Select(contestant => contestant.DescriptorSeed));
        int candidate = preferredSeed < 0 ? 0 : preferredSeed;
        while (usedSeeds.Contains(candidate))
            candidate = candidate == int.MaxValue ? 0 : candidate + 1;
        return candidate;
    }
}
