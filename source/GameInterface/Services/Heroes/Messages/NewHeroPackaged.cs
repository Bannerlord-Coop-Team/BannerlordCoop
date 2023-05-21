using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;

public record NewHeroPackaged : IResponse 
{
    public byte[] Package { get; }

    public NewHeroPackaged(byte[] package)
    {
        Package = package;
    }
}