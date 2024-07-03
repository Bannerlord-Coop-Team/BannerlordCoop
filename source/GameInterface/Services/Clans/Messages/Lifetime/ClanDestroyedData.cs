using ProtoBuf;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Data required to destroy a clan over the network
/// </summary>
/// <param name="ClanId">StringId of clan</param>
/// <param name="Details">Details to why the clan is being destroyed</param>
[ProtoContract(SkipConstructor = true)]
record class ClanDestroyedData(string ClanId, int Details)
{
    [ProtoMember(1)]
    public string ClanId { get; } = ClanId;

    [ProtoMember(2)]
    public int Details { get; } = Details;
}
