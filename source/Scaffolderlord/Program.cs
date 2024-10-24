using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Scaffolderlord.Models;

namespace Scaffolderlord
{
	class Program
	{
		static async Task<int> Main(string[] args)
		{
			var rootCommand = new RootCommand("Scaffolding CLI tool for generating files for the BannerlordCoop project");

			InitializeCommands(rootCommand);

			return await rootCommand.InvokeAsync(args);
		}

		private static void InitializeCommands(RootCommand rootCommand)
		{
			var typeNameOption = new Option<string>(
	"--typeName",
	"Specify the fully qualified name of the type using the format: '<namespace>.<type name>, <assembly name>'. Example: 'TaleWorlds.CampaignSystem.Siege.BesiegerCamp, TaleWorlds.CampaignSystem'")
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
			},
	typeNameOption);

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
