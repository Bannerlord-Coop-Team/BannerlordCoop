using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.GameMenus.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshGameMenu : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetHeroId;

    [ProtoMember(2)]
    public readonly string MenuName;

    public RefreshGameMenu(string targetHeroId, string menuName)
    {
        TargetHeroId = targetHeroId;
        MenuName = menuName;
    }
}