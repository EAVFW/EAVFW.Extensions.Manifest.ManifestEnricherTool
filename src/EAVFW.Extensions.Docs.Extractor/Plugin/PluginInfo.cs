using System;
using System.IO;
using System.Linq;

namespace EAVFW.Extensions.Docs.Extractor
{
    public struct PluginInfo
    {
        public PluginInfo(DirectoryInfo rootPath, FileInfo assemblyPath)
        {
            var basePath = rootPath.Parent;
            var search = rootPath.Name;
            
            if (rootPath.FullName.Contains("**"))
            {
                basePath = new DirectoryInfo(rootPath.FullName.Split("**").First());
                search = rootPath.FullName[basePath.FullName.Length..];
            }
            else if (!rootPath.FullName.Contains('*'))
            {
                throw new ArgumentException("Probing path mu");
            }
            
            Search = search;
            RootPath = !(basePath?.Exists ?? false)
            ? throw new ArgumentException($"Directory {basePath.FullName} does not exists")
            : basePath;
            
            AssemblyPath = !assemblyPath.Exists
                ? throw new ArgumentException($"File {nameof(assemblyPath)} does not exists")
                : assemblyPath;
        }

        public DirectoryInfo RootPath { get; }
        public string Search { get; }
        public FileInfo AssemblyPath { get; }
    }
}
