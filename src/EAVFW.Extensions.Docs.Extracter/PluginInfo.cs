using System;
using System.IO;

namespace EAVFW.Extensions.Docs.Extracter
{
    public struct PluginInfo
    {
        public PluginInfo(DirectoryInfo rootPath, FileInfo assemblyPath, string configuration, string framework)
        {
            RootPath = !rootPath.Exists
                ? throw new ArgumentException($"Directory {nameof(rootPath)} does not exists")
                : rootPath;
            AssemblyPath = !assemblyPath.Exists
                ? throw new ArgumentException($"File {nameof(assemblyPath)} does not exists")
                : assemblyPath;

            Configuration = string.IsNullOrWhiteSpace(configuration)
                ? throw new ArgumentNullException(configuration)
                : configuration;
            Framework = string.IsNullOrWhiteSpace(framework) ? throw new ArgumentNullException(framework) : framework;
        }

        public DirectoryInfo RootPath { get; }
        public FileInfo AssemblyPath { get; }
        public string Configuration { get; }
        public string Framework { get; }
    }
}
