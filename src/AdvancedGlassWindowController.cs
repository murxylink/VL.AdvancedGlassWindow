using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Stride.Games;
using VL.Core.Import;

namespace VL.AdvancedGlassWindow
{
    /// <summary>Owns glass state for one Stride window, including recreated windows after F9.</summary>
    [ProcessNode]
    public sealed class AdvancedGlassWindowController : IDisposable
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<IntPtr, WeakReference<AdvancedGlassWindowController>> Windows = new();
        private IntPtr handle;
        private bool enabled = true;
        private bool clickable = true;
        private bool disposed;
        private GameWindow owner;
        private enum Recovery { Warmup, Queued, Reapply, Complete }
        private Recovery recovery;
        private int warmupUpdates;
        private bool wasMinimized;


        /// <summary>Glass starts enabled. Reset disables it until the next Apply.</summary>
        [SupportedOSPlatform("windows")]
        public void Update(GameWindow window, bool clickable = true, bool apply = false, bool reset = false)
        {
            if (disposed || window == null) return;
            lock (Gate)
            {
                var previousHandle = window.NativeWindow.Handle;
                var minimized = IsIconic(previousHandle);
                if (!ReferenceEquals(owner, window))
                {
                    owner = window;
                    recovery = Recovery.Warmup;
                    warmupUpdates = 0;
                }
                else if ((wasMinimized && !minimized) ||
                    (previousHandle != handle && recovery == Recovery.Complete))
                {
                    recovery = Recovery.Warmup;
                    warmupUpdates = 0;
                }
                wasMinimized = minimized;
                var desired = enabled;
                if (reset) { desired = false; recovery = Recovery.Complete; }
                if (apply) desired = true;
                // An explicit Apply after a manual Reset is already a full recovery.
                if (!enabled && desired) recovery = Recovery.Complete;
                var autoReset = false;
                var autoApply = false;
                if (desired && !minimized)
                {
                    if (recovery == Recovery.Reapply)
                    {
                        recovery = Recovery.Complete;
                        autoApply = true;
                    }
                    else if (recovery == Recovery.Queued ||
                        (recovery == Recovery.Warmup && ++warmupUpdates >= 4))
                    {
                        recovery = Recovery.Reapply;
                        autoReset = true;
                    }
                }
                // Cross two real Update boundaries, just like the working manual Reset/Apply.
                var borderless = desired && !autoReset;
                if (window.IsBorderLess != borderless) window.IsBorderLess = borderless;
                var next = window.NativeWindow.Handle;
                if (next == IntPtr.Zero) return;
                var changed = handle != next;
                enabled = desired;
                if (changed)
                {
                    Unregister();
                    handle = next;
                    foreach (var key in new List<IntPtr>(Windows.Keys))
                        if (!Windows[key].TryGetTarget(out _)) Windows.Remove(key);
                }
                this.clickable = clickable;
                Windows[handle] = new WeakReference<AdvancedGlassWindowController>(this);
                NativeMethods.SetClickable(handle, !enabled || autoReset || clickable);
                if (enabled && (changed || apply || autoApply || (!window.Visible && !IsIconic(handle))))
                {
                    window.Visible = true;
                    NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOACTIVE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_DRAWFRAME | NativeMethods.SWP_SHOWWINDOW);
                }
                if ((reset && !enabled) || autoReset)
                {
                    var margins = default(NativeMethods.MARGINS);
                    NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);
                    NativeMethods.SetWindowPos(handle, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOACTIVE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_DRAWFRAME);
                }
                if (changed || autoReset || autoApply || reset || apply)
                    Trace($"window={handle.ToInt64():X}, visible={window.Visible}, enabled={enabled}, borderless={borderless}, recovery={recovery}, reset={reset}, apply={apply}");
            }
        }

        internal static void Presented(IntPtr hwnd)
        {
            lock (Gate)
            {
                if (Windows.TryGetValue(hwnd, out var weak) && weak.TryGetTarget(out var state) &&
                    state.enabled && state.recovery == Recovery.Warmup)
                {
                    state.recovery = Recovery.Queued;
                    Trace($"window={hwnd.ToInt64():X}, first Present complete; recovery queued");
                }
            }
        }

        private static void Trace(string message)
        {
            try
            {
                var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VL.AdvancedGlassWindow");
                System.IO.Directory.CreateDirectory(folder);
                var path = System.IO.Path.Combine(folder, "startup.log");
                if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 262144)
                    System.IO.File.WriteAllText(path, "");
                System.IO.File.AppendAllText(path, $"{DateTime.UtcNow:O} preview.8 {message}\n");
            }
            catch { /* Diagnostics must never stop rendering. */ }
        }

        internal static void Refresh(IntPtr hwnd, bool recreated)
        {
            lock (Gate)
            {
                if (Windows.TryGetValue(hwnd, out var weak) && weak.TryGetTarget(out var state))
                {
                    if (!state.enabled || state.disposed || state.recovery == Recovery.Reapply) return;
                    if (recreated && state.recovery == Recovery.Complete)
                        state.recovery = Recovery.Queued;
                    NativeMethods.SetClickable(hwnd, state.clickable);
                }
                // Glass wrappers also work before SetWindowTransparent's first Update.
                if (recreated)
                {
                    var zero = default(NativeMethods.MARGINS);
                    NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref zero);
                }
                var margins = NativeMethods.EXTENT_MARGIN;
                var hr = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
                if (hr != 0 && IsWindow(hwnd) && !IsIconic(hwnd)) Marshal.ThrowExceptionForHR(unchecked((int)hr));
            }
        }

        private void Unregister()
        {
            if (Windows.TryGetValue(handle, out var weak) && weak.TryGetTarget(out var owner) && ReferenceEquals(owner, this))
                Windows.Remove(handle);
        }

        public void Dispose()
        {
            lock (Gate) { disposed = true; Unregister(); handle = IntPtr.Zero; }
        }

        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    }
}
