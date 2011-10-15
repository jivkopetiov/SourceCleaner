using System.IO;
using System.Xml.Linq;

namespace SourceCleaner
{
    public static class Extensions
    {
        public static void RemoveReadOnly(this FileSystemInfo path)
        {
            path.Attributes &= ~(FileAttributes.ReadOnly);
        }

        public static void ApplyReadOnly(this FileSystemInfo path)
        {
            path.Attributes |= FileAttributes.ReadOnly;
        }

        public static XNamespace GetXmlns(this XDocument doc)
        {
            var attribute = doc.Root.Attribute("xmlns");
            if (attribute == null)
                return XNamespace.None;

            return attribute.Value;
        }
    }
}
