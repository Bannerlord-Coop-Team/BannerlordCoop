using System.Linq;
using System.Text;

namespace IntroServer.Extensions
{
    public static class StringBuilderExtensions
    {
        public static void AppendJoin(this StringBuilder stringBuilder, char delimiter, object[] items)
        {
            for (int i = 0; i < items.Length - 1; i++)
            {
                stringBuilder.Append(items[i].ToString());
                stringBuilder.Append(delimiter);
            }

            stringBuilder.Append(items.Last());
        }
    }
}
