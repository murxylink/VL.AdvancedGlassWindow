param([string]$Package = (Join-Path $PSScriptRoot 'dist/VL.AdvancedGlassWindow.1.0.0.nupkg'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package).Path)
try {
    $files = @('VL.AdvancedGlassWindow.vl', 'README.md',
        'help/HowTo Show Transparent Window.vl',
        'help/HowTo Show Transparent RenderWindow.vl',
        'help/HowTo Show Transparent Skia Window.vl',
        'lib/net8.0/VL.AdvancedGlassWindow.dll')
    foreach ($name in $files) {
        $entry = $archive.GetEntry($name)
        if (!$entry) { throw "Missing package file: $name" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        $source = if ($name -eq 'lib/net8.0/VL.AdvancedGlassWindow.dll') {
            Join-Path $PSScriptRoot 'src/bin/Release/net8.0/VL.AdvancedGlassWindow.dll'
        } else { Join-Path $PSScriptRoot $name }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $actual) {
            throw "Package bytes differ from source: $name"
        }
    }
    $entry = $archive.GetEntry('VL.AdvancedGlassWindow.nuspec')
    if (!$entry) { throw 'Missing package manifest' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $spec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($metadata.SelectSingleNode("*[local-name()='id']").InnerText -ne 'VL.AdvancedGlassWindow') { throw 'Wrong package ID' }
    if ($metadata.SelectSingleNode("*[local-name()='version']").InnerText -ne '1.0.0') { throw 'Wrong version' }
    if ($metadata.SelectNodes(".//*[local-name()='dependency']").Count) { throw 'Built-in Gamma libraries must not be restored from NuGet' }
    if (@($archive.Entries | Where-Object FullName -Match '(^|/)install\.ps1$').Count) { throw 'Installer must not be shipped' }
    if (@($archive.Entries | Where-Object FullName -Match '^help/.*\.vl$').Count -ne 3) { throw 'Expected exactly three help patches' }
    Write-Output 'Verified package identity, all three unchanged help patches, DLL, VL document and README.'
} finally { $archive.Dispose() }
