using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class RecipeCompiler
    {
        private static string Local(string value,string instance)
        {
            if(value==null)return null;
            if(value.StartsWith("@",StringComparison.Ordinal))return instance+"."+value.Substring(1);
            foreach(string prefix in new[]{"variable:","node:","signal:"})if(value.StartsWith(prefix+"@",StringComparison.Ordinal))return prefix+instance+"."+value.Substring(prefix.Length+1);
            return value;
        }
        private void ResolvePorts(XElement root)
        {
            var aliases=new Dictionary<string,XElement>(StringComparer.Ordinal);
            foreach(var export in root.Elements("Exports").Elements("Export"))
            {
                string id=Required(export,"id"),role=Required(export,"role"),target=Required(export,"target");
                if(aliases.ContainsKey(id))throw new InvalidDataException("Duplicate recipe export: "+id);
                string section=role=="component"?"Components":role=="node"?"Graph":"";
                if(target!=id&&section!=""&&root.Elements(section).Elements().Any(e=>(string)e.Attribute("id")==id))throw new InvalidDataException(id+": export shadows a concrete "+role+"; give the internal item another ID");
                aliases.Add(id,export);
            }
            Func<string,string,HashSet<string>,string> resolve=null;
            resolve=(id,role,active)=>{
                if(string.IsNullOrEmpty(id))return id;XElement export;
                if(!aliases.TryGetValue(id,out export))return id;
                string actual=Required(export,"role"),target=Required(export,"target");
                if(actual!=role&&!(role=="signal"&&actual=="property"))throw new InvalidDataException(id+": expected a "+role+" export, got "+actual);
                if(target==id)return id;
                if(!active.Add(id)||active.Count>64)throw new InvalidDataException("Cyclic recipe export: "+id);
                string value=resolve(target,role,active);active.Remove(id);return value;
            };
            Func<string,string,string> port=(id,role)=>resolve(id,role,new HashSet<string>(StringComparer.Ordinal));
            Func<string,string> property=id=>{
                id=port(id,"property");int slash=(id??"").IndexOf('/');return slash<0?id:port(id.Substring(0,slash),"component")+id.Substring(slash);
            };
            foreach(var component in root.Elements("Components").Elements("Component"))
            {
                var descriptor=registry.Require(Required(component,"kind"));
                foreach(var value in component.Elements("Value"))
                {
                    var field=descriptor.Fields[descriptor.Index(Required(value,"name"))];string text=Required(value,"value",true);
                    if(field.Reference==ReferenceKind.Component)value.SetAttributeValue("value",port(text,"component"));
                    else if(field.Reference==ReferenceKind.Signal)value.SetAttributeValue("value",port(text,"signal"));
                    else if(field.Reference==ReferenceKind.Selection)value.SetAttributeValue("value",string.Join(";",text.Split(';').Select(token=>port(token,"component"))));
                }
            }
            foreach(var node in root.Elements("Graph").Elements("Node"))
            {
                var inputs=node.Attribute("inputs");if(inputs!=null)inputs.Value=string.Join(";",inputs.Value.Split(';').Select(id=>port(id,"node")));
                var source=node.Attribute("source");if(source==null||(string)node.Attribute("op")!="input")continue;
                if(source.Value.StartsWith("node:",StringComparison.Ordinal)){node.SetAttributeValue("op","pass");node.SetAttributeValue("inputs",port(source.Value.Substring(5),"node"));source.Remove();}
                else{string id=source.Value.StartsWith("signal:",StringComparison.Ordinal)?source.Value.Substring(7):source.Value;source.Value=port(id,"signal");}
            }
            foreach(var binding in root.Elements("Bindings").Elements("Bind"))
            {binding.SetAttributeValue("source",port(Required(binding,"source"),"node"));binding.SetAttributeValue("target",property(Required(binding,"target")));}
            foreach(var value in root.Elements("Effects").Elements("Effect").Elements("Set"))
            {value.SetAttributeValue("target",property(Required(value,"target")));var source=value.Attribute("source");if(source!=null)source.Value=port(source.Value,"node");}
            foreach(var condition in root.Descendants().Attributes("condition"))condition.Value=port(condition.Value,"node");
            foreach(var action in root.Descendants("Action"))
            {
                var source=action.Attribute("source");if(source!=null)source.Value=port(source.Value,"node");
                string op=(string)action.Attribute("op");if(op=="stamp"||op=="burst"||op=="impulse")action.SetAttributeValue("target",port(Required(action,"target"),"component"));
            }
            foreach(var node in root.Elements("Trees").Descendants("Node"))
            {var source=node.Attribute("source");if(source!=null)source.Value=port(source.Value,"node");}
            foreach(var export in aliases.Values)
            {string role=Required(export,"role");string target=port(Required(export,"target"),role);export.SetAttributeValue("target",role=="property"?property(target):target);}
        }
    }
}
