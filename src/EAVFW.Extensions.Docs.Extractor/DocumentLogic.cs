using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using EAVFramework.Plugins;
using EAVFW.Extensions.Manifest.SDK;
using WorkflowEngine.Core;

namespace EAVFW.Extensions.Docs.Extractor
{
    public class DocumentLogic : IDocumentLogic
    {
        /// <inheritdoc />
        public IEnumerable<PluginDocumentation> ExtractPluginDocumentation(FileInfo assemblyInfo, string[] assemblies)
        {
            var assembly = LoadAssembly(assemblyInfo, assemblies);

            var implementingTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            var plugins =
                from implementingType in implementingTypes
                let pluginRegistrations = implementingType.GetCustomAttributes<PluginRegistrationAttribute>()
                let _interface = implementingType.GetInterfaces()
                    .FirstOrDefault(i => i.GenericTypeArguments.Length == 2)
                select new PluginDocumentation
                {
                    PluginRegistrations = pluginRegistrations.Select(x => new PluginRegistrationAttributeData
                        { Order = x.Order, Execution = x.Execution, Operation = x.Operation, Mode = x.Mode }),
                    Name = implementingType.Name,
                    Summary = implementingType.GetDocumentation(),
                    Context = new TypeInformation(_interface.GetGenericArguments().First()),
                    Entity = new TypeInformation(_interface.GetGenericArguments().Last())
                };

            return plugins;
        }

        private static Assembly LoadAssembly(FileInfo assemblyInfo, string [] assemblies)
        {
            var dictionary = new Dictionary<string, AssemblyInfo>();
            foreach (var file in assemblies)
            {
                var assemblyName = AssemblyName.GetAssemblyName(file);

                dictionary.TryAdd(assemblyName.Name!, new AssemblyInfo
                {
                    Name = assemblyName.Name!,
                    Version = assemblyName.Version!.ToString(),
                    Path = file
                });
            }

            CustomAssemblyResolver.Dictionary = dictionary;

            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += CustomAssemblyResolver.CustomAssemblyResolverEventHandler;

            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyInfo.FullName);
            return assembly;
        }

        /// <inheritdoc/>
        public Dictionary<string, EntityDefinition> ExtractWizardDocumentation(FileInfo manifestFile,
            FileInfo assemblyInfo, string[] assemblies)
        {
            var assembly = LoadAssembly(assemblyInfo, assemblies);

            // Preloading types to easily query for documentation
            var workflows = assembly.GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IWorkflow).IsAssignableFrom(type))
                .ToDictionary(x => x.Name, x => x);

            // Load manifest
            using var openStream = manifestFile.OpenRead();
            var jsonManifest = JsonDocument.ParseAsync(openStream).Result;

            // Find Wizards
            var simpleManifest = new Dictionary<string, EntityDefinition>();
            ExtractEntitiesWithWizards(jsonManifest.RootElement, simpleManifest);

            var tabs =
                (from entity in simpleManifest
                    from wizard in entity.Value.Wizards
                    from _tabs in wizard.Value.Tabs
                    select _tabs).AsEnumerable();

            // Glorified for loop?
            var tabsWithWorkflows =
                from tab in tabs
                where tab.Value.OnTransitionOut?.Workflow != null || tab.Value.OnTransitionIn?.Workflow != null
                select tab;

            foreach (var (key, value) in tabsWithWorkflows)
            {
                if (!string.IsNullOrWhiteSpace(value?.OnTransitionIn?.Workflow) &&
                    workflows.TryGetValue(value.OnTransitionIn.Workflow, out var type1))
                {
                    value.OnTransitionIn.AdditionalData["x-workflowSummary"] = type1.GetDocumentation();
                }

                if (!string.IsNullOrWhiteSpace(value?.OnTransitionOut?.Workflow) &&
                    workflows.TryGetValue(value.OnTransitionOut.Workflow, out var type2))
                {
                    value.OnTransitionOut.AdditionalData["x-workflowSummary"] = type2.GetDocumentation();
                }
            }
            
            var actionsWithWorkflows =
                from tab in tabs
                where tab.Value.Actions != null
                from action in tab.Value.Actions
                where action.Value.Workflow != null
                select action;

            foreach (var (key, value) in actionsWithWorkflows)
            {
                if (workflows.TryGetValue(value.Workflow, out var type))
                    value.AdditionalFields["x-workflowSummary"] = type.GetDocumentation();
            }

            return simpleManifest;
        }

        private void ExtractEntitiesWithWizards(JsonElement element, IDictionary<string, EntityDefinition> entities)
        {
            if (element.ValueKind != JsonValueKind.Object) return;

            foreach (var property in element.EnumerateObject())
            {
                if (!property.NameEquals("entities") || property.Value.ValueKind != JsonValueKind.Object) continue;

                var localEntities =
                    JsonSerializer.Deserialize<Dictionary<string, EntityDefinition>>(property.Value.GetRawText());

                if (localEntities == null) return;

                foreach (var (key, value) in localEntities.Where(x => x.Value?.Wizards?.Any() ?? false))
                {
                    entities[key] = value;
                }
            }
        }
    }
}
