using System;
using System.Collections.Generic;
using System.Reflection;
using Stride.Graphics;
using VL.Stride.Games;
using D3D = SharpDX.Direct3D11;

namespace VL.AdvancedGlassWindow
{
    /// <summary>Gamma 7.4 / Stride 4.2.1 compatibility for the legacy DWM glass path.</summary>
    public sealed class LegacyPresentation : IPresentCallIntercept
    {
        private static readonly LegacyPresentation Instance = new LegacyPresentation();
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        /// <summary>Connect to RenderWindow's Present Call Intercept pin.</summary>
        public static IPresentCallIntercept Get() => Instance;

        /// <summary>Runs after the frame has been drawn, on the rendering thread.</summary>
        public void Present(GraphicsPresenter presenter)
        {
            Prepare(presenter);
            presenter.Present();
            AdvancedGlassWindowController.Presented(((SharpDX.DXGI.SwapChain)presenter.NativePresenter).Description.OutputHandle);
        }

        /// <summary>Switches the presenter's native swapchain without introducing an MSAA backbuffer.</summary>
        public static void Prepare(GraphicsPresenter presenter)
        {
            if (presenter is not SwapChainGraphicsPresenter swap)
                throw new NotSupportedException("Glass output requires the Gamma 7.4 Direct3D swapchain presenter.");
            var type = typeof(SwapChainGraphicsPresenter);
            var flipSupport = type.GetField("flipModelSupport", PrivateInstance);
            var useFlip = type.GetField("useFlipModel", PrivateInstance);
            var destroy = type.GetMethod("OnDestroyed", PrivateInstance | BindingFlags.Public);
            var recreate = type.GetMethod("OnRecreated", PrivateInstance | BindingFlags.Public);
            var destroyChildren = type.GetMethod("DestroyChildrenTextures", PrivateInstance | BindingFlags.Public | BindingFlags.Static);
            if (flipSupport == null || useFlip == null || destroy == null || recreate == null || destroyChildren == null)
                throw new NotSupportedException($"Unsupported Stride presenter: flipSupport={flipSupport!=null}, useFlip={useFlip!=null}, destroy={destroy!=null}, recreate={recreate!=null}, childViews={destroyChildren!=null}; {type.Assembly.Location}");
            if (!(bool)useFlip.GetValue(swap))
            {
                AdvancedGlassWindowController.Refresh(((SharpDX.DXGI.SwapChain)swap.NativePresenter).Description.OutputHandle, false);
                return;
            }
            var original = (D3D.Texture2D)SharpDXInterop.GetNativeResource(swap.BackBuffer);
            var description = original.Description;
            if (description.SampleDescription.Count != 1)
                throw new NotSupportedException("Glass output must use a single-sample backbuffer.");
            var device = (D3D.Device)SharpDXInterop.GetNativeDevice(swap.GraphicsDevice);
            var context = device.ImmediateContext;
            description.BindFlags = D3D.BindFlags.None;
            description.OptionFlags = D3D.ResourceOptionFlags.None;
            description.CpuAccessFlags = D3D.CpuAccessFlags.None;
            description.Usage = D3D.ResourceUsage.Default;
            using var savedFrame = new D3D.Texture2D(device, description);
            context.CopyResource(original, savedFrame);
            // Reset both Stride's cached targets and the native context before releasing views.
            var mainList = (CommandList)typeof(GraphicsDevice).GetField("InternalMainCommandList", PrivateInstance | BindingFlags.Public).GetValue(swap.GraphicsDevice);
            if (mainList != null) mainList.ClearState();
            else context.ClearState();
            context.Flush();
            var children = (List<Texture>)destroyChildren.Invoke(destroyChildren.IsStatic ? null : swap, new object[] { swap.BackBuffer });
            destroy.Invoke(swap, null);
            flipSupport.SetValue(swap, false);
            try { recreate.Invoke(swap, null); }
            catch
            {
                flipSupport.SetValue(swap, true);
                recreate.Invoke(swap, null);
                throw;
            }
            var initializeView = typeof(Texture).GetMethod("InitializeFrom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Texture), typeof(TextureViewDescription), typeof(DataBox[]) }, null);
            foreach (var child in children)
                initializeView.Invoke(child, new object[] { swap.BackBuffer, child.ViewDescription, null });
            var replacement = (D3D.Texture2D)SharpDXInterop.GetNativeResource(swap.BackBuffer);
            context.CopyResource(savedFrame, replacement);
            context.Flush();
            AdvancedGlassWindowController.Refresh(((SharpDX.DXGI.SwapChain)swap.NativePresenter).Description.OutputHandle, true);
        }
    }
}
