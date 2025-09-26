using System;
using System.IO;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public static class GitRepositoryLocator
    {
        /// <summary>
        /// Recursively searches for a .git directory from the given path upwards.
        /// Returns the path to the git repository root, or null if not found.
        /// </summary>
        public static string? FindGitRepositoryRoot(string startPath)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startPath));
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
