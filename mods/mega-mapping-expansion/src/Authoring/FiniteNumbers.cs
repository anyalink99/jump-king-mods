using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    internal static class FiniteNumbers
    {
        // DTO traversal also covers new nested authoring types and the cache load path.
        internal static void Validate(object value, string path)
        {
            if (value == null || value is string) return;
            if (value is float)
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number))
                    throw new InvalidDataException(path + ": expected a finite number, got " + number);
                return;
            }
            if (value is double)
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidDataException(path + ": expected a finite number, got " + number);
                return;
            }
            if (value.GetType().IsValueType) return;
            var list = value as IEnumerable;
            if (list != null)
            {
                int index = 0;
                foreach (object item in list) Validate(item, path + "[" + index++ + "]");
                return;
            }
            PropertyInfo identity = value.GetType().GetProperty("Id");
            if (identity != null) path += " id='" + identity.GetValue(value, null) + "'";
            foreach (PropertyInfo property in value.GetType().GetProperties())
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0
                    || property.IsDefined(typeof(XmlIgnoreAttribute), true)) continue;
                var attribute = (XmlAttributeAttribute)Attribute.GetCustomAttribute(property, typeof(XmlAttributeAttribute));
                string name = attribute == null ? property.Name : "@" + attribute.AttributeName;
                Validate(property.GetValue(value, null), path + "." + name);
            }
        }
    }
}
