using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Kingdoms.Patches;


/// <summary>
/// Patches for managing lifetime of <see cref="Kingdom"/> objects.
/// </summary>
[HarmonyPatch(typeof(Kingdom))]
internal class KingdomLifetimePatches
{
	private static readonly ILogger Logger = LogManager.GetLogger<KingdomLifetimePatches>
	();

	[HarmonyPatch(typeof(Kingdom))]
	[HarmonyPatch(MethodType.Constructor)]
	[HarmonyPrefix]
	private static bool ConstructorPrefix(ref Kingdom __instance)
	{
		// Call original if we call this function
		if (CallOriginalPolicy.IsOriginalAllowed()) return true;

		if (ModInformation.IsClient)
		{
			Logger.Error("Client created unmanaged {name}\n"
			+ "Callstack: {callstack}", typeof(Kingdom), Environment.StackTrace);
			return true;
		}

		var message = new KingdomCreated(__instance);

		MessageBroker.Instance.Publish(__instance, message);

		return true;
	}
}
