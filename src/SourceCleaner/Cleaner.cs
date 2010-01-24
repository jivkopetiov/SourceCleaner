using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SourceCleaner
{
    public class Cleaner
    {
        private readonly string _directory;

        private const string _markedFilesForDelete = "*.scc|*.vssscc|*.user|*.vspscc|*.suo|UpgradeLog.xml";

        private const string _markedDirectoriesForDelete = "Debug|Release|bin|obj|_ReSharper*|_UpgradeLog|.svn|_svn";

        private const string _sourceBindingPattern = @"\.(cs|vb)proj$|\.sln$";

        public Cleaner(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);

            _directory = directory;
            Recursive = true;
            Force = true;
        }

        public bool Recursive { get; set; }

        public bool Force { get; set; }

        public void CleanAll()
        {
            LogMessage("Cleaning directory " + _directory);
            LogMessage("");

            int deletedDirectories = DeleteDirectories();

            int deletedFiles = DeleteFiles();

            int cleanedSolutionFiles = RemoveTfsBindingFromSolutionFiles();

            int cleanedProjectFiles = RemoveTfsBindingFromProjectFiles();

            if (deletedDirectories + cleanedProjectFiles + cleanedSolutionFiles + deletedFiles == 0)
            {
                LogWarning("Nothing to clean");
            }
            else
            {
                LogMessage("");
                if (deletedDirectories > 0)
                {
                    LogMessage("Deleted directories: " + deletedDirectories);
                }
                if (deletedFiles > 0)
                {
                    LogMessage("Deleted files: " + deletedFiles);
                }
                if (cleanedSolutionFiles > 0)
                {
                    LogMessage("Cleaned solution files: " + cleanedSolutionFiles);
                }
                if (cleanedProjectFiles > 0)
                {
                    LogMessage("Cleaned project files: " + cleanedProjectFiles);
                }
            }
        }

        private int DeleteDirectories()
        {
            int counter = 0;
            foreach (var directory in GetDirectoriesToDelete())
            {
                try
                {
                    if (Force)
                    {
                        directory.RemoveReadOnly();
                    }

                    directory.Delete(true);
                    LogSuccess(string.Format("Deleted directory {0}", directory.FullName));
                    counter++;
                }
                catch (Exception ex)
                {
                    LogError(ex.Message);
                }
            }
            return counter;
        }

        private IEnumerable<DirectoryInfo> GetDirectoriesToDelete()
        {
            var dirInfo = new DirectoryInfo(_directory);

            foreach (string directoryPattern in _markedDirectoriesForDelete.Split('|'))
            {
                var dirs = Recursive
                           ? dirInfo.GetDirectories(directoryPattern, SearchOption.AllDirectories)
                           : dirInfo.GetDirectories(directoryPattern, SearchOption.TopDirectoryOnly);

                foreach (var directory in dirs)
                {
                    yield return directory;
                }
            }
        }

        private int DeleteFiles()
        {
            int counter = 0;

            foreach (var file in GetFilesToDelete())
            {
                try
                {
                    if (Force)
                    {
                        file.RemoveReadOnly();
                    }

                    file.Delete();
                    LogSuccess(string.Format("Deleted file {0}", file.FullName));
                    counter++;
                }
                catch (Exception ex)
                {
                    LogError(ex.Message);
                }
            }

            return counter;
        }

        private IEnumerable<FileInfo> GetFilesToDelete()
        {
            foreach (string file in _markedFilesForDelete.Split('|'))
            {
                var files = Recursive
                            ? new DirectoryInfo(_directory).GetFiles(file, SearchOption.AllDirectories)
                            : new DirectoryInfo(_directory).GetFiles(file, SearchOption.TopDirectoryOnly);

                foreach (var chosenFile in files)
                {
                    yield return chosenFile;
                }
            }
        }

        private int RemoveTfsBindingFromProjectFiles()
        {
            var projectFiles = new DirectoryInfo(_directory).GetFiles("*.csproj", SearchOption.AllDirectories);

            int counter = 0;

            foreach (var file in projectFiles)
            {
                file.RemoveReadOnly();

                var fileAsXml = XDocument.Load(file.FullName);
                var sourceControlNodes = fileAsXml.Descendants().Where(
                    node =>
                        node.Name.LocalName == "SccProjectName" ||
                        node.Name.LocalName == "SccLocalPath" ||
                        node.Name.LocalName == "SccAuxPath" ||
                        node.Name.LocalName == "SccProvider");

                foreach (var node in sourceControlNodes)
                {
                    node.Remove();
                }

                fileAsXml.Save(file.FullName);

                LogSuccess(string.Format("Removed source control bindings from {0}", file.FullName));
                counter++;
            }

            return counter;
        }

        private int RemoveTfsBindingFromSolutionFiles()
        {
            var solutionFiles = new DirectoryInfo(_directory).GetFiles("*.sln", SearchOption.AllDirectories);

            int counter = 0;

            foreach (var file in solutionFiles)
            {
                file.RemoveReadOnly();

                string slnText = File.ReadAllText(file.FullName);
                var regex = new Regex(@"\s+GlobalSection\(TeamFoundationVersionControl\)[\w|\W]+?EndGlobalSection",
                                      RegexOptions.None);

                var match = regex.Match(slnText);
                if (match.Success)
                {
                    string resultingSlnText = slnText.Replace(match.Value, "");
                    File.WriteAllText(file.FullName, resultingSlnText);
                    LogSuccess(string.Format("Removed source control bindings from {0}", file.FullName));
                    counter++;
                }
            }

            return counter;
        }

        private void LogMessage(string message)
        {
            Console.WriteLine(message);
        }

        private void LogWarning(string warning)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(warning);
            Console.ResetColor();
        }

        private void LogError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
        }

        private void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}