using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddSkillXpFromCrafting : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingHeroId;

    [ProtoMember(2)]
    public readonly float Xp;

    public NetworkAddSkillXpFromCrafting(string craftingHeroId, float xp)
    {
        CraftingHeroId = craftingHeroId;
        Xp = xp;
    }
}
