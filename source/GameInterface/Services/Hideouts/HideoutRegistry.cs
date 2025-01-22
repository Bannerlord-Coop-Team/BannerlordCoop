using Common;
using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts;

/// <summary>
/// Registry class that assosiates <see cref="Hideout"/> and a <see cref="string"/> id
/// </summary>
internal class HideoutRegistry : RegistryBase<Hideout>
{
    private const string HideoutStringIdPrefix = "CoopHideout";
    private static int InstanceCounter = 0;

    public HideoutRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        var campaign = Campaign.Current;

        if (campaign == null)
        {
            Logger.Error("Unable to register objects when Campaign is null");
            return;
        }

        foreach (var hideout in campaign.AllHideouts)
        {
            RegisterExistingObject(hideout.StringId, hideout);
        }
    }

    protected override string GetNewId(Hideout hideout)
    {
        hideout.StringId = $"{HideoutStringIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        return hideout.StringId;
    }
}