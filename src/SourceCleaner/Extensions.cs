using System.IO;

namespace SourceCleaner
{
    public static class Extensions
    {
        public static void RemoveReadOnly(this FileSystemInfo path)
        {
            path.Attributes &= FileAttributes.Normal;
        }

        public static void ApplyReadOnly(this FileSystemInfo path)
        {
            path.Attributes &= FileAttributes.ReadOnly;
        }
    }
}
