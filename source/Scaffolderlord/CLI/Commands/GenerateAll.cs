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
	Name = "all",
	Description = "Generates all classes(registry,sync)",
	Parent = typeof(RootCliCommand)
	)]
	public class GenerateAllCommand : GenerateAutoSyncCommand
	{
		public GenerateAllCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}

		protected override ITemplateModel GetTemplateModel() => null;

		// This is kinda dumb but I don't know any better way
		public override async Task RunAsync()
		{
			var registryCommand = new GenerateRegistryCommand(scaffolder);
			var syncCommand = new GenerateAutoSyncCommand(scaffolder);
			this.PropagateCliArgumentsAndOptions(registryCommand, syncCommand);
			await registryCommand.RunAsync();
			await syncCommand.RunAsync();
		}
	}
}
