using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using EAVFramework.Plugins;

namespace EAVFW.Extensions.Docs.Extracter
{
    public class DocumentLogic : IDocumentLogic
    {
        private static Dictionary<string, AssemblyInfo> BuildAssemblyDictionary(IEnumerable<string> binDirectories)
        {
            var dictionary = new Dictionary<string, AssemblyInfo>();

            foreach (var directory in binDirectories)
            {
                var dlls = Directory.GetFiles(directory, "*.dll");
                foreach (var dll in dlls)
                {
                    var assemblyName = AssemblyName.GetAssemblyName(dll);

                    dictionary.TryAdd(assemblyName.Name!, new AssemblyInfo
                    {
                        Name = assemblyName.Name!,
                        Version = assemblyName.Version!.ToString(),
                        Path = dll
                    });
                }
            }

            return dictionary;
        }

        /// <inheritdoc />
        public IEnumerable<PluginDocumentation> ExtractPluginDocumentation(PluginInfo pluginInfo)
        {
            var subDirectories = pluginInfo.RootPath.EnumerateDirectories("*", SearchOption.AllDirectories);

            var directoriesWithBin =
                from d in subDirectories
                where d.FullName.EndsWith($"bin/{pluginInfo.Configuration}/{pluginInfo.Framework}")
                select d.FullName;

            CustomAssemblyResolver.Dictionary = BuildAssemblyDictionary(directoriesWithBin.AsQueryable());

            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += CustomAssemblyResolver.CustomAssemblyResolverEventHandler;

            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginInfo.AssemblyPath.FullName);

            var implementingTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            var plugins =
                from implementingType in implementingTypes
                let pluginRegistrations = implementingType.GetCustomAttributes<PluginRegistrationAttribute>()
                let interface2 = implementingType.GetInterfaces()
                    .FirstOrDefault(i => i.GenericTypeArguments.Length == 2)
                select new PluginDocumentation
                {
                    PluginRegistrations = pluginRegistrations,
                    Name = implementingType.Name,
                    Summary = implementingType.GetDocumentation(),
                    Context = interface2.GetGenericArguments().First(),
                    Entity = interface2.GetGenericArguments().Last()
                };

            return plugins;
        }
    }
}
