# EAVFW.Extensions.Manifest.ManifestEnricherTool
A DotNET CLI tool for expanding and enriching manifest.json

### Install the tool locally

1. Build the project in **Release**
2. `dotnet tool install --add-source .\src\EAVFW.Extensions.Manifest.ManifestEnricherTool\nupkg EAVFW.Extensions.Manifest.ManifestEnricherTool`
3. Activate the tool `dotnet eavfw-manifest`

#### Update tool

1. `dotnet pack -c Release -p:PackageVersion=<higher-version>`
2. Update `.config/dotnet-tools.json`
3. `dotnet tool restore`

The new version can be used.

_Read more at [https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools)_.

## Install the tool globally

### Install the tool from NuGet

The latest version on NuGet can be installed globally by:
```sh
$ dotnet tool install --global EAVFW.Extensions.Manifest.ManifestEnricherTool
```
_Instructions to install a specific version can be found on [NuGet](https://www.nuget.org/packages/EAVFW.Extensions.Manifest.ManifestEnricherTool)._

Invoke the tool globally by:
```
$ eavfw-manifest
```
_This is contradictory to invoking the tool installed locally by `$ dotnet eavfw-manifest`._

#### Update tool

The tool can be updated globally by:
```
$ dotnet tool update --global EAVFW.Extensions.Manifest.ManifestEnricherTool
```

#### Uninstall tool

The tool can be uninstalled globally by:
```
$ dotnet tool uninstall --global EAVFW.Extensions.Manifest.ManifestEnricherTool
```
