using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
namespace MegaMappingExpansion.Construction
{
    internal sealed partial class RecipeCompiler
    {
        private static void ValidateDepth(XElement root)
        {
            if(root==null)throw new InvalidDataException("Scene XML has no root");var pending=new Stack<KeyValuePair<XElement,int>>();pending.Push(new KeyValuePair<XElement,int>(root,0));
            while(pending.Count>0){var item=pending.Pop();if(item.Value>64)throw new InvalidDataException("Scene XML exceeds depth 64");foreach(var child in item.Key.Elements())pending.Push(new KeyValuePair<XElement,int>(child,item.Value+1));}
        }
    }
}
