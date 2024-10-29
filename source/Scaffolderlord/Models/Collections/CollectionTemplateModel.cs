using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.Models.Collections
{
	public abstract class CollectionTemplateModel : BaseTemplateModel
	{
		public string TypeName { get; protected set; }
		public string? Namespace { get; protected set; }
		public IEnumerable<string> Usings { get; protected set; }

		public abstract string TemplateFileName { get; }

		public abstract string GetOutputPath();

		public MemberInfo Collection { get; set; }
		public string CollectionName => Collection.Name;

		public CollectionTemplateModel(MemberInfo selectedCollection)
		{
			Collection = selectedCollection;
		}
	}
}
