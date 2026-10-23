using System;
using System.IO;

namespace MegaMappingExpansion.Construction
{
    internal static class SemanticIdentity
    {
        internal static readonly string Value=Read();
        private static string Read()
        {
            using(var stream=typeof(SemanticIdentity).Assembly.GetManifestResourceStream("MegaMapping.CompilerIdentity"))
            {
                if(stream==null)throw new InvalidDataException("Construction compiler identity is missing from the package");using(var reader=new StreamReader(stream))return reader.ReadToEnd();
            }
        }
    }
}
