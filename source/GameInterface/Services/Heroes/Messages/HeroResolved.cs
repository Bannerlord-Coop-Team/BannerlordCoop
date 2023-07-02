using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;

public record HeroResolved : IResponse
{
    public string HeroId { get; }
    public HeroResolved(string heroId)
    {
        HeroId = heroId;
    }
}