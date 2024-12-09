using System.Text;

namespace Ryujinx.Common.Utilities
{

    public static class StringBuilderUtils
    {

        public static void AppendFormatLine(this StringBuilder sb, string format, params object[] args)
        {
            sb.AppendFormat(format, args);
            sb.AppendLine();
        }

    }

}
