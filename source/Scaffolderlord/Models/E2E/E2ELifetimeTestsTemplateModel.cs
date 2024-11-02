using Scaffolderlord.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.E2E
{
	public class E2ELifetimeTestsTemplateModel : BaseTemplateModel, ITemplateModel
	{
		public string TypeName { get; }
		public string? Namespace { get; }
		public IEnumerable<string> Usings { get; }
		public IEnumerable<FieldInfo> Fields { get; }

		public string TemplateFileName => @"E2E\E2ELifetimeTestsTemplate.cshtml";

		public string GetOutputPath() => GetRelativeDirectory(@$"E2E.Tests\Services\{TypeName}s\{TypeName}LifetimeTests.cs");

		public E2ELifetimeTestsTemplateModel(ServiceTypeInfo serviceInfo)
		{
			TypeName = serviceInfo.Type.Name;
			Namespace = $"E2E.Tests.Services.{serviceInfo.Type.Name}s;";
			Fields = serviceInfo.Fields;
            Usings = GetUsings(serviceInfo.Type);
        }
	}
}
