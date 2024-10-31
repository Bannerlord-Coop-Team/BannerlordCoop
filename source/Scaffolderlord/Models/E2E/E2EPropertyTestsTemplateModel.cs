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
	public class E2EPropertyTestsTemplateModel : BaseTemplateModel, ITemplateModel
	{
		public string TypeName { get; }
		public string? Namespace { get; }
		public IEnumerable<string> Usings { get; }
		public IEnumerable<PropertyInfo> Properties { get; }

		public string TemplateFileName => @"E2E\E2EPropertyTestsTemplate.cshtml";

		public string GetOutputPath() => GetRelativeDirectory(@$"E2E.Tests\Services\{TypeName}s\{TypeName}PropertyTests.cs");

		public IEnumerable<PropertyInfo> GetStructProps() => Properties.Where(x => x.PropertyType.IsStruct());
		public IEnumerable<PropertyInfo> GetClassProps() => Properties.Where(x => !x.PropertyType.IsStruct());

		public E2EPropertyTestsTemplateModel(ServiceTypeInfo serviceInfo)
		{
			TypeName = serviceInfo.Type.Name;
			Namespace = $"E2E.Tests.Services.{serviceInfo.Type.Name}s;";
			Properties = serviceInfo.Properties;
			Usings = new List<string>
			{
				serviceInfo.Type.Namespace!
			};

			var requiredNamespaces = Properties
				.Select(x => x.PropertyType.Namespace)
				.Where(x => x != null)
				.Distinct() ?? Array.Empty<string>();

			Usings = Usings.Concat(requiredNamespaces).Distinct();
		}
	}
}
