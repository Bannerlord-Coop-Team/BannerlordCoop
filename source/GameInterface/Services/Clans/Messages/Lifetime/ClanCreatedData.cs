using ProtoBuf;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Data required for clan creation
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class ClanCreatedData
{
    [ProtoMember(1)]
    public string ClanId { get; }

    public ClanCreatedData(string clanId)
    {
        ClanId = clanId;
    }
}
