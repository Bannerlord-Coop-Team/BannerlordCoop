using GameInterface.Services.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms;


/// <summary>
/// Registry manager for <see cref="Kingdom"/>
/// </summary>
internal class KingdomRegistry : RegistryBase<Kingdom>
{
	private const string KingdomIdPrefix = "CoopKingdom";
	private static int InstanceCounter = 0;

	public KingdomRegistry (IRegistryCollection collection) : base(collection)
	{
	}

	public override void RegisterAll()
	{
		// Implement RegisterAll if needed
	}

	protected override string GetNewId(Kingdom obj)
	{
		return $"{KingdomIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
	}
}