using ProtoBuf;

namespace GameInterface.Services.CampaignService.Data;

/// <summary>
/// Options configured outside of the campaign options that are not transferred as part of the save game.
/// Add other options that should only be managed by the server to this data structure.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class ServerOptions
{
    [ProtoMember(1)]
    public readonly int PlayerReceivedDamage;

    public ServerOptions(int playerReceivedDamage)
    {
        PlayerReceivedDamage = playerReceivedDamage;
    }
}
