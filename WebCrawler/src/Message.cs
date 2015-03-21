using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebCrawler
{
    [Flags]
    public enum Verbosity_Level
    {
        E_None = 0,
        E_Debug = 1 << 0,
        E_Warning = 1 << 1,
        E_Error = 1 << 2,
        E_Notice = 1 << 3
    }

    static class Message
    {
        public static Verbosity_Level Verbosity { set; get; }

        public static void ShowMessage(string message, Verbosity_Level verbosity, params object[] obj)
        {
            if ((Verbosity & verbosity) == verbosity)
                Console.WriteLine(message, obj);
        }
    }
}

