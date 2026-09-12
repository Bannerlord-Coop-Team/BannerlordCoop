using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentDescriptorSeedAllocatorTests
{
    [Fact]
    public void ResolveUniqueSeed_AvoidsCollisionOnlyForSameCharacter()
    {
        var contestants = new[]
        {
            new TournamentContestantData("a", "troop", 7, null, "Troop", false, false, false, null),
            new TournamentContestantData("b", "other", 8, null, "Other", false, false, false, null)
        };

        Assert.Equal(8, TournamentDescriptorSeedAllocator.ResolveUniqueSeed("troop", 7, contestants));
        Assert.Equal(7, TournamentDescriptorSeedAllocator.ResolveUniqueSeed("other", 7, contestants));
        Assert.Equal(0, TournamentDescriptorSeedAllocator.ResolveUniqueSeed("troop", -1, contestants));
    }
}
