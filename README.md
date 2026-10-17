# VL.AdvancedGlassWindow

Transparent windows for **vvvv gamma 7.4**, with **Stride and Skia**.

Render 3D scenes and 2D graphics over your desktop, with optional mouse click-through.

## Requirements

- Windows x64 with desktop composition (DWM).
- vvvv gamma 7.4 / .NET 8.

## Installation

Once the package is published, open **Manage NuGets → Commandline** in vvvv and run:

```text
nuget install VL.AdvancedGlassWindow
```

Follow the installation instructions shown by vvvv.

## Stride

Use `SceneWindow (Glass)` for a scene, or `RenderWindow (Glass)` for a low-level renderer. Both nodes are in `Stride.AdvancedGlassWindow`.

1. Connect your scene or renderer to the corresponding Glass window.
2. Connect the window's `Output` to `SetWindowTransparent.Window`.
3. Use a transparent clear color: `(0, 0, 0, 0)`.
4. Scene MSAA defaults to the standard `X4`. Keep default post effects disabled.

The Stride controller enables transparency automatically. It includes a deferred Reset → Apply cycle at startup and after restoring a minimized window.

- `Clickable = True`: the window receives mouse input.
- `Clickable = False`: mouse input passes through to windows underneath.
- `Reset`: disables glass styling until `Apply` is triggered.
- `Apply`: enables glass styling again.

The Glass wrappers reserve the presentation handler and use a single-sample backbuffer. Custom presentation handlers are not supported by these wrappers.

## Skia

Open the Skia help patch for a complete example.

1. Connect your layer to `Renderer.Input`.
2. Set the renderer's background to `(0, 0, 0, 0)`.
3. Connect `Renderer.Form` to the Skia `SetWindowTransparent.Window` input.
4. Trigger `Apply`; use `Reset` to restore the window styling.

`Clickable` uses the same convention as Stride: `True` receives mouse input, `False` enables click-through.

## Examples

- [SceneWindow: transparent 3D scene](help/HowTo%20Show%20Transparent%20Window.vl)
- [RenderWindow: low-level rendering](help/HowTo%20Show%20Transparent%20RenderWindow.vl)
- [Skia: transparent 2D graphics](help/HowTo%20Show%20Transparent%20Skia%20Window.vl)

## Notes

- Stride supports windowed SDR output. HDR and exclusive fullscreen are not supported.
- Scene MSAA can produce a light fringe around geometry over the vvvv patch editor. Set `MSAA=None` if this occurs.
- If the image is missing, trigger `Reset` and then `Apply`. Startup diagnostics are written to `%TEMP%\VL.AdvancedGlassWindow\startup.log`.

## Credits

1. [**murxylink**](https://github.com/murxylink) — VL.AdvancedGlassWindow, gamma 7.4 adaptation, integration and testing.
2. [**torinos-yt**](https://github.com/torinos-yt) — author of the original [VL.GlassWindow](https://github.com/torinos-yt/VL.GlassWindow) library.
