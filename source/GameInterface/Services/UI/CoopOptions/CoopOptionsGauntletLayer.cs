using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace GameInterface.Services.UI.CoopOptions;

// Both the standalone options screen and the mission overlay tick the native popup here.
internal sealed class CoopOptionsGauntletLayer : GauntletLayer
{
    private readonly ICoopOptionsKeybinding keybinding;
    private readonly SpriteCategory sprites;
    private readonly ScreenBase owner;
    private readonly CoopOptionsVM options;
    private bool closed;

    public CoopOptionsGauntletLayer(ScreenBase owner, CoopOptionsVM options) : base("CoopOptionsUI", 100)
    {
        this.owner = owner;
        this.options = options;
        sprites = UIResourceManager.LoadSpriteCategory("ui_options");
        if (ContainerProvider.TryResolve<ICoopOptionsKeybinding>(out keybinding))
            keybinding.Attach(owner, options, () =>
            {
                if (owner.IsActive && ScreenManager.TopScreen == owner) ScreenManager.TrySetFocus(this);
            });
    }

    protected override void Tick(float dt)
    {
        base.Tick(dt);
        if (ScreenManager.TopScreen != owner) keybinding?.Cancel();
        else keybinding?.Tick();
    }

    protected override void OnDeactivate()
    {
        keybinding?.Cancel();
        base.OnDeactivate();
    }

    protected override void OnFinalize()
    {
        CloseKeybinding();
        base.OnFinalize();
    }

    public void CloseKeybinding()
    {
        if (closed) return;
        closed = true;
        keybinding?.Dispose();
        options.OnFinalize();
        sprites?.Unload();
    }
}
