using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.CLI.Commands
{
	[CliCommand(
	Name = "lifetime",
	Description = "Generates all lifetime classes for a type (patches, handler, messages)",
	Parent = typeof(RootCliCommand)
	)]
	public class GenerateLifetimeCommand : GenerateLifetimeHandlerCommand
	{
		public GenerateLifetimeCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}

		protected override ITemplateModel GetTemplateModel() => null;

		public override async Task RunAsync()
		{
			var commands = new ICliCommand[]
			{
				new GenerateLifetimePatchesCommand(scaffolder),
				new GenerateLifetimeHandlerCommand(scaffolder),
				new GenerateCreatedMessageCommand(scaffolder),
				new GenerateDestroyedMessageCommand(scaffolder),
				new GenerateNetworkCreateMessageCommand(scaffolder),
				new GenerateNetworkDestroyMessageCommand(scaffolder)
			};
			this.PropagateCliArgumentsAndOptions(commands);
			foreach (var command in commands) await command.RunAsync();

			PrintCommandSucceededMessage();
		}
	}

}
