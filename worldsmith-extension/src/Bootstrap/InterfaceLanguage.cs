using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class InterfaceLanguage
    {
        internal static void Initialize()
        {
            // keep the user's number/date culture, only resource messages use English
            var english = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = english;
            System.Threading.Thread.CurrentThread.CurrentUICulture = english;
        }
    }
}
