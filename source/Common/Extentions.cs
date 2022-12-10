using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class Extentions
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
