using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Services;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.CLI.Commands
{

	[CliCommand(
	Name = "sync",
	Description = "Generates an AutoSync class",
	Parent = typeof(RootCliCommand)
	)]
	public class GenerateAutoSyncCommand : GenerateCommandBase<AutoSyncTemplateModel>
	{
		public GenerateAutoSyncCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}

		[CliOption(Name = "--fields",
		Description = "Specify the name of fields to AutoSync",
		AllowMultipleArgumentsPerToken = true)]
		public string[] FieldsOption { get; set; } = Array.Empty<string>();

		[CliOption(Name = "--props",
		Description = "Specify the name of properties to AutoSync",
		AllowMultipleArgumentsPerToken = true)]
		public string[] PropsOption { get; set; } = Array.Empty<string>();

		[CliOption(Name = "--cols",
		Description = "Specify the name of collections to AutoSync",
		AllowMultipleArgumentsPerToken = true)]
		public string[] CollectionsOption { get; set; } = Array.Empty<string>();

	}

	//   public static class GenerateAutoSyncCommand
	//{
	//	public static void InitializeCommand(RootCommand rootCommand)
	//	{
	//		var typeNameOpt = new Option<string>("--typeName", "Specify the fully qualified name of the type using the format: '<namespace>.<type name>, <assembly name>'. Example: 'TaleWorlds.CampaignSystem.Siege.BesiegerCamp, TaleWorlds.CampaignSystem'")
	//		{
	//			IsRequired = true
	//		};

	//		var fieldsOpt = new Option<string[]>("--fields", "Specify the name of fields to AutoSync")
	//		{
	//			AllowMultipleArgumentsPerToken = true,
	//			Arity = ArgumentArity.OneOrMore
	//		};

	//		var propsOpt = new Option<string[]>("--props", "Specify the name of the properties to AutoSync")
	//		{
	//			AllowMultipleArgumentsPerToken = true,
	//			Arity = ArgumentArity.OneOrMore
	//		};

	//		var colsOpt = new Option<string[]>("--cols", "Specify the name of the collections to AutoSync")
	//		{
	//			AllowMultipleArgumentsPerToken = true,
	//			Arity = ArgumentArity.OneOrMore
	//		};

	//		var registryCommand = new Command("autosync", "Generates an AutoSync class")
	//		{
	//			typeNameOpt,
	//			fieldsOpt,
	//			propsOpt,
	//			colsOpt
	//		};

	//		registryCommand.SetHandler(async (typeName, fieldsOpt, propsOpt, colsOpt) =>
	//		{
	//			await GenerateAutoSync(typeName, fieldsOpt, propsOpt, colsOpt);
	//		},
	//		typeNameOpt,
	//		fieldsOpt,
	//		propsOpt,
	//		colsOpt
	//		);

	//		rootCommand.AddCommand(registryCommand);
	//	}

	//	private static async Task GenerateAutoSync(string typeName, string[]? fields = null, string[]? props = null, string[]? cols = null)
	//	{
	//		var typeInfo = ReflectionHelper.GetServiceTypeInfo(typeName, props, fields, cols);
	//		var registryTemplateModel = new AutoSyncTemplateModel(typeInfo);
	//		var scaffolder = new ScaffoldingService();
	//		await scaffolder.Generate(registryTemplateModel);
	//	}
	//}
}
