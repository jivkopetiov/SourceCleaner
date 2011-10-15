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

        private const string _markedFilesForDelete = "*.scc|*.vssscc|*.csproj.user|*.vspscc|*.suo|UpgradeLog.xml|.DS_Store";

        private const string _firstBatchOfDirectoriesForDeletion = "bin|obj";

        private const string _secondBatchOfMarkedDirectoriesForDeletion = "_ReSharper*|_UpgradeLog|.svn|_svn|.hg|pkg|pkgobj";

        private const string _sourceBindingPattern = @"\.(cs|vb)proj$|\.sln$";

        public Cleaner(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);

            _directory = directory;
            Recursive = true;
            ShouldRemoveTfsBindings = false;
            Force = true;
        }

        public bool Recursive { get; set; }

        public bool Force { get; set; }

        public bool ShouldRemoveTfsBindings { get; set; }

        public void CleanAll()
        {
            LogMessage("Cleaning directory " + _directory);
            LogMessage("");

            int deletedDirectories = DeleteDirectories(_firstBatchOfDirectoriesForDeletion);

            deletedDirectories += DeleteDirectories(_secondBatchOfMarkedDirectoriesForDeletion);

            int deletedFiles = DeleteFiles();

            int cleanedSolutionFiles = RemoveTfsBindingFromSolutionFiles();

            int cleanedProjectFiles = 0;

            if (ShouldRemoveTfsBindings)
                cleanedProjectFiles = RemoveTfsBindingFromProjectFiles();

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

        private int DeleteDirectories(string directoriesTemplate)
        {
            int counter = 0;
            foreach (var directory in GetDirectoriesToDelete(directoriesTemplate))
            {
                try
                {
                    if (directory.Name == "bin")
                    {
                        bool hasProjectFile = false;
                        foreach (var file in directory.Parent.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
                        {
                            if (file.Name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                file.Name.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                                file.Name.EndsWith(".modelproj", StringComparison.OrdinalIgnoreCase) ||
                                file.Name.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
                            {
                                hasProjectFile = true;
                                break;
                            }
                        }

                        if (!hasProjectFile)
                        {
                            LogMessage("Skipping " + directory.FullName + " because it doesn't seem to be a temp dir");
                            continue;
                        }
                    }

                    if (Force)
                        directory.RemoveReadOnly();

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

        private IEnumerable<DirectoryInfo> GetDirectoriesToDelete(string directoriesTemplate)
        {
            var dirInfo = new DirectoryInfo(_directory);

            foreach (string directoryPattern in directoriesTemplate.Split('|'))
            {
                var dirs = Recursive
                           ? dirInfo.GetDirectories(directoryPattern, SearchOption.AllDirectories)
                           : dirInfo.GetDirectories(directoryPattern, SearchOption.TopDirectoryOnly);

                foreach (var directory in dirs)
                    yield return directory;
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
                        file.RemoveReadOnly();

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

                var xmlDoc = XDocument.Load(file.FullName);
                var ns = xmlDoc.GetXmlns();
                var firstPropertyGroup = xmlDoc.Root.Element(ns + "PropertyGroup");
                var nodesToDelete = firstPropertyGroup.Elements().Where(
                    node =>
                        node.Name == ns + "SccProjectName" ||
                        node.Name == ns + "SccLocalPath" ||
                        node.Name == ns + "SccAuxPath" ||
                        node.Name == ns + "SccProvider").ToArray();

                if (nodesToDelete.Any())
                {
                    for (int nodeCounter = 0; nodeCounter < nodesToDelete.Length; nodeCounter++)
                        nodesToDelete[nodeCounter].Remove();

                    xmlDoc.Save(file.FullName);

                    LogSuccess(string.Format("Removed source control bindings from {0}", file.FullName));
                    counter++;
                }
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