# Building VL.AdvancedGlassWindow

Requires Windows x64, vvvv gamma 7.4 and .NET SDK 8.0.413 (see global.json).
The native DLL references the libraries from your local Gamma installation.
Gamma's bundled libraries are not included in this repository or downloaded as package dependencies.

Build the DLL and the NuGet package from PowerShell:

```powershell
./build.ps1 -Pack
```

For a different installation location:

```powershell
./build.ps1 -Pack -GammaDirectory 'C:\path\to\vvvv_gamma_7.4-win-x64'
```

Output: `dist/VL.AdvancedGlassWindow.1.0.0.nupkg`.

The build verifies that the package contains the main VL document, DLL, README and all three help patches. Each packaged file is checked against its source using SHA-256. Help patches are included without rewriting or reformatting them.

To check an existing package:

```powershell
./verify-package.ps1
```

When changing the release version, update the project, nuspec and verification script together. Never replace a version already published on NuGet.org.

The README contains installation and usage instructions. Upstream credit is preserved there; no license has been assigned to the upstream code in this adaptation.
