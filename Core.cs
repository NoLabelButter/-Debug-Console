using System;
using System.Collections.Generic;
using HarmonyLib;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugConsole
{
    /// <summary>
    /// 模组入口。负责生命周期、热键、把命令表挂上、以及几个必要的流程钩子。
    /// </summary>
    public sealed class Core : IMod
    {
        public const string Version = "1.0.0";

        internal static ModLog Log = new ModLog();
        public static Core Instance { get; private set; }

        private PreferenceEntry<string> _hotkey;
        private PreferenceEntry<bool> _verbose;
        private PreferenceEntry<bool> _lightTheme;
        private PreferenceEntry<string> _mode;
        private PreferenceEntry<int> _uiScale;
        private PreferenceEntry<string> _favs;
        private PreferenceEntry<string> _lang;
        private HarmonyLib.Harmony _harmony;
        private GameObject _runnerObject;

        /// <summary>
        /// 热键。用带修饰键的组合而不是单键 —— 单键（原来的 F8）在装了别的模组的
        /// 环境里几乎必然撞车：Pure Ice、Custom_Rent 之类都占着 F8，按一下连它们一起触发。
        /// 组合键在游戏里没人用，撞不上。
        /// </summary>
        private const string DefaultHotkey = "LeftControl+LeftShift+I";

        private static KeyCode[] _hotKeys = new KeyCode[0];
        private static string _hotkeyText = "Ctrl+Shift+I";

        /// <summary>给人看的热键写法，状态栏和日志用。</summary>
        public static string HotkeyText { get { return _hotkeyText; } }

        /// <summary>界面风格偏好（纯外观，跟存档无关）。面板里那个「亮色/深色」按钮改的就是它。</summary>
        public static bool LightThemePref
        {
            get { return Instance != null && Instance._lightTheme != null && Instance._lightTheme.Value; }
            set
            {
                if (Instance == null || Instance._lightTheme == null) return;
                if (Instance._lightTheme.Value == value) return;
                Instance._lightTheme.Value = value;
                PreferenceStore.Save();
            }
        }

        /// <summary>物品控制台里收藏的物品 ID（'|' 分隔）。</summary>
        public static string FavoritesPref
        {
            get { return Instance != null && Instance._favs != null ? Instance._favs.Value : ""; }
            set
            {
                if (Instance == null || Instance._favs == null) return;
                if (Instance._favs.Value == value) return;
                Instance._favs.Value = value;
                PreferenceStore.Save();
            }
        }

        /// <summary>界面模式偏好：simple / hybrid / developer。</summary>
        public static string ModePref
        {
            get { return Instance != null && Instance._mode != null ? Instance._mode.Value : null; }
            set
            {
                if (Instance == null || Instance._mode == null) return;
                if (Instance._mode.Value == value) return;
                Instance._mode.Value = value;
                PreferenceStore.Save();
            }
        }

        /// <summary>控制台整体缩放百分比，不影响游戏原生 UI。</summary>
        public static int UiScalePref
        {
            get { return Instance != null && Instance._uiScale != null ? Instance._uiScale.Value : 100; }
            set
            {
                if (Instance == null || Instance._uiScale == null) return;
                if (Instance._uiScale.Value == value) return;
                Instance._uiScale.Value = value;
                PreferenceStore.Save();
            }
        }

        /// <summary>界面语言偏好：zh / en。顶栏那个中英按钮改的就是它。</summary>
        public static string LangPref
        {
            get { return Instance != null && Instance._lang != null ? Instance._lang.Value : "zh"; }
            set
            {
                if (Instance == null || Instance._lang == null) return;
                if (Instance._lang.Value == value) return;
                Instance._lang.Value = value;
                PreferenceStore.Save();
            }
        }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            PreferenceStore.Load();
            _hotkey = new PreferenceEntry<string>("Hotkey", DefaultHotkey);
            _verbose = new PreferenceEntry<bool>("Verbose", false);
            _lightTheme = new PreferenceEntry<bool>("LightTheme", false);
            _mode = new PreferenceEntry<string>("Mode", "hybrid");
            _uiScale = new PreferenceEntry<int>("UiScalePercent", 100);
            _favs = new PreferenceEntry<string>("Favorites", "");
            _lang = new PreferenceEntry<string>("Language", "zh");

            ApplyHotkey(_hotkey.Value);

            // 语言要在建任何界面之前定下来：文本是显示的那一刻查表的
            L10n.En = LangPref == "en";

            // 建面板之前先把上次选的风格刷进去，否则第一眼看到的是深色
            Palette.SetTheme(LightThemePref ? Palette.Theme.Light : Palette.Theme.Dark);
            ConsoleUI.LoadMode();

            CmdGame.Register();
            CmdSys.Register();

            Log.Msg("调试控制台 v" + Version + " 已初始化，共 " + Cmds.All.Count
                + " 条命令。默认按 " + _hotkeyText + " 打开。");
        }

        public void OnEnable()
        {
            try
            {
                _harmony = new HarmonyLib.Harmony("butterlab.debugconsole");
                PatchAll();
                _runnerObject = new GameObject("DebugConsole.Runner");
                UnityEngine.Object.DontDestroyOnLoad(_runnerObject);
                _runnerObject.AddComponent<DebugConsoleRunner>();
                SceneManager.sceneLoaded += OnSceneLoaded;
                Log.Msg("调试控制台已启用。");
            }
            catch (Exception ex) { Log.Error("启用调试控制台失败：" + ex); }
        }

        public void OnDisable()
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            try { if (_harmony != null) _harmony.UnpatchAll("butterlab.debugconsole"); } catch { }
            try { GameApi.ReleaseWorldMouse(); GameApi.BlockUiNav(false); GameApi.BlockGameInput(false); } catch { }
            try { if (_runnerObject != null) UnityEngine.Object.Destroy(_runnerObject); } catch { }
            _runnerObject = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { OnSceneWasLoaded(scene.buildIndex, scene.name); }

        // ── 热键 ──────────────────────────────────────────────────────

        /// <summary>
        /// 把 "LeftControl+LeftShift+I" 这样的写法解析成键位数组（最后一个才是主键）。
        /// 顺带拼一句给人看的短名（Ctrl+Shift+I）。
        /// </summary>
        private void ApplyHotkey(string text)
        {
            string[] parts = (text ?? "").Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<KeyCode> keys = new List<KeyCode>();
            List<string> names = new List<string>();

            for (int i = 0; i < parts.Length; i++)
            {
                KeyCode k;
                try
                {
                    k = (KeyCode)Enum.Parse(typeof(KeyCode), parts[i].Trim(), true);
                }
                catch
                {
                    Log.Warning("热键里认不出「" + parts[i] + "」，这一段忽略了。");
                    continue;
                }
                keys.Add(k);
                names.Add(ShortKeyName(k.ToString()));
            }

            if (keys.Count == 0)
            {
                // 配置写坏了就退回默认，不然玩家会连控制台都打不开
                Log.Warning("热键「" + text + "」解析不出键位，改用默认 " + DefaultHotkey + "。");
                ApplyHotkey(DefaultHotkey);
                return;
            }
            if (keys.Count == 1)
            {
                Log.Warning("热键只有一个键（" + names[0] + "），容易和别的模组撞车，建议写成 "
                    + DefaultHotkey + " 这种组合。");
            }

            _hotKeys = keys.ToArray();
            _hotkeyText = string.Join("+", names.ToArray());
        }

        private static string ShortKeyName(string code)
        {
            switch (code)
            {
                case "LeftControl": case "RightControl": return "Ctrl";
                case "LeftShift": case "RightShift": return "Shift";
                case "LeftAlt": case "RightAlt": return "Alt";
                case "LeftCommand": case "RightCommand": return "Win";
            }
            if (code.StartsWith("Alpha", StringComparison.Ordinal)) return code.Substring(5);
            if (code.StartsWith("Keypad", StringComparison.Ordinal)) return "小键盘" + code.Substring(6);
            return code;
        }

        /// <summary>
        /// 组合键判定：所有修饰键都按住不放，同时主键在这一帧被按下。
        /// 用 Input.GetKeyDown 逐个查（KeyCode 里没有「Ctrl+Shift+I」这种枚举）。
        /// </summary>
        private static bool HotkeyPressed()
        {
            int n = _hotKeys.Length;
            if (n == 0) return false;
            for (int i = 0; i < n - 1; i++)
            {
                if (!Input.GetKey(_hotKeys[i])) return false;
            }
            return Input.GetKeyDown(_hotKeys[n - 1]);
        }

        /// <summary>
        /// 逐个补丁类挂载。PatchAll 是一锤子买卖 —— 只要有一个目标方法对不上
        /// （游戏版本差异、方法改名），整批补丁都会失效；分开挂就互不牵连。
        /// </summary>
        private void PatchAll()
        {
            Type[] patches =
            {
                typeof(GameInputSuppressPatch),
                typeof(GameInputKeysSuppressPatch),
                typeof(GameInputHandlerSuppressPatch),
                typeof(LoadGamePatch),
                typeof(StartNewGamePatch),
                typeof(QuitToMenuPatch),
            };
            for (int i = 0; i < patches.Length; i++)
            {
                try
                {
                    _harmony.CreateClassProcessor(patches[i]).Patch();
                }
                catch (Exception ex)
                {
                    Log.Warning("补丁 " + patches[i].Name + " 挂载失败：" + ex.Message);
                }
            }
        }

        internal void OnUpdate()
        {
            try
            {
                ConsoleUI.Tick();
                // 连跳天数得在面板关着的时候也能走，所以驱动放在这儿而不是界面里
                GameApi.TickSkip();
                // 举枪的手臂：非射击模式下游戏每帧都会自己把它藏起来，得跟着按回去
                GameApi.TickArmShow();
                // 快捷删除只在游戏画面里生效（面板开着时鼠标归面板管）：
                // 先右键点一下选中，再按 Ctrl+Shift+X 删
                if (!ConsoleUI.IsOpen)
                {
                    GameApi.TickItemPick();
                    GameApi.TickItemDelete();
                    // 属性刷子：按住 Ctrl+Shift+A 划过物品就批量改
                    GameApi.TickPropBrush();
                }
                if (HotkeyPressed())
                {
                    ConsoleUI.Toggle();
                }
            }
            catch (Exception ex)
            {
                Log.Warning("控制台更新失败：" + ex.Message);
            }
        }

        private void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ConsoleUI.OnSceneLoaded();
            // 场景换了，OverlayHandler 也重建了，把可能残留的屏蔽栈还回去
            GameApi.ReleaseWorldMouse();
            GameApi.BlockUiNav(false);
            GameApi.BlockGameInput(false);
        }

        public static void Debug(string message)
        {
            if (Instance != null && Instance._verbose != null && Instance._verbose.Value)
            {
                Log.Msg("[调试] " + message);
            }
        }
    }

    internal sealed class DebugConsoleRunner : MonoBehaviour
    {
        private void Update()
        {
            Core core = Core.Instance;
            if (core != null) core.OnUpdate();
        }
    }

    internal sealed class ModLog
    {
        public void Msg(string message) { UnityEngine.Debug.Log("[Debug Console] " + message); }
        public void Warning(string message) { UnityEngine.Debug.LogWarning("[Debug Console] " + message); }
        public void Error(string message) { UnityEngine.Debug.LogError("[Debug Console] " + message); }
    }

    internal sealed class PreferenceEntry<T>
    {
        private readonly string _key;
        private readonly T _defaultValue;
        public PreferenceEntry(string key, T defaultValue) { _key = key; _defaultValue = defaultValue; }
        public T Value
        {
            get { return PreferenceStore.Get(_key, _defaultValue); }
            set { PreferenceStore.Set(_key, value); }
        }
    }

    internal static class PreferenceStore
    {
        private static readonly object Sync = new object();
        private static System.Collections.Generic.Dictionary<string, string> _values =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string ConfigPath { get { return Path.Combine(Application.persistentDataPath, "DebugConsole.json"); } }

        public static void Load()
        {
            try
            {
                string path = ConfigPath;
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(File.ReadAllText(path));
                    if (loaded != null) _values = new System.Collections.Generic.Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex) { Core.Log.Warning("读取配置失败：" + ex.Message); }
        }

        public static T Get<T>(string key, T fallback)
        {
            lock (Sync)
            {
                string raw;
                if (!_values.TryGetValue(key, out raw)) return fallback;
                try { return JsonConvert.DeserializeObject<T>(raw); }
                catch { return fallback; }
            }
        }

        public static void Set<T>(string key, T value)
        {
            lock (Sync) _values[key] = JsonConvert.SerializeObject(value);
            Save();
        }

        public static void Save()
        {
            try
            {
                string path = ConfigPath;
                string dir = Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                lock (Sync) File.WriteAllText(path, JsonConvert.SerializeObject(_values, Formatting.Indented));
            }
            catch (Exception ex) { Core.Log.Warning("保存配置失败：" + ex.Message); }
        }
    }

    /// <summary>
    /// 控制台开着的时候，把游戏自己的键鼠处理掐掉。
    ///
    /// 游戏所有按键派发都走 InputActionManager.Update → 各个 InputActionHandler，
    /// 掐这一处就等于「世界不再响应按键和点击」，但 Unity 的 EventSystem 完全不受影响，
    /// 所以我们的输入框照样能打字、按钮照样能点。
    /// 早先那种「把 EventSystem 禁掉自己模拟点击」的做法（NotEnoughItems）会把输入法、
    /// 回车、Tab 全变成手工活，不值得。
    ///
    /// 注意这里拦不住「别的模组自己在 OnUpdate 里读 Input」—— 那是模组各自的循环，
    /// 拦不了也不该拦。所以热键从 F8 换成了组合键：装了 Pure Ice / Custom_Rent 这些
    /// 占用 F8 的模组时，按一下 F8 会连它们一起触发。
    /// </summary>
    [HarmonyPatch(typeof(InputActionManager), "Update")]
    internal static class GameInputSuppressPatch
    {
        private static bool Prefix()
        {
            return !ConsoleUI.IsOpen;
        }
    }

    /// <summary>
    /// 管理器里那个按 KeyCode 派发的入口，单独再掐一道 —— 有些流程不经过 Update。
    /// </summary>
    [HarmonyPatch(typeof(InputActionManager), "UpdateKeys")]
    internal static class GameInputKeysSuppressPatch
    {
        private static bool Prefix()
        {
            return !ConsoleUI.IsOpen;
        }
    }

    /// <summary>
    /// 处理器自己那一层也补一刀。这是打字还会触发游戏快捷键的漏网处：
    /// 部分处理器自己带 Update / 按键回调，绕开了管理器的 Update。
    /// </summary>
    [HarmonyPatch(typeof(InputActionHandler), "UpdateUpdate")]
    internal static class GameInputHandlerSuppressPatch
    {
        private static bool Prefix()
        {
            return !ConsoleUI.IsOpen;
        }
    }

    /// <summary>换档/新开一局：物品目录缓存作废，重新读一遍（装了别的模组会加新物品）。</summary>
    [HarmonyPatch(typeof(PlayerStore), "LoadGame")]
    internal static class LoadGamePatch
    {
        private static void Postfix()
        {
            try { GameApi.DropItemCache(); } catch { }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "StartNewGame")]
    internal static class StartNewGamePatch
    {
        private static void Postfix()
        {
            try { GameApi.DropItemCache(); } catch { }
        }
    }

    /// <summary>退回主菜单：把面板收掉，并把鼠标交互还给游戏。</summary>
    [HarmonyPatch(typeof(GameMaster), "QuitToMenu")]
    internal static class QuitToMenuPatch
    {
        private static void Postfix()
        {
            try { ConsoleUI.Close(); } catch { }
        }
    }
}
