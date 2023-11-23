# EAVFW Documentation Generator

The process of generating documentation for EAVFW is a two step process.

1. Extract information from metadata and code
2. Generate documentation from sources.

The process is divided in two steps and seperated by JSON files. The makes it possible to enrich the source files and to create a custom generator without extracting information twice. 

## Extract information

`eavfw docs extract -h`

The metadata extract for both plugins and wizards are saved as JSON files.

Plugins is a single file and `PluginDocumentation` can be used to deserialize the document.
Wizards are saved in a directory `wizards` where each JSON file is a `EntityDefinition` enrich with extracted documentation from Workflow.

OnTransitionIn, OnTransitionOut and Actions with workflows are enriched with `x-workflowSummary` with the doc-string for the given class.

## Generate information

The interface `IDocumentationGenerator` depicts how the source can be loaded and at the end written. `EAVFW.Extensions.Docs.Generator` implement a simple markdown generator, that writes all the content to a single markdown file. 

This is the default invoked in `eavfw docs generate`.
