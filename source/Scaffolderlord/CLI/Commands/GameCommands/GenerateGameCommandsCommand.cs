using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Models.Lifetime;
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
		Name = "commands",
		Description = "Generates all commands for setting the specified members of a type",
		Parent = typeof(RootCliCommand)
		)]
	public class GenerateCollectionCommand : GenerateAutoSyncCommand
	{
		public GenerateCollectionCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}

		protected override ITemplateModel GetTemplateModel() => null;

		public override async Task RunAsync()
		{
			throw new NotImplementedException();
		}
	}
}
