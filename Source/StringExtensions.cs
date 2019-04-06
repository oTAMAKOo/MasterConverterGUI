using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterConverterGUI
{
    public static class StringExtensions
    {
        /// <summary> 文字列に指定されたキーワード群が含まれるか判定 </summary>
        public static bool IsMatch(this string text, string[] keywords)
        {
            keywords = keywords.Select(x => x.ToLower()).ToArray();

            if (!string.IsNullOrEmpty(text))
            {
                var tl = text.ToLower();
                var matches = 0;

                for (var b = 0; b < keywords.Length; ++b)
                {
                    if (tl.Contains(keywords[b])) ++matches;
                }

                if (matches == keywords.Length)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
