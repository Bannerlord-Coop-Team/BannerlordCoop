using Scaffolderlord.Models;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.CLI.Commands
{
	public static class GenerateRegistryCommand
	{
		public static void InitializeCommand(RootCommand rootCommand)
		{
			var typeNameOption = new Option<string>("--typeName","Specify the fully qualified name of the type using the format: '<namespace>.<type name>, <assembly name>'. Example: 'TaleWorlds.CampaignSystem.Siege.BesiegerCamp, TaleWorlds.CampaignSystem'")
			{
				IsRequired = true
			};

			var registryCommand = new Command("registry", "Generates a Registry class")
			{
				typeNameOption
			};

			registryCommand.SetHandler(async (typeName) =>
			{
				await GenerateRegistry(typeName);
			}, typeNameOption);

			rootCommand.AddCommand(registryCommand);
		}

		private static async Task GenerateRegistry(string typeName)
		{
			var typeInfo = ReflectionHelper.GetServiceTypeInfo(typeName);
			var registryTemplateModel = new RegistryTemplateModel(typeInfo);
			var scaffolder = new Scaffolder();
			await scaffolder.Generate(registryTemplateModel);
		}
	}
}
