using System;

namespace SourceCleaner
{
    public static class EntryPoint
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(
                    "No solution folder path was provided. " +
                    "Please provide a folder path as the first parameter to this executable.");
                return;
            }

            try
            {
                var cleaner = new Cleaner(args[0]);
                cleaner.CleanAll();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }
        }
    }
}