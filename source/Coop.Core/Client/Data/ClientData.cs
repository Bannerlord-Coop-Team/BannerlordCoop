using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Data
{
    internal class ClientData
    {
        public static string ValuesToString<T>(this HashSet<T> hashset)
        {
            StringBuilder stringBuilder = new StringBuilder();

            foreach(var value in hashset)
            {
                stringBuilder.AppendLine(value.ToString());
            }

            return stringBuilder.ToString();
        }
    }
}
