using System;
using System.Runtime.InteropServices;
using UnityEngine;
using VR180Recorder; // 引入主命名空间，借用它的日志系统
/// <summary>
/// Universal GPU sync barrier for Unity (including BepInEx mods).
/// D3D11 only (AMD / Intel / NVIDIA). On other renderers: degrades to no-op.
///
/// Usage:
///   1. Once, anywhere you have a valid RenderTexture:
///        GpuFence.TryInit(someRenderTexture);
///      (Idempotent — safe to call every frame if you want.)
///   2. Each frame after rendering + encode submission:
///        GpuFence.IssueSync();
///        while (!GpuFence.IsDone()) yield return null;
/// </summary>
public static class GpuFence
{
    private const string DLL = "GpuFence";

    [DllImport(DLL)] private static extern void GpuFenceManualInit(IntPtr d3d11Resource);
    [DllImport(DLL)] private static extern IntPtr GpuFenceGetEventFunc();
    [DllImport(DLL)] private static extern void GpuFenceReset();
    [DllImport(DLL)] private static extern bool GpuFenceIsDone();
    [DllImport(DLL)] private static extern bool GpuFenceIsSupported();
    [DllImport(DLL)] private static extern int GpuFenceGetRenderer();
    [DllImport(DLL)] private static extern ulong GpuFenceGetSyncCount();
    [DllImport(DLL)] private static extern ulong GpuFenceGetFailCount();

    private static IntPtr s_eventFunc = IntPtr.Zero;
    private static bool s_initialized = false;
    private static bool s_dllMissing = false;

    /// <summary>
    /// Initialize from any valid RenderTexture. Idempotent.
    /// Returns true if fence is usable after this call; false if degraded to no-op.
    /// </summary>
    public static bool TryInit(RenderTexture anyRT)
    {
        if (s_initialized) return s_eventFunc != IntPtr.Zero;
        if (s_dllMissing) return false;
        if (anyRT == null) return false;

        // ★ 后端安全闸门:只有 D3D11 才进 native ManualInit
        // 其他后端(D3D12/Vulkan/Metal/OpenGL)一律降级为 no-op,不碰 native 指针
        var gfxType = SystemInfo.graphicsDeviceType;
        if (gfxType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
        {
            VR180Recorder.VR180Recorder.LogWarning("[GpuFence] Graphics backend is " + gfxType
                + ", not D3D11. Fence degraded to no-op (recording will still work, "
                + "but TDR protection is unavailable on this backend).");
            s_initialized = true;   // 标记已尝试,避免每帧重试
            return false;
        }
        if (!anyRT.IsCreated())
        {
            try { anyRT.Create(); }
            catch { return false; }
        }

        IntPtr nativePtr = anyRT.GetNativeTexturePtr();
        if (nativePtr == IntPtr.Zero) return false;

        try
        {
            GpuFenceManualInit(nativePtr);

            if (!GpuFenceIsSupported())
            {
                VR180Recorder.VR180Recorder.LogWarning("[GpuFence] Not supported on current renderer ("
                    + GpuFenceGetRenderer() + "), fence is no-op");
                s_initialized = true;
                return false;
            }

            s_eventFunc = GpuFenceGetEventFunc();
            if (s_eventFunc == IntPtr.Zero)
            {
                VR180Recorder.VR180Recorder.LogError("[GpuFence] GetEventFunc returned null");
                s_initialized = true;
                return false;
            }

            s_initialized = true;
            VR180Recorder.VR180Recorder.LogInfo("[GpuFence] Initialized successfully (renderer="
                + GpuFenceGetRenderer() + ")");
            return true;
        }
        catch (DllNotFoundException)
        {
            VR180Recorder.VR180Recorder.LogError("[GpuFence] GpuFence.dll not found in Plugins directory");
            s_dllMissing = true;
            return false;
        }
        catch (Exception e)
        {
            VR180Recorder.VR180Recorder.LogError("[GpuFence] Init failed: " + e.Message);
            s_initialized = true;
            return false;
        }
    }

    /// <summary>
    /// Issue a GPU sync. Non-blocking; poll IsDone() afterward.
    /// No-op if not initialized or renderer unsupported.
    /// </summary>
    public static void IssueSync()
    {
        if (!s_initialized || s_eventFunc == IntPtr.Zero) return;
        GpuFenceReset();
        GL.IssuePluginEvent(s_eventFunc, 1);
    }

    /// <summary>
    /// Issue a GPU sync and block the caller thread until it completes.
    /// Main-thread safe in multi-threaded rendering mode (render thread runs independently).
    /// Falls back to no-op if fence not initialized.
    /// </summary>
    public static void IssueSyncAndWait(int timeoutMs = 5000)
    {
        if (!s_initialized || s_eventFunc == IntPtr.Zero) return;
        IssueSync();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!GpuFenceIsDone())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                VR180Recorder.VR180Recorder.LogWarning("[GpuFence] IssueSyncAndWait timeout " + timeoutMs + "ms");
                break;
            }
            System.Threading.Thread.Sleep(0);  // 让出时间片,不烧 CPU
        }
    }
    /// <summary>
    /// True once the last IssueSync has completed.
    /// Returns true immediately if not initialized (so callers never block).
    /// </summary>
    public static bool IsDone()
    {
        if (!s_initialized || s_eventFunc == IntPtr.Zero) return true;
        return GpuFenceIsDone();
    }

    public static bool IsInitialized { get { return s_initialized && s_eventFunc != IntPtr.Zero; } }
    public static ulong SyncCount { get { return IsInitialized ? GpuFenceGetSyncCount() : 0UL; } }
    public static ulong FailCount { get { return IsInitialized ? GpuFenceGetFailCount() : 0UL; } }
}