using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterConverterGUI
{
    public enum Mode
    {
        Import = 0,
        Export,
        Build,
    }

    public static class Constants
    {
        private static readonly Dictionary<Mode, string> ModeTable = new Dictionary<Mode, string>()
        {
            { Mode.Import,  "import" },
            { Mode.Export,  "export" },
            { Mode.Build,   "build"  },
        };

        public static string GetArgumentText(Mode mode)
        {
            return ModeTable[mode];
        }

        public static string MasterFileExtension = ".xlsx";
    }
}
