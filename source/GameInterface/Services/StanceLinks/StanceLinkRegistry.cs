using GameInterface.Services.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks;


/// <summary>
/// Registry manager for <see cref="StanceLink"/>
/// </summary>
internal class StanceLinkRegistry : RegistryBase<StanceLink>
{
	private const string StanceLinkIdPrefix = "CoopStanceLink";
	private static int InstanceCounter = 0;

	public StanceLinkRegistry (IRegistryCollection collection) : base(collection)
	{
	}

	public override void RegisterAll()
	{
		// Implement RegisterAll if needed
	}

	protected override string GetNewId(StanceLink obj)
	{
		return $"{StanceLinkIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
	}
}