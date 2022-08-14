using GameInterface.Helpers;

namespace GameInterface
{
    public interface IGameInterface
    {
        IExampleGameHelper ExampleGameHelper { get; }
        ISaveLoadHelper SaveLoadHelper { get; }
    }
}
