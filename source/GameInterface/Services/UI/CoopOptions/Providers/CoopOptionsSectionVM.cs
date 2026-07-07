using GameInterface.Services.UI.CoopOptions;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers;

public abstract class CoopOptionsSectionVM : ViewModel
{
    public abstract string Id { get; }

    public virtual bool CanApply => true;

    public abstract void Apply(string tabId, CoopOptionsData options);

    public virtual void AfterApply()
    {
    }
}
