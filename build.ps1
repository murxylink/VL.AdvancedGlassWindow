param([switch]$Pack, [string]$GammaDirectory = 'C:\Program Files\vvvv\vvvv_gamma_7.4-win-x64')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $env:MSBuildEnableWorkloadResolver = 'false'
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
    dotnet build src/VL.AdvancedGlassWindow.csproj -c Release --configfile NuGet.Config "-p:GammaDirectory=$GammaDirectory"
    if ($LASTEXITCODE -ne 0) { throw 'DLL build failed' }
    New-Item -ItemType Directory -Path 'lib/net8.0' -Force | Out-Null
    Copy-Item -LiteralPath 'src/bin/Release/net8.0/VL.AdvancedGlassWindow.dll' -Destination 'lib/net8.0/VL.AdvancedGlassWindow.dll' -Force
    if ($Pack) {
        $spec = Join-Path $PSScriptRoot 'VL.AdvancedGlassWindow.nuspec'
        dotnet pack src/VL.AdvancedGlassWindow.csproj -c Release --no-build --no-restore "-p:NuspecFile=$spec" "-p:NuspecBasePath=$PSScriptRoot" -p:NoDefaultExcludes=true -o dist
        if ($LASTEXITCODE -ne 0) { throw 'Package build failed' }
        & (Join-Path $PSScriptRoot 'verify-package.ps1')
    }
} finally { Pop-Location }
