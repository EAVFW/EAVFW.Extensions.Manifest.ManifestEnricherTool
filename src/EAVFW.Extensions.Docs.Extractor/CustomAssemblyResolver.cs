using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EAVFW.Extensions.Docs.Extractor
{
    public static class CustomAssemblyResolver
    {
        public static Dictionary<string, AssemblyInfo> Dictionary { get; set; } = new();

        public static Assembly? CustomAssemblyResolverEventHandler(object? sender, ResolveEventArgs args)
        {
            // Ignore missing resources
            if (args.Name.Contains(".resources"))
                return null;

            // check for assemblies already loaded
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null)
                return assembly;

            if (!Dictionary.TryGetValue(args.Name.Split(',').First(), out var assemblyInfo))
                return null;

            try
            {
                return Assembly.LoadFrom(assemblyInfo.Path);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
