# EAVFW.Extensions.Manifest.ManifestEnricherTool
A DotNET CLI tool for expanding and enriching manifest.json

### Install the tool locally

1. Build the project in **Release**
2. `dotnet tool install --add-source .\EAVFW.Extensions.Manifest.ManifestEnricherTool\nupkg EAVFW.Extensions.Manifest.ManifestEnricherTool`
3. Activate the tool `dotnet eavfw-manifest`

#### Update tool

1. `dotnet pack -c Release -p:PackageVersion=<higher-version>`
2. Update `.config/dotnet-tools.json`
3. `dotnet tool restore`

The new version can be used.

_Read more at [https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools)_.

## Install the tool globally



### Install the tool from NuGet
Coming soon...
