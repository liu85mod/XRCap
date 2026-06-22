// ============================================================================
//  KkapiHighResToolbarIcon
//  ---------------------------------------------------------------------------
//  Drop-in helper that replaces the low-resolution (32x32) KKAPI Studio toolbar
//  button icon with your own HIGH-RESOLUTION icon that scales up crisply.
//
//  把 KKAPI Studio 侧边工具栏那个又小又糊的 32x32 按钮图标，替换成你自己的
//  高清图标，并放大显示、锁定纵横比、绝不被裁切。
//
//  Why this is non-trivial / 为什么不是一句话能搞定：
//   - KKAPI hard-codes the toolbar icon to a 32x32 sprite, so a real hi-res PNG
//     gets downscaled into mush. We keep KKAPI's 32x32 hit-area but overlay our
//     own upscaled child Image on top.
//   - KKAPI repaints the button green/white on every toggle WITHOUT a null check
//     (button.image.color = ...). We redirect that paint onto an invisible decoy
//     so it never stains our icon.
//   - HS2API/KKAPI v1.43 rewrote the toolbar API. Old DLLs (<= v1.42.x) only have
//     CustomToolbarButtons; new ones have Toolbars.ToolbarManager. Touching the
//     wrong type throws TypeLoadException at JIT time and silently kills the icon.
//     This file isolates each API behind its own class (NoInlining) and only ever
//     JITs the branch that actually exists on the user's install.
//
//  This file is SAFE to ship even if the end-user has NOT installed KKAPI:
//  KoikatuAPI.GUID is a compile-time const (inlined, no type load), detection is
//  pure reflection, and the KKAPI-typed code lives in isolated, NoInlining classes
//  that are never reached when KKAPI is absent.
//
//  ---------------------------------------------------------------------------
//  REQUIREMENTS
//   - BepInEx
//   - A reference to KKAPI (KoikatuAPI) to compile. Recommended on your plugin:
//       [BepInDependency(KKAPI.KoikatuAPI.GUID, BepInDependency.DependencyFlags.SoftDependency)]
//   - Your two icons added to the project as Embedded Resources
//     (Properties -> Build Action -> Embedded Resource). The resource name is
//     usually "<RootNamespace>.<filename>", e.g. "MyPlugin.icon_off.png".
//
//  USAGE
//      private KkapiHighResToolbarIcon.Handle _iconHandle;
//
//      void Start()
//      {
//          if (!Chainloader.PluginInfos.ContainsKey(KKAPI.KoikatuAPI.GUID)) return;
//
//          _iconHandle = KkapiHighResToolbarIcon.Install(
//              plugin:     this,                       // your BaseUnityPlugin
//              log:        Logger,                     // your ManualLogSource (or null)
//              id:         "MyPlugin_MainToggle",
//              hoverText:  "My Feature\nHotkey: F6",
//              resourceAssembly:   Assembly.GetExecutingAssembly(),
//              iconOffResourceName:"MyPlugin.icon_off.png",
//              iconOnResourceName: "MyPlugin.icon_on.png",
//              getOnState: () => MyToggle.Value,       // read your current on/off
//              onClick:    () => MyToggle.Value = !MyToggle.Value); // flip on real click
//
//          // If your state can change OUTSIDE the button (hotkey, config UI, ...),
//          // refresh the icon when it does:
//          MyToggle.SettingChanged += (s, e) => _iconHandle?.Refresh();
//      }
//
//  Adapted from the VR180Recorder plugin. Rename the namespace below to your own.
// ============================================================================

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace KkapiToolbarIcons   // <-- rename to your own namespace
{
    public static class KkapiHighResToolbarIcon
    {
        // ===================================================================
        //  Visual tuning. Tweak these if the icon looks too small / clipped /
        //  off-centre. Sensible defaults are provided.
        //  视觉微调：图标偏小/被裁/没对齐时改这里。
        // ===================================================================
        public sealed class Tuning
        {
            // New API (v1.43+) child-icon scale & micro offset (px).
            public float NewScale = 1.4f;
            public float NewOffsetX = -5f;
            public float NewOffsetY = -6f;

            // Legacy API (<= v1.42.x) child-icon scale & micro offset (px).
            public float LegacyScale = 1.35f;
            public float LegacyOffsetX = 0f;
            public float LegacyOffsetY = 0f;

            // Transparent border (px) added around the source icon so upscaling
            // never clips the edges. 四周防爆透明衬垫，放大后边缘不被裁。
            public int Padding = 4;

            // Pixels with alpha <= this are forced fully transparent, killing the
            // "ghost white" fringe some PNG exporters leave behind.
            // 透明度低于此阈值的像素强制抹成透明黑，清除幽灵白边。
            public float GhostAlphaThreshold = 0.02f;
        }

        // ===================================================================
        //  Handle returned by Install(). Keep a reference and call Refresh()
        //  whenever your on/off state changes from outside the button.
        // ===================================================================
        public sealed class Handle
        {
            internal Func<bool> GetOnState;
            internal Sprite SpriteOn;
            internal Sprite SpriteOff;
            internal Texture2D TexOn;
            internal Texture2D TexOff;
            internal Image VisualImage;     // our own hi-res child icon
            internal Action RefreshAction;  // wired by whichever API path installed
            internal ManualLogSource Log;

            // Apply the correct sprite (on/off) to the visible child icon.
            // Unity-only — references no KKAPI types.
            internal void ApplySprite()
            {
                if (VisualImage == null || SpriteOn == null || SpriteOff == null) return;
                bool on = GetOnState != null && GetOnState();
                VisualImage.sprite = on ? SpriteOn : SpriteOff;
            }

            /// <summary>
            /// Call when your on/off state changes from OUTSIDE the toolbar button
            /// (e.g. a hotkey or config-UI change). A real button click already
            /// refreshes automatically.
            /// </summary>
            public void Refresh() { RefreshAction?.Invoke(); }
        }

        // ===================================================================
        //  Install — primary entry point (raw PNG bytes).
        //  Returns null if KKAPI is absent, not in Studio, or icons failed.
        // ===================================================================
        public static Handle Install(
            BaseUnityPlugin plugin,
            ManualLogSource log,
            string id,
            string hoverText,
            byte[] iconOffPng,
            byte[] iconOnPng,
            Func<bool> getOnState,
            Action onClick,
            Tuning tuning = null)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            tuning = tuning ?? new Tuning();

            // KKAPI presence check. KoikatuAPI.GUID is a compile-time const string,
            // so referencing it here does NOT force the KoikatuAPI type to load —
            // safe even if KKAPI is not installed.
            // GUID 是编译期常量(已内联)，引用它不会触发 KKAPI 类型加载，没装也安全。
            if (!Chainloader.PluginInfos.ContainsKey(KKAPI.KoikatuAPI.GUID))
            {
                log?.LogInfo("[HiResIcon] KKAPI not present; skipping toolbar button.");
                return null;
            }

            var handle = new Handle { GetOnState = getOnState, Log = log };

            // Build hi-res sprites once (KKAPI-free).
            LoadHighResTexture(iconOffPng, tuning, out handle.TexOff, out handle.SpriteOff);
            LoadHighResTexture(iconOnPng, tuning, out handle.TexOn, out handle.SpriteOn);
            if (handle.SpriteOff == null)
            {
                log?.LogWarning("[HiResIcon] failed to build icon sprites; aborting.");
                return null;
            }

            bool isNew = DetectNewToolbarApi(out string ver);
            try
            {
                if (isNew)
                {
                    NewApi.Install(plugin, handle, id, hoverText, getOnState, onClick, tuning);
                    log?.LogInfo($"[HiResIcon] KKAPI v{ver} (new toolbar API): hi-res icon installed.");
                }
                else
                {
                    LegacyApi.Install(plugin, handle, id, hoverText, getOnState, onClick, tuning);
                    log?.LogInfo($"[HiResIcon] KKAPI v{ver} (legacy toolbar API): hi-res icon installed.");
                }
            }
            catch (Exception e)
            {
                log?.LogWarning($"[HiResIcon] install failed ({(isNew ? "new" : "legacy")} path): {e.GetType().Name}: {e.Message}");
                return null;
            }
            return handle;
        }

        // ===================================================================
        //  Install — convenience overload that reads icons from embedded
        //  resources of the given assembly.
        // ===================================================================
        public static Handle Install(
            BaseUnityPlugin plugin,
            ManualLogSource log,
            string id,
            string hoverText,
            Assembly resourceAssembly,
            string iconOffResourceName,
            string iconOnResourceName,
            Func<bool> getOnState,
            Action onClick,
            Tuning tuning = null)
        {
            byte[] off = ReadEmbeddedResource(resourceAssembly, iconOffResourceName);
            byte[] on = ReadEmbeddedResource(resourceAssembly, iconOnResourceName);
            if (off == null || on == null)
            {
                log?.LogWarning($"[HiResIcon] embedded icon resource(s) not found: '{iconOffResourceName}' / '{iconOnResourceName}'. " +
                                "Check Build Action = Embedded Resource and the exact resource name.");
                return null;
            }
            return Install(plugin, log, id, hoverText, off, on, getOnState, onClick, tuning);
        }

        /// <summary>Read an embedded resource into a byte[]. Returns null if not found.</summary>
        public static byte[] ReadEmbeddedResource(Assembly asm, string name)
        {
            if (asm == null || string.IsNullOrEmpty(name)) return null;
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s == null) return null;
                var buf = new byte[s.Length];
                int total = 0, read;
                while (total < buf.Length && (read = s.Read(buf, total, buf.Length - total)) > 0)
                    total += read;
                return buf;
            }
        }

        // ===================================================================
        //  API-generation detection.
        //  Pure string reflection — never touches a KKAPI type (GUID is a const,
        //  inlined at compile time). Safe regardless of which KKAPI is installed,
        //  or whether one is installed at all.
        //  返回 true  = v1.43+ 新版 Toolbars.ToolbarManager API；
        //  返回 false = v1.42.x 及更早的旧版 CustomToolbarButtons API。
        // ===================================================================
        private static bool DetectNewToolbarApi(out string kkapiVersion)
        {
            kkapiVersion = "?";
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(KKAPI.KoikatuAPI.GUID, out var info) || info == null)
                    return false;

                if (info.Metadata != null && info.Metadata.Version != null)
                    kkapiVersion = info.Metadata.Version.ToString();

                // Preferred: capability probe — the new toolbar manager type exists => new API.
                var inst = info.Instance;
                if (inst != null)
                    return inst.GetType().Assembly.GetType("KKAPI.Studio.UI.Toolbars.ToolbarManager", false) != null;

                // Fallback: judge by version number when the instance is unavailable.
                return info.Metadata != null && info.Metadata.Version != null
                       && info.Metadata.Version >= new Version(1, 43);
            }
            catch
            {
                return false;
            }
        }

        // ===================================================================
        //  NEW API path (KKAPI v1.43+) — isolated so its KKAPI type references
        //  are only JITed when the new API actually exists.
        //
        //  Implementation notes (from KKAPI v1.45.1 source,
        //  src/Shared.CharaStudio/Studio/UI/Toolbars/):
        //   - The button is cloned from "Button Center" (a Unity Button, not a Toggle).
        //   - Green status tint: SimpleToolbarToggle.UpdateVisualState() runs
        //         button.image.color = Toggled.Value ? green : white;
        //     via Button.targetGraphic with NO null check -> targetGraphic must
        //     never be null; redirect it onto an invisible decoy instead.
        //   - Click pipeline bubbles via raycast to the root, unrelated to
        //     targetGraphic, so the decoy redirect does not affect clicks.
        //   - Dispose calls Object.Destroy on the iconGetter's texture, so the
        //     icon we feed the getter MUST be a throwaway placeholder.
        // ===================================================================
        private static class NewApi
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Install(
                BaseUnityPlugin plugin, Handle handle, string id, string hoverText,
                Func<bool> getOnState, Action onClick, Tuning t)
            {
                var toggle = new KKAPI.Studio.UI.Toolbars.SimpleToolbarToggle(
                    id,
                    hoverText,
                    // Placeholder icon: a fully-transparent 32x32. Three reasons (all from source):
                    //   - 32x32 matches Sprite.Create's hard-coded Rect and avoids KKAPI's size warning;
                    //   - explicitly cleared (a fresh Texture2D is uninitialised noise) so nothing flashes
                    //     before our takeover lands;
                    //   - disposable: KKAPI's Dispose Object.Destroys it, so never hand it a real icon.
                    // 占位贴图(全透明 32x32)：尺寸正好不触发警告；显式刷透明防闪杂图；一次性,会被 Destroy。
                    () =>
                    {
                        var placeholder = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                        placeholder.SetPixels(new Color[32 * 32]);   // default Color == (0,0,0,0)
                        placeholder.Apply();
                        return placeholder;
                    },
                    getOnState != null && getOnState(),
                    plugin,
                    val =>
                    {
                        // Stateless flip: ignore val. The BehaviorSubject's subscribe-time replay is
                        // swallowed by KKAPI's firstSkipped wrapper (confirmed in source), so this only
                        // fires on real clicks. 无状态翻转,忽略 val,补发已被 KKAPI 吃掉,只收真实点击。
                        onClick?.Invoke();
                        handle.Refresh();
                    });

                handle.RefreshAction = handle.ApplySprite;

                // toggle.SetDesiredPosition(2, 1); // uncomment to pin the button to a fixed slot
                KKAPI.Studio.UI.Toolbars.ToolbarManager.AddLeftToolbarControl(toggle);

                plugin.StartCoroutine(TakeoverIconRoutine(handle, toggle, t));
            }

            // The button is only created once Studio finishes loading, so wait for ButtonObject.
            private static IEnumerator TakeoverIconRoutine(
                Handle handle, KKAPI.Studio.UI.Toolbars.SimpleToolbarToggle toggle, Tuning t)
            {
                while (toggle != null && !toggle.IsDisposed && toggle.ButtonObject == null)
                    yield return null;
                if (toggle == null || toggle.IsDisposed || toggle.ButtonObject == null) yield break;

                var btn = toggle.ButtonObject;
                GameObject go = btn.gameObject;
                if (go == null || handle.SpriteOff == null) yield break;

                // (1) Kill the green-tint sink: redirect targetGraphic onto an invisible decoy.
                //     One-shot, permanent, zero ongoing cost.
                var decoyObj = new GameObject("HiResIcon_TintDecoy");
                decoyObj.transform.SetParent(go.transform, false);
                var decoy = decoyObj.AddComponent<Image>();
                decoy.enabled = false;        // never renders: paint it any colour, invisible
                decoy.raycastTarget = false;  // never blocks clicks
                btn.targetGraphic = decoy;
                btn.transition = Selectable.Transition.None; // bury the template's hover/press tint too

                // (2) Make the root image transparent; it only receives clicks (hit area stays 32x32).
                var rootImg = go.GetComponent<Image>();
                if (rootImg != null) rootImg.color = new Color(1f, 1f, 1f, 0f);

                // (3) Attach our own hi-res, upscaled icon as a child.
                Transform existing = go.transform.Find("HiResIcon_Visual");
                if (existing == null)
                {
                    var iconObj = new GameObject("HiResIcon_Visual");
                    iconObj.transform.SetParent(go.transform, false);

                    handle.VisualImage = iconObj.AddComponent<Image>();
                    handle.VisualImage.type = Image.Type.Simple;
                    handle.VisualImage.preserveAspect = true;
                    handle.VisualImage.raycastTarget = false;   // clicks go to the root image
                    handle.VisualImage.color = Color.white;

                    var rect = iconObj.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(0f, 0f);
                    rect.pivot = new Vector2(0f, 0f);
                    rect.sizeDelta = new Vector2(32f, 32f);
                    iconObj.transform.localScale = new Vector3(t.NewScale, t.NewScale, 1f);
                    rect.anchoredPosition = new Vector2(t.NewOffsetX, t.NewOffsetY);
                }
                else
                {
                    handle.VisualImage = existing.GetComponent<Image>();
                    handle.VisualImage.color = Color.white;
                }

                handle.ApplySprite();
                handle.Log?.LogInfo("[HiResIcon] (new API) icon takeover complete; green tint redirected to decoy.");
            }
        }

        // ===================================================================
        //  LEGACY API path (KKAPI <= v1.42.x) — fully self-isolated.
        //  In v1.43+ these types survive as [Obsolete] wrappers, so this class
        //  JITs safely on both old and new DLLs, but is only invoked when the
        //  legacy API was detected.
        //
        //  Implementation notes (from KKAPI v1.42.2 source):
        //   - AddLeftToolbarToggle clones "Button Center" (a Unity Button) and
        //     builds its Sprite from the EXACT Texture2D you pass in — so we can
        //     locate the resulting button by reference-comparing
        //     img.sprite.texture == ourTexture (independent of hierarchy path).
        //   - Green tint comes from a single subscription:
        //         BufferedValueChanged.Subscribe(b => btn.image.color = Value ? green : white);
        //     same decoy trick applies.
        //   - ValueChanged is a plain Subject (no replay on subscribe), but the
        //     control manually re-emits the current value once when created; that
        //     emission (val == current state) is filtered out below.
        //
        //  NOTE: this path keeps static single-button state (the common case).
        //  For multiple legacy buttons, instantiate separate adapted copies.
        // ===================================================================
        private static class LegacyApi
        {
#pragma warning disable 612, 618 // legacy toolbar types are [Obsolete] in v1.43+; intentional here
            private static Texture2D _matchTex;                         // exact texture handed to KKAPI (button locator)
            private static KKAPI.Studio.UI.ToolbarToggle _kkapiToggle;  // handle to keep KKAPI's Value in sync
            private static bool _initialized;
            private static bool _suppressCallback;
            private static Func<bool> _getOnState;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Install(
                BaseUnityPlugin plugin, Handle handle, string id, string hoverText,
                Func<bool> getOnState, Action onClick, Tuning t)
            {
                _getOnState = getOnState;
                _initialized = false;
                _matchTex = handle.TexOff; // the (padded) OFF texture; KKAPI builds the button sprite from it

                var toggle = KKAPI.Studio.UI.CustomToolbarButtons.AddLeftToolbarToggle(
                    handle.TexOff,
                    getOnState != null && getOnState(),
                    val =>
                    {
                        if (!_initialized || _suppressCallback) return;          // ignore programmatic syncs
                        if (getOnState != null && val == getOnState()) return;   // ignore the create-time replay
                        onClick?.Invoke();
                        handle.Refresh();
                    });
                _initialized = true;

                if (toggle == null)
                {
                    // Legacy API returns null outside Studio (e.g. in the main game) — normal.
                    handle.Log?.LogInfo("[HiResIcon] (legacy) not in Studio; no button created.");
                    return;
                }
                _kkapiToggle = toggle;

                handle.RefreshAction = () => { handle.ApplySprite(); SyncToggleValue(); };

                plugin.StartCoroutine(FindLegacyIconRoutine(handle, t));
            }

            // Sync KKAPI's internal Value to our state. Fixes hotkey-induced drift that would
            // otherwise make the next click get eaten by the equal-value short-circuit.
            // SetValue is a no-op on equal values, so this is cheap and idempotent.
            // 把 KKAPI 内部 Value 同步到当前状态,修复 F6 漂移导致的"点一下没反应"。
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void SyncToggleValue()
            {
                if (_kkapiToggle == null || _getOnState == null) return;
                bool on = _getOnState();
                if (_kkapiToggle.Value == on) return;
                _suppressCallback = true;
                try { _kkapiToggle.Value = on; }
                finally { _suppressCallback = false; }
            }

            private static IEnumerator FindLegacyIconRoutine(Handle handle, Tuning t)
            {
                const float timeoutSeconds = 120f;   // Studio can load slowly; give it room
                float deadline = Time.realtimeSinceStartup + timeoutSeconds;
                var wait = new WaitForSecondsRealtime(0.25f);

                while (Time.realtimeSinceStartup < deadline)
                {
                    var images = UnityEngine.Object.FindObjectsOfType<Image>();
                    for (int i = 0; i < images.Length; i++)
                    {
                        var img = images[i];
                        if (img != null && img.sprite != null && img.sprite.texture == _matchTex)
                        {
                            // (1) redirect green tint onto an invisible decoy
                            var btn = img.GetComponentInParent<Button>();
                            if (btn != null)
                            {
                                var decoyObj = new GameObject("HiResIcon_TintDecoy");
                                decoyObj.transform.SetParent(btn.transform, false);
                                var decoy = decoyObj.AddComponent<Image>();
                                decoy.enabled = false;
                                decoy.raycastTarget = false;
                                btn.targetGraphic = decoy;
                                btn.transition = Selectable.Transition.None;
                            }
                            else
                            {
                                handle.Log?.LogWarning("[HiResIcon] (legacy) no Button found; green tint may persist.");
                            }

                            // (2) transparent carrier image (still receives clicks)
                            img.color = new Color(1f, 1f, 1f, 0f);

                            // (3) attach our hi-res, upscaled child icon
                            //     A child avoids fighting the toolbar LayoutGroup for the root's size;
                            //     it scales/centres freely with the aspect ratio locked.
                            Image vis;
                            Transform existing = img.transform.Find("HiResIcon_LegacyVisual");
                            if (existing == null)
                            {
                                var iconObj = new GameObject("HiResIcon_LegacyVisual");
                                iconObj.transform.SetParent(img.transform, false);
                                vis = iconObj.AddComponent<Image>();

                                var rt = iconObj.GetComponent<RectTransform>();
                                rt.anchorMin = Vector2.zero;          // fill the parent icon
                                rt.anchorMax = Vector2.one;
                                rt.offsetMin = new Vector2(t.LegacyOffsetX, t.LegacyOffsetY);
                                rt.offsetMax = new Vector2(t.LegacyOffsetX, t.LegacyOffsetY);
                                rt.pivot = new Vector2(0.5f, 0.5f);
                                iconObj.transform.localScale = new Vector3(t.LegacyScale, t.LegacyScale, 1f);
                            }
                            else
                            {
                                vis = existing.GetComponent<Image>();
                            }
                            vis.type = Image.Type.Simple;     // no Sliced/Filled stretching
                            vis.preserveAspect = true;        // lock aspect ratio, never squashed
                            vis.raycastTarget = false;        // clicks go to the carrier
                            vis.color = Color.white;
                            handle.VisualImage = vis;

                            handle.ApplySprite();
                            handle.Log?.LogInfo("[HiResIcon] (legacy) button located; takeover complete.");
                            yield break;
                        }
                    }
                    yield return wait;
                }
                handle.Log?.LogInfo("[HiResIcon] (legacy) button image not found (click still works; on/off swap disabled).");
            }
#pragma warning restore 612, 618
        }

        // ===================================================================
        //  Hi-res texture loader (KKAPI-free, shared by both API paths).
        //  Adds a transparent padding border (anti-clip) and scrubs "ghost white"
        //  low-alpha pixels, then builds a FullRect sprite.
        //  高清贴图加载：4px 防爆透明衬垫 + 幽灵白像素清洗 + FullRect 不切多边形。
        // ===================================================================
        internal static void LoadHighResTexture(byte[] png, Tuning t, out Texture2D texture, out Sprite sprite)
        {
            texture = null;
            sprite = null;
            if (png == null || png.Length == 0) return;

            Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            original.LoadImage(png); // auto-resizes to the PNG's real dimensions

            int padding = Mathf.Max(0, t.Padding);
            int w = original.width + padding * 2;
            int h = original.height + padding * 2;

            texture = new Texture2D(w, h, TextureFormat.RGBA32, false);

            Color clear = new Color(0f, 0f, 0f, 0f); // absolute transparent black
            Color[] clearPixels = new Color[w * h];
            for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = clear;
            texture.SetPixels(clearPixels); // base coat fully transparent

            // Scrub ghost-white / near-transparent pixels regardless of RGB.
            Color[] px = original.GetPixels();
            for (int i = 0; i < px.Length; i++)
                if (px[i].a <= t.GhostAlphaThreshold) px[i] = clear;

            // Paste the cleaned icon dead-centre on the padded canvas.
            texture.SetPixels(padding, padding, original.width, original.height, px);
            texture.Apply();

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect); // FullRect: never tessellate to a polygon

            UnityEngine.Object.DontDestroyOnLoad(texture);
            UnityEngine.Object.Destroy(original); // burn the source texture
        }
    }
}
