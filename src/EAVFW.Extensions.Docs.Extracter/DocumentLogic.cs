using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            var assembly = LoadAssembly(pluginInfo);

            var implementingTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            var plugins =
                from implementingType in implementingTypes
                let pluginRegistrations = implementingType.GetCustomAttributes<PluginRegistrationAttribute>()
                let _interface = implementingType.GetInterfaces()
                    .FirstOrDefault(i => i.GenericTypeArguments.Length == 2)
                select new PluginDocumentation
                {
                    PluginRegistrations = pluginRegistrations,
                    Name = implementingType.Name,
                    Summary = implementingType.GetDocumentation(),
                    Context = _interface.GetGenericArguments().First(),
                    Entity = _interface.GetGenericArguments().Last()
                };

            return plugins;
        }

        private static Assembly LoadAssembly(PluginInfo pluginInfo)
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
            return assembly;
        }

        /// <inheritdoc/>
        public IEnumerable<string> ExtractWizardDocumentation(FileInfo manifestFile, PluginInfo pluginInfo)
        {
            var assembly = LoadAssembly(pluginInfo);

            // Preloading types to easily query for documentation
            var workflows =
                from type in assembly.GetTypes()
                //     where type.IsAbstract && typeof(Workflow).IsAssignableFrom(type)
                select type;

            // Load manifest


            using var openStream = manifestFile.OpenRead();
            var jsonManifest = JsonDocument.ParseAsync(openStream).Result;

            // Find Wizards
            var simpleManifest = new Dictionary<string, Entity>();
            ExtractEntitiesWithWizards(jsonManifest.RootElement, simpleManifest);


            // Glorified for loop?
            var tabsWithWorkflows =
                from entity in simpleManifest
                from wizard in entity.Value.Wizards
                from tabs in wizard.Value.Tabs
                where tabs.Value.OnTransitionOut?.Workflow != null && tabs.Value.OnTransitionIn?.Workflow != null
                select tabs.Value;

            foreach (var tabsWithWorkflow in tabsWithWorkflows)
            {
                if (!string.IsNullOrWhiteSpace(tabsWithWorkflow?.OnTransitionOut?.Workflow))
                {
                    var workflow = tabsWithWorkflow.OnTransitionOut.Workflow!;
                    // look for workflow in types?
                    Console.WriteLine(workflow);
                }
            }

            foreach (var (key, wizard) in simpleManifest)
            {
                var t = JsonSerializer.Serialize(wizard, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                Console.WriteLine(t);
            }


            // Generate Wizard object


            // Return Wizard object
            return new List<string>();
        }

        private void ExtractEntitiesWithWizards(JsonElement element, IDictionary<string, Entity> entities)
        {
            if (element.ValueKind != JsonValueKind.Object) return;

            foreach (var property in element.EnumerateObject())
            {
                if (!property.NameEquals("entities") || property.Value.ValueKind != JsonValueKind.Object) continue;

                var localEntities =
                    JsonSerializer.Deserialize<Dictionary<string, Entity>>(property.Value.GetRawText(),
                        new JsonSerializerOptions
                        {
                            Converters = { new TabConverter() }
                        });

                if (localEntities == null) return;

                foreach (var (key, value) in localEntities.Where(x => x.Value?.Wizards?.Any() ?? false))
                {
                    entities[key] = value;
                }
            }
        }
    }
}
