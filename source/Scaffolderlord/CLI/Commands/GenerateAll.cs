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

		// This is kinda dumb but I just don't know better
		public override async Task RunAsync()
		{
			var registryCommand = new GenerateRegistryCommand(scaffolder)
			{
				OverwriteExistingFiles = this.OverwriteExistingFiles,
				TypeFullyQualifiedName = this.TypeFullyQualifiedName
			};
			var syncCommand = new GenerateAutoSyncCommand(scaffolder)
			{
				OverwriteExistingFiles = this.OverwriteExistingFiles,
				TypeFullyQualifiedName = this.TypeFullyQualifiedName,
				MembersOption = this.MembersOption
			};
			await registryCommand.RunAsync();
			await syncCommand.RunAsync();
		}
	}
}
