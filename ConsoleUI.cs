using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DebugConsole
{
    /// <summary>输出区的一行。行高固定，滚动才好算。</summary>
    internal sealed class LogLine
    {
        public string Text;
        public Color Tone;
    }

    /// <summary>
    /// 命令输出的出口。命令只管说话，具体怎么显示、能不能显示（面板没开时）由这边决定。
    /// 面板没建起来的时候退到 游戏日志，至少不会丢消息。
    /// </summary>
    internal static class Out
    {
        public static void Line(string s) { Print(s, Palette.Body, Mark.Dot); }
        public static void Ok(string s) { Print(s, Palette.Ok, Mark.Ok); }
        public static void Warn(string s) { Print(s, Palette.Warn, Mark.Warn); }
        public static void Err(string s) { Print(s, Palette.Err, Mark.Err); }
        public static void Echo(string s) { Print(s, Palette.Accent, Mark.In); }
        public static void Plain(string s) { Print(s, Palette.Body, null); }

        private static void Print(string text, Color tone, string mark)
        {
            string body = text ?? "";
            if (ConsoleUI.Ready)
            {
                ConsoleUI.Print(body, tone, mark);
                return;
            }
            Core.Log.Msg((mark != null ? mark + " " : "") + Strip(body));
        }

        /// <summary>去掉富文本标签，只给 游戏日志用。</summary>
        private static string Strip(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
            StringBuilder sb = new StringBuilder(s.Length);
            bool inTag = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 界面模式。三套模式共用同一份命令表、同一份游戏状态，切模式只是换个摆法，
    /// 所以在任何一套里改过的东西，换过去还是那个值。
    /// </summary>
    internal enum ConsoleMode
    {
        Simple = 0,      // 简单：只有操作卡片，给不想敲命令的人
        Hybrid = 1,      // 混合：卡片 + 命令行，边点边敲
        Developer = 2,   // 开发者：纯命令行，带上反射调试那一组
    }

    /// <summary>卡片上那块实时读数。按节流刷新，而且只在文字真的变了才写回。</summary>
    internal sealed class CardView
    {
        public TextMeshProUGUI Text;
        public Func<string> Src;
        public string Last;
        public float MaxW;     // 这块读数能占多宽（方块窄，长读数得截）
    }

    /// <summary>物品控制台网格里的一格。</summary>
    internal sealed class ItemCell
    {
        public string Id;
        public UiButton Btn;    // 整格（点一下选中）
        public UiButton Star;   // 右上角的收藏星
    }

    /// <summary>物品控制台顶栏的下拉框：一个头 + 一列弹出来的选项。</summary>
    internal sealed class ItemDrop
    {
        public UiButton Head;
        public GameObject Popup;
        public UiScroll Scroll;                           // 选项多的时候要能滚，不然下面的选项够不着
        public Transform Parent;                          // 弹层挂在谁下面（物品控制台 / 面板）
        public string Prefix;                             // 头上的前缀，如「分类」
        public List<string> Keys = new List<string>();    // 选项的值
        public List<string> Names = new List<string>();   // 选项显示的字
        public List<UiButton> Rows = new List<UiButton>();
        public float X, Y, W;                             // 头的位置（弹层按它对齐）
        public int Sel;
        public Action OnPick;
        public Func<List<KeyValuePair<string, string>>> Provider;  // 选项现取的回调（可空）
        public string EmptyHint;                          // 取不到选项时显示 / 提示的字
    }

    /// <summary>
    /// 控制台面板。整块自建 UGUI。
    ///
    /// 三种模式共用同一套构建代码，差别只在「摆哪些区块」：
    ///   简单   = 顶部横向分类 + 卡片墙 + 一行反馈
    ///   混合   = 左栏分类与命令 + 卡片墙 + 输出区 + 输入行
    ///   开发者 = 左栏分类与命令 + 输出区（更大）+ 输入行
    /// 布局一律走 Ui.Place（从父容器左上角量），尺寸在 Build 里按屏幕算一次。
    /// </summary>
    internal static class ConsoleUI
    {
        public static readonly string Version = Core.Version;

        // ── 尺寸 ──────────────────────────────────────────────────────
        private const float MaxW = 1120f;
        private const float MaxH = 700f;
        private const float TitleH = 48f;
        private const float SideW = 236f;
        private const float Gap = 12f;
        private const float InputH = 42f;
        private const float StatusH = 28f;
        private const float LogRowH = 22f;
        private const float LogFont = 15f;
        private const float CmdRowH = 44f;
        private const int MaxLines = 400;

        // 操作方块（母菜单一个分类一块，子菜单一个功能一块）
        private const float TileH = 150f;        // 子菜单里功能方块的高
        private const float GroupTileH = 116f;   // 母菜单里分类方块的高
        private const float TilePad = 12f;       // 方块之间的间隙
        private const float CardRowH = 30f;      // 方块里按钮行 / 输入行的高
        private const float FeedH = 34f;        // 简单模式底部那条反馈
        private const float DragKeepRight = 620f; // 标题栏右侧按钮区不作为拖拽把手

        // 物品控制台
        private const float ItemDropH = 30f;    // 顶栏那一行（下拉框 / 搜索框）的高度
        private const int GridCols = 7;         // 网格列数
        private const int GridRows = 6;         // 网格行数

        private static float _w = MaxW, _h = MaxH;

        // ── 状态 ──────────────────────────────────────────────────────
        private static Canvas _canvas;
        private static GameObject _panel;
        private static RectTransform _panelRect;
        private static RectTransform _titleRect;
        private static UiButton _btnTheme;
        private static UiButton _btnLang;               // 顶栏那个中英切换（中 / EN）
        private static UiButton _btnScaleDown;
        private static UiButton _btnScaleUp;
        private static TextMeshProUGUI _scaleLabel;
        private static UiButton _btnClose;
        private static UiButton _btnRun;
        private static TMP_InputField _input;

        private static readonly List<UiButton> _groupBtns = new List<UiButton>();
        private static readonly List<UiButton> _modeBtns = new List<UiButton>();
        private static readonly List<UiButton> _cmdBtns = new List<UiButton>();
        private static GameObject _cmdContent;
        private static UiScroll _cmdScroll;
        private static UiScroll _logScroll;
        private static readonly List<TextMeshProUGUI> _rows = new List<TextMeshProUGUI>();

        // 操作方块（母菜单 / 子菜单）
        private static UiScroll _cardScroll;
        private static GameObject _cardContent;
        private static readonly List<CardView> _cardViews = new List<CardView>();
        private static readonly List<TMP_InputField> _cardInputs = new List<TMP_InputField>();
        private static bool _subOpen;                   // 工作区停在某个分类的子菜单？否 = 母菜单
        private static float _readAt;

        // 物品控制台
        private static bool _itemView;                  // 现在停在物品控制台这一屏？
        private static UiButton _btnItems;              // 顶栏那个「物品控制台 / 返回命令」
        private static GameObject _itemRoot;
        private static GameObject _clearAllDialog;
        private static GameObject _itemGrid;
        private static float _itemGridW, _itemGridH;
        private static Image _itemSelIcon;
        private static TextMeshProUGUI _itemSelName;
        private static TextMeshProUGUI _itemSelInfo;
        private static TextMeshProUGUI _itemPageText;
        private static UiButton _itemPrev, _itemNext;
        private static TMP_InputField _itemCount;
        private static TMP_InputField _itemSearch;
        private static UiButton _itemDesk;
        private static UiButton _itemStore;
        private static UiButton _itemFavBtn;
        private static ItemDrop _dropDir, _dropKind, _dropOpen;
        private static int _dropFrame = -1;             // 刚点开下拉框的那一帧，别被自己的「点外面收起来」关掉
        private static readonly List<ItemCell> _itemCells = new List<ItemCell>();
        private static readonly HashSet<string> _favs = new HashSet<string>();
        private static bool _favLoaded;
        private static string _itemSelId;
        private static int _itemPage;
        private static string _itemDir = "";            // "" = 全部分类
        private static int _itemKind;                   // 0 全部 / 1 只收藏 / 2 店里已有
        private static bool _itemToStore;               // 生成到仓库？（默认落在柜台）
        private static string _itemQuery = "";
        private static string _itemLastQuery = "\u0000";
        private static float _itemPollAt;

        private static TextMeshProUGUI _statusLeft;
        private static TextMeshProUGUI _statusMid;
        private static TextMeshProUGUI _statusRight;
        private static TextMeshProUGUI _feed;          // 简单模式的反馈行

        private static readonly List<LogLine> _lines = new List<LogLine>();
        private static string _lastLine;
        private static Color _lastTone = Color.white;

        private static ConsoleMode _mode = ConsoleMode.Hybrid;
        private static bool _open;
        public static bool IsOpen { get { return _open; } }
        public static bool Ready { get { return _canvas != null && (_rows.Count > 0 || _feed != null); } }
        public static bool IsTyping { get { return Ui.IsFocused(_input) || AnyCardTyping(); } }
        public static ConsoleMode Mode { get { return _mode; } }
        public static int UiScalePercent { get { return Mathf.Clamp(Core.UiScalePref, 75, 200); } }

        private static float UiScaleFactor { get { return UiScalePercent / 100f; } }

        public static string UiScaleName() { return UiScalePercent + "%"; }

        private static void AdjustUiScale(int delta)
        {
            SetUiScale(Mathf.Clamp(UiScalePercent + delta, 75, 200));
        }

        public static bool SetUiScale(int percent)
        {
            if (percent < 75 || percent > 200) return false;
            if (percent == UiScalePercent)
            {
                Out.Line("当前界面大小已经是 " + Palette.TagVal(UiScaleName()) + "。");
                return true;
            }

            Core.UiScalePref = percent;
            Rebuild();
            Out.Ok("界面整体大小已调整为 " + Palette.TagVal(UiScaleName()) + "。");
            return true;
        }

        // 一进来停在母菜单（「全部」），不预先替用户挑一个分类高亮
        private static string _group = "all";
        private static float _posX, _posY;
        private static bool _dragging;
        private static Vector2 _grab;
        private static int _lastW, _lastH;

        // 历史与补全
        private static readonly List<string> _history = new List<string>();
        private static int _histIdx = -1;
        private static string _histDraft = "";

        private static int _logFirst = -1;   // 已渲染的起始行，用来判断要不要重画
        private static int _logCount = -1;
        private static bool _welcomed;       // 开场白只打一次
        private static bool _refocus;        // 下一帧把焦点还给输入框

        // ══════════════════════════════════════════════════════════════
        //  对外
        // ══════════════════════════════════════════════════════════════

        public static void Toggle()
        {
            if (_open) Close(); else Open();
        }

        // ── 界面模式 ──────────────────────────────────────────────────

        public static string ModeName()
        {
            return _mode == ConsoleMode.Simple ? "简单"
                 : _mode == ConsoleMode.Developer ? "开发者" : "混合";
        }

        public static string ModeKey()
        {
            return _mode == ConsoleMode.Simple ? "simple"
                 : _mode == ConsoleMode.Developer ? "developer" : "hybrid";
        }

        public static bool ParseMode(string s, out ConsoleMode m)
        {
            m = ConsoleMode.Hybrid;
            if (string.IsNullOrEmpty(s)) return false;
            switch (s.Trim().ToLowerInvariant())
            {
                case "simple": case "s": case "简单": m = ConsoleMode.Simple; return true;
                case "hybrid": case "h": case "mixed": case "混合": m = ConsoleMode.Hybrid; return true;
                case "developer": case "dev": case "d": case "开发": case "开发者":
                    m = ConsoleMode.Developer; return true;
            }
            return false;
        }

        /// <summary>启动时把上次选的模式读回来（Core 建好偏好项之后调）。</summary>
        public static void LoadMode()
        {
            ConsoleMode m;
            _mode = ParseMode(Core.ModePref, out m) ? m : ConsoleMode.Hybrid;
        }

        public static void SetMode(ConsoleMode m)
        {
            if (_mode == m) return;
            _mode = m;
            Core.ModePref = ModeKey();
            // 反射那一组在简单模式里不露面，切过去时别把分类留在它上面
            if (_mode == ConsoleMode.Simple && _group == "reflect") _group = "all";
            Rebuild();
            Out.Line("界面已切到 " + Palette.TagVal(ModeName() + "模式") + "。");
        }

        public static void Open()
        {
            if (_open) return;
            try
            {
                if (_canvas == null && !Build())
                {
                    Core.Log.Warning("控制台面板建不起来，看日志。");
                    return;
                }
                _open = true;
                // 上次关面板时可能正按着鼠标拖，状态带进来面板就会粘在指针上
                _dragging = false;
                _grab = Vector2.zero;
                Ui.SetActive(_canvas.gameObject, true);
                // 每次打开都贴到底：上一轮滚到哪儿是上一轮的事，进来先看最新一条
                if (_logScroll != null) _logScroll.ToBottom();
                if (_cardScroll != null) _cardScroll.ToTop();
                Render(true);
                RefreshCards();
                GameApi.BlockWorldMouse(true);
                GameApi.BlockUiNav(true);   // 别让方向键/回车穿透到游戏界面的按钮上
                GameApi.BlockGameInput(true);   // 游戏自己的按键派发整条掐掉，打字不会触发游戏快捷键
                Focus();
            }
            catch (Exception ex)
            {
                _open = false;
                Core.Log.Error("打开控制台失败：" + ex);
            }
        }

        public static void Close()
        {
            if (!_open) return;
            _open = false;
            _dragging = false;
            _grab = Vector2.zero;
            try
            {
                Ui.Blur(_input);
                BlurCards();
                Ui.SetActive(_canvas != null ? _canvas.gameObject : null, false);
                GameApi.BlockWorldMouse(false);
                GameApi.BlockUiNav(false);
                GameApi.BlockGameInput(false);
            }
            catch (Exception ex)
            {
                Core.Log.Warning("关闭控制台失败：" + ex.Message);
            }
        }

        public static void ToggleTheme()
        {
            bool light = !Palette.IsLight;
            Core.LightThemePref = light;
            Palette.SetTheme(light ? Palette.Theme.Light : Palette.Theme.Dark);
            Rebuild();
            Out.Line(light ? "界面已切到亮色风格。" : "界面已切到深色风格。");
        }

        /// <summary>
        /// 中英切换。界面上的字是「显示的那一刻」查表翻的，所以整块重建一遍就够了。
        /// 日志里存的是中文原文，重建后连旧日志一起变成新语言。
        /// </summary>
        public static void ToggleLang()
        {
            bool en = !L10n.En;
            Core.LangPref = en ? "en" : "zh";
            L10n.En = en;
            GameApi.DropNameCache();   // 物品名跟着语言走，缓存得一起扔
            Rebuild();
            Out.Line(en ? "Language switched to English." : "界面已切回中文。");
        }

        /// <summary>颜色是建界面时烙进 Image/Text 的，换风格只能整块拆了重建。</summary>
        public static void Rebuild()
        {
            string keep = Ui.TextOf(_input, "");
            KillCanvas();
            if (_open && !Build())
            {
                _open = false;
                return;
            }
            if (_open)
            {
                Ui.SetActive(_canvas.gameObject, true);
                Ui.SetTextOf(_input, keep);
                Render(true);
                RefreshCards();
            }
        }

        /// <summary>
        /// 场景重载：旧物体已经没了，引用全清，下次打开重建。
        /// 换了场景就是换了存档/世界，旧输出留着只会误导，一并清掉。
        /// </summary>
        public static void OnSceneLoaded()
        {
            KillCanvas();
            _open = false;
            _subOpen = false;     // 换了场景就从母菜单重新开始
            _group = "all";        // 高亮也跟着回「全部」，别留着上一个存档里选的分类
            _lines.Clear();
            _logCount = -1;
            _welcomed = false;
            _lastLine = null;
            Fonts.Reset();
        }

        public static void ClearLog()
        {
            _lines.Clear();
            _logFirst = -1;
            Render(true);
        }

        /// <summary>往输出区加一行。面板没建起来也不会崩，只是看不见。</summary>
        public static void Print(string text, Color tone, string mark)
        {
            if (string.IsNullOrEmpty(text)) return;
            string line = string.IsNullOrEmpty(mark) ? text : mark + " " + text;
            _lastLine = line;
            _lastTone = tone;
            _lines.Add(new LogLine { Text = line, Tone = tone });
            while (_lines.Count > MaxLines) _lines.RemoveAt(0);
            _logFirst = -1;   // 强制重画
            SyncFeed();
        }

        /// <summary>
        /// 简单模式没有输出区，就把最近一条消息挂到底部那条反馈带上。
        /// 这样在简单模式里点按钮也不会「点下去没反应」。
        /// </summary>
        private static void SyncFeed()
        {
            if (_feed == null) return;
            try
            {
                if (string.IsNullOrEmpty(_lastLine)) { _feed.text = ""; return; }
                _feed.color = _lastTone;
                _feed.text = Palette.Rt(Ui.Cut(L10n.S(_lastLine), 13f, _w - 120f));
            }
            catch { }
        }

        private static void Focus()
        {
            Ui.Focus(_input);
        }

        // ══════════════════════════════════════════════════════════════
        //  每帧
        // ══════════════════════════════════════════════════════════════

        public static void Tick()
        {
            if (!_open) return;
            try
            {
                if (Screen.width != _lastW || Screen.height != _lastH)
                {
                    // 分辨率变了：整块重来最省事，布局全都是按屏幕算死的
                    Rebuild();
                    return;
                }

                Ui.UpdateHover();
                UpdateDrag();
                HandleKeys();
                if (_refocus && _input != null)
                {
                    // 回车提交后的下一帧才抢焦点：TMP_InputField 是在事件处理里失焦的，
                    // 当场抢回来会被它随后的 DeactivateInputField 覆盖掉。
                    _refocus = false;
                    Ui.Focus(_input);
                }
                if (_logScroll != null) _logScroll.PollWheel();
                if (_cmdScroll != null) _cmdScroll.PollWheel();
                if (_cardScroll != null) _cardScroll.PollWheel();
                if (_dropOpen != null && _dropOpen.Scroll != null) _dropOpen.Scroll.PollWheel();
                PollItemConsole();
                Render(false);
                RefreshCards();
                SyncStatus();
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[界面] 刷新失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 拖面板：只认标题栏左边那块「空白把手」。
        /// 模式切换 / 物品控制台 / 缩放 / 主题 / 关闭这几个按钮都在标题栏右侧，
        /// 以前整条标题栏都能拖 —— 按到关闭按钮也进了拖拽态，面板一关一开就粘在鼠标上，
        /// 而且拖到指针正好停在关闭按钮上，随手一点又把面板关了。把手只留左边，这几件事就都没了。
        /// </summary>
        private static void UpdateDrag()
        {
            if (_titleRect == null || _panelRect == null) return;

            Vector2 m = Input.mousePosition;
            float scale = UiScaleFactor;
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            float mx = m.x / scale;
            float my = m.y / scale;
            float cx = screenW * 0.5f + _posX;      // 面板中心（画布坐标）
            float cy = screenH * 0.5f + _posY;
            float left = cx - _w * 0.5f;
            float top = cy + _h * 0.5f;
            float handleW = Mathf.Max(120f, _w - DragKeepRight);

            if (Input.GetMouseButtonDown(0))
            {
                _dragging = mx >= left && mx <= left + handleW
                         && my <= top && my >= top - TitleH;
                if (_dragging) _grab = new Vector2(mx - cx, my - cy);
            }

            if (!_dragging) return;
            // 送手（哪怕松在面板外）或换了焦点就停，不然鼠标往哪儿走它跟到哪儿
            if (!Input.GetMouseButton(0)) { _dragging = false; return; }

            _posX = mx - _grab.x - screenW * 0.5f;
            _posY = my - _grab.y - screenH * 0.5f;
            ClampPos();
            Ui.PlaceCentered(_panel, _posX, _posY, _w, _h);
        }

        private static void ClampPos()
        {
            float scale = UiScaleFactor;
            float mx = Mathf.Max(0f, (Screen.width / scale - _w) * 0.5f);
            float my = Mathf.Max(0f, (Screen.height / scale - _h) * 0.5f);
            _posX = Mathf.Clamp(_posX, -mx - 40f, mx + 40f);
            _posY = Mathf.Clamp(_posY, -my - 40f, my + 40f);
        }

        private static void HandleKeys()
        {
            bool typing = Ui.IsFocused(_input) || AnyCardTyping();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 下拉框开着时，ESC 先收下拉框
                if (_dropOpen != null) { CloseDrop(); return; }
                // 有输入框在输入时，ESC 先把它收掉；再按一次才关面板
                if (typing) { Ui.Blur(_input); BlurCards(); }
                else Close();
                return;
            }

            // 简单模式没有命令行，Tab 没什么可补的
            if (_input != null && Input.GetKeyDown(KeyCode.Tab))
            {
                Complete();
                return;
            }

            if (Ui.IsFocused(_input))
            {
                if (Input.GetKeyDown(KeyCode.UpArrow)) History(-1);
                else if (Input.GetKeyDown(KeyCode.DownArrow)) History(1);
            }
        }

        private static bool AnyCardTyping()
        {
            for (int i = 0; i < _cardInputs.Count; i++)
            {
                if (Ui.IsFocused(_cardInputs[i])) return true;
            }
            return false;
        }

        private static void BlurCards()
        {
            for (int i = 0; i < _cardInputs.Count; i++) Ui.Blur(_cardInputs[i]);
        }

        private static void History(int dir)
        {
            if (_history.Count == 0) return;
            if (_histIdx == -1)
            {
                if (dir > 0) return;
                _histDraft = Ui.TextOf(_input, "");
                _histIdx = _history.Count - 1;
            }
            else
            {
                _histIdx += dir;
            }

            if (_histIdx < 0) _histIdx = 0;
            if (_histIdx >= _history.Count)
            {
                // 越过最新一条 → 回到刚打了一半的那句
                _histIdx = -1;
                Ui.SetTextOf(_input, _histDraft);
                return;
            }
            Ui.SetTextOf(_input, _history[_histIdx]);
        }

        private static void Complete()
        {
            // Tab 键 TMP_InputField 自己也会处理（它想着挪焦点），所以补完下一帧把焦点抢回来
            _refocus = true;

            string line = Ui.TextOf(_input, "");
            List<string> cand = Cmds.Complete(line);
            if (cand.Count == 0) { Out.Warn("没有可补全的项。"); return; }

            if (cand.Count == 1)
            {
                Ui.SetTextOf(_input, ReplaceLastToken(line, cand[0]));
                return;
            }

            string prefix = CommonPrefix(cand);
            string now = Ui.TextOf(_input, "");
            string last = LastToken(now);
            if (prefix.Length > last.Length) Ui.SetTextOf(_input, ReplaceLastToken(now, prefix));

            Out.Line("候选 " + Palette.TagVal(cand.Count + " 个") + "：");
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < cand.Count && i < 24; i++)
            {
                if (i > 0) sb.Append("　");
                sb.Append(Palette.TagKey(cand[i]));
            }
            // 候选是「要照抄打进去」的 token，原样显示，别翻译
            Out.Line(L10n.Raw(sb.ToString()));
        }

        private static string LastToken(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            int i = line.Length;
            while (i > 0 && !char.IsWhiteSpace(line[i - 1])) i--;
            return line.Substring(i);
        }

        private static string ReplaceLastToken(string line, string token)
        {
            line = line ?? "";
            int i = line.Length;
            while (i > 0 && !char.IsWhiteSpace(line[i - 1])) i--;
            return line.Substring(0, i) + token;
        }

        private static string CommonPrefix(List<string> list)
        {
            if (list.Count == 0) return "";
            string p = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                int k = 0;
                int max = Math.Min(p.Length, list[i].Length);
                while (k < max && char.ToLowerInvariant(p[k]) == char.ToLowerInvariant(list[i][k])) k++;
                p = p.Substring(0, k);
                if (p.Length == 0) break;
            }
            return p;
        }

        // ══════════════════════════════════════════════════════════════
        //  执行
        // ══════════════════════════════════════════════════════════════

        /// <summary>输入框回车。</summary>
        private static void Submit(string text)
        {
            try
            {
                string line = (text ?? "").Trim();
                Ui.SetTextOf(_input, "");
                if (line.Length == 0) return;

                _history.Add(line);
                while (_history.Count > 60) _history.RemoveAt(0);
                _histIdx = -1;

                Out.Echo(line);
                Cmds.Execute(line);
            }
            catch (Exception ex)
            {
                Out.Err("执行失败：" + ex.Message);
            }
            finally
            {
                // 让玩家可以接着敲下一条（下一帧再抢焦点，见 Tick）
                _refocus = true;
            }
        }

        private static void RunPreset(string cmdText)
        {
            Ui.SetTextOf(_input, cmdText);
            Submit(cmdText);
        }

        /// <summary>侧栏点命令：能直接跑的直接跑，要参数的把命令名塞进输入框让玩家接着打。</summary>
        private static void PickCommand(Cmd c)
        {
            if (c == null || _input == null) return;
            if (!string.IsNullOrEmpty(c.Preset)) { RunPreset(c.Preset); return; }

            if (c.Hints == null || c.Hints.Length == 0)
            {
                // 没有参数提示 —— 大概率是「给个必填参数」那种，只填个名字
                Ui.SetTextOf(_input, c.Name + " ");
                _refocus = true;
                Out.Line(Palette.TagMuted("把参数补上再回车：") + Palette.TagKey(c.Usage));
                return;
            }
            Ui.SetTextOf(_input, c.Name + " ");
            _refocus = true;
        }

        /// <summary>卡片按钮：直接当命令跑一遍。跟手敲走的是同一条路径，不存在两套逻辑。</summary>
        private static void RunCard(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return;
            try
            {
                Out.Echo(cmd);
                Cmds.Execute(cmd);
            }
            catch (Exception ex)
            {
                Out.Err("执行失败：" + ex.Message);
            }
        }

        private static void RunCardInput(TMP_InputField f, CardInput ci, string text)
        {
            string s = (text ?? "").Trim();
            if (s.Length == 0) { Out.Warn("先在框里填内容。"); return; }
            Ui.SetTextOf(f, "");
            RunCard(ci.Prefix + s + (ci.Suffix ?? ""));
        }

        // ══════════════════════════════════════════════════════════════
        //  构建
        // ══════════════════════════════════════════════════════════════

        private static void KillCanvas()
        {
            // Destroy 要到帧末才生效，先关掉，否则重建时新旧画布会重叠闪一下
            Ui.SetActive(_canvas != null ? _canvas.gameObject : null, false);
            Ui.Kill(_canvas);
            Ui.Kill(_panel);
            _canvas = null;
            _panel = null;
            _panelRect = null;
            _titleRect = null;
            _btnTheme = _btnClose = _btnRun = null;
            _btnScaleDown = _btnScaleUp = null;
            _scaleLabel = null;
            _input = null;
            _groupBtns.Clear();
            _modeBtns.Clear();
            _cmdBtns.Clear();
            _cmdContent = null;
            _cmdScroll = null;
            _logScroll = null;
            _rows.Clear();
            _cardScroll = null;
            _cardContent = null;
            _cardViews.Clear();
            _cardInputs.Clear();
            KillItemConsole();
            _statusLeft = _statusMid = _statusRight = null;
            _feed = null;
            _dragging = false;
            _refocus = false;
            _logFirst = -1;
            Ui.ResetHovers();
        }

        private static bool Build()
        {
            try
            {
                float scale = UiScaleFactor;
                _w = Mathf.Min(MaxW, Mathf.Max(320f, (Screen.width - 60f) / scale));
                _h = Mathf.Min(MaxH, Mathf.Max(280f, (Screen.height - 60f) / scale));
                _lastW = Screen.width;
                _lastH = Screen.height;
                ClampPos();     // 上次拖到哪儿存着，但分辨率可能已经变了，先拉回屏内

                _canvas = Ui.NewCanvas("DebugConsole_Canvas", 32000, scale);
                if (_canvas == null) return false;

                // 全屏挡板：把面板以外的点击全部吃掉，免得点到游戏里的东西
                Image scrim = Ui.MakeImage(_canvas.transform, "Scrim", Palette.A(Palette.PageBg, 0.45f), true);
                if (scrim != null) Ui.Stretch(scrim.gameObject, 0f);

                _panel = Ui.New("Panel", _canvas.transform);
                if (_panel == null) return false;
                _panelRect = Ui.Rect(_panel);
                Ui.PlaceCentered(_panel, _posX, _posY, _w, _h);

                // 面板底：铺一层描边色的圆角块，往里缩 2px 再铺纸面，凑出 2px 边线。
                // 两块都铺满面板本身（而不是按坐标摆），面板拖动时不会脱节。
                Image rim = Ui.MakeSliced(_panel.transform, "PanelRim", Ui.Card(), Palette.CardRim, false);
                if (rim != null) Ui.Stretch(rim.gameObject, 0f);

                Image face = Ui.MakeSliced(_panel.transform, "PanelFace", Ui.Card(), Palette.PageBg, true);
                if (face != null) Ui.Inset(face.gameObject, 2f, 2f, 2f, 2f);

                BuildTitle();
                if (_mode == ConsoleMode.Simple) BuildSimpleWorkspace();
                else BuildWorkWorkspace();
                BuildStatus();

                // 开场白只在第一次建面板时打；换主题、换模式、换分辨率重建时保留原有输出
                if (!_welcomed)
                {
                    _welcomed = true;
                    Welcome();
                }
                Render(true);
                RefreshCards();
                SyncFeed();
                Ui.SetActive(_canvas.gameObject, false);
                Core.Log.Msg("[界面] 控制台面板已就绪（" + (int)_w + "×" + (int)_h
                    + "，" + ModeName() + "模式）。");
                return true;
            }
            catch (Exception ex)
            {
                Core.Log.Error("[界面] 建面板失败：" + ex);
                KillCanvas();
                return false;
            }
        }

        private static void Welcome()
        {
            // 简单模式只看得到最后一条（反馈带），所以最后一句得是句有用的话
            Out.Plain(Palette.TagVal("调试控制台") + Palette.TagMuted("　v" + Version + "　Probably Stolen Demo"));
            Out.Line(Palette.TagMuted("中间那些卡片点一下就能改，分类在")
                + Palette.TagKey(_mode == ConsoleMode.Simple ? "上面一排" : "左边一栏")
                + Palette.TagMuted("切。"));
            Out.Line("也可以直接敲命令：" + Palette.TagKey("stat") + " 看状态，"
                + Palette.TagKey("money add 10000") + " 加钱，"
                + Palette.TagKey("spawn 止痛药 5") + " 刷货。");
            Out.Line(Palette.TagMuted("按 ") + Palette.TagKey("help") + Palette.TagMuted(" 看全部命令，Tab 补全，↑↓ 翻历史。"));
            Out.Line(Palette.TagMuted("顶栏可以切 ") + Palette.TagKey("简单 / 混合 / 开发者") + Palette.TagMuted(" 三种模式。"));
        }

        /// <summary>顶栏：标题 + 副标题 + 模式切换 + 主题 + 关闭，整条都可以拖。</summary>
        private static void BuildTitle()
        {
            GameObject bar = Ui.New("TitleBar", _panel.transform);
            if (bar == null) return;
            Ui.Place(bar, 0f, 0f, _w, TitleH);
            _titleRect = Ui.Rect(bar);

            Image bg = Ui.MakeSliced(bar.transform, "TitleBg", Ui.Chip(), Palette.SlotBg, true);
            if (bg != null) Ui.Place(bg.gameObject, 2f, 2f, _w - 4f, TitleH - 2f);

            // 左侧一根主色竖条，给标题一点重量
            Image accent = Ui.MakeSliced(bar.transform, "Accent", Ui.Chip(), Palette.Accent, false);
            if (accent != null) Ui.Place(accent.gameObject, 16f, 14f, 4f, 20f);

            TextMeshProUGUI title = Ui.MakeText(bar.transform, "Title", "调试控制台", 19f,
                Palette.Title, Ui.AlignLeft, false);
            if (title != null) Ui.Place(title.gameObject, 30f, 10f, 240f, 28f);

            TextMeshProUGUI sub = Ui.MakeText(bar.transform, "Sub",
                "按住左边标题栏拖动", 12f, Palette.Muted, Ui.AlignLeft, false);

            // 顶栏右侧这一排按钮：从右往左依次摆，宽度变了也不用重算一串魔数
            float right = _w - 12f;

            _btnClose = Ui.MakeButton(bar.transform, "Close", "关闭", 13f,
                Palette.BtnDanger, Palette.Title, Close, Ui.AlignCenter);
            if (_btnClose != null) { _btnClose.Place(right - 40f, 11f, 40f, 26f); right -= 46f; }

            // 中英切换：按钮上写的是「切过去之后会是哪国话」
            _btnLang = Ui.MakeButton(bar.transform, "Lang", L10n.En ? "中" : "EN", 13f,
                Palette.BtnIdle, Palette.Body, ToggleLang, Ui.AlignCenter);
            if (_btnLang != null) { _btnLang.Place(right - 44f, 11f, 44f, 26f); right -= 50f; }

            _btnTheme = Ui.MakeButton(bar.transform, "Theme", Palette.IsLight ? "深色" : "亮色", 13f,
                Palette.BtnIdle, Palette.Body, ToggleTheme, Ui.AlignCenter);
            if (_btnTheme != null) { _btnTheme.Place(right - 68f, 11f, 68f, 26f); right -= 74f; }

            right = BuildModeSwitch(bar.transform, right);

            // 物品控制台：一整屏的物品图，点一件就刷一件
            _btnItems = Ui.MakeButton(bar.transform, "Items",
                _itemView ? "◀ 返回命令" : "物品控制台", 13f,
                _itemView ? Palette.NavOn : Palette.BtnIdle,
                _itemView ? Palette.Title : Palette.Body,
                () => ShowItems(!_itemView), Ui.AlignCenter);
            if (_btnItems != null) _btnItems.Place(right - 148f, 11f, 148f, 26f);

            // 整体缩放交互：按钮每次调整 5%，当前百分比始终可见并自动保存。
            float scaleRight = right - 148f - 8f;
            float scaleX = scaleRight - 112f;
            _btnScaleDown = Ui.MakeButton(bar.transform, "ScaleDown", "A-", 12f,
                Palette.BtnIdle, Palette.Body, () => AdjustUiScale(-5), Ui.AlignCenter);
            if (_btnScaleDown != null) _btnScaleDown.Place(scaleX, 11f, 32f, 26f);

            _scaleLabel = Ui.MakeText(bar.transform, "ScaleValue", UiScaleName(), 11f,
                Palette.Muted, Ui.AlignCenter, false);
            if (_scaleLabel != null) Ui.Place(_scaleLabel.gameObject, scaleX + 34f, 15f, 44f, 18f);

            _btnScaleUp = Ui.MakeButton(bar.transform, "ScaleUp", "A+", 12f,
                Palette.BtnIdle, Palette.Body, () => AdjustUiScale(5), Ui.AlignCenter);
            if (_btnScaleUp != null) _btnScaleUp.Place(scaleX + 80f, 11f, 32f, 26f);
            right = scaleX - 6f;

            // 副标题跟在标题屁股后面。「调试控制台」只有 5 个字，写死 x = 152 刚好；
            // 英文「Debug Console」快长一倍，会直接压到副标题上，所以得按实测宽度让位。
            if (sub != null)
            {
                float sx = 30f + Ui.TextW(title) + 18f;
                float sw = right - 10f - sx;
                if (sw < 90f) Ui.SetActive(sub.gameObject, false);
                else
                {
                    sub.text = Palette.Rt(Ui.Cut(L10n.S("A-/A+ 调整大小（75%-200%，每次 5%）；左侧可拖动"), 12f, sw));
                    Ui.Place(sub.gameObject, sx, 18f, sw, 18f);
                }
            }
        }

        /// <summary>
        /// 顶栏的模式分段控件。用三个并排的小按钮而不是一个循环按钮 ——
        /// 一眼能看出「现在在哪套模式、还能切到哪两套」。
        /// right 是这一排右边界，返回左边的下一个落点。
        /// </summary>
        private static float BuildModeSwitch(Transform bar, float right)
        {
            _modeBtns.Clear();
            string[] names = { "简单", "混合", "开发" };
            ConsoleMode[] vals = { ConsoleMode.Simple, ConsoleMode.Hybrid, ConsoleMode.Developer };
            float bw = 52f, bg2 = 4f;
            float x = right - (bw * 3f + bg2 * 2f);

            for (int i = 0; i < 3; i++)
            {
                ConsoleMode m = vals[i];
                bool on = _mode == m;
                UiButton b = Ui.MakeButton(bar.transform, "Mode" + i, names[i], 13f,
                    on ? Palette.NavOn : Palette.BtnIdle, on ? Palette.Title : Palette.Body,
                    () => SetMode(m), Ui.AlignCenter);
                if (b == null) continue;
                b.Place(x, 11f, bw, 26f);
                _modeBtns.Add(b);
                x += bw + bg2;
            }
            return right - (bw * 3f + bg2 * 2f) - 6f;
        }

        // ══════════════════════════════════════════════════════════════
        //  两种工作区
        // ══════════════════════════════════════════════════════════════

        /// <summary>混合 / 开发者：左栏分类与命令 + 中栏（卡片 + 输出）+ 输入行。</summary>
        private static void BuildWorkWorkspace()
        {
            bool dev = _mode == ConsoleMode.Developer;

            float top = TitleH + Gap;
            float inputY = _h - StatusH - Gap - InputH;
            float sideH = inputY - Gap - top;

            BuildSide(top, sideH, dev);

            float midX = SideW + Gap * 2;
            float midW = _w - SideW - Gap * 3;

            if (_itemView)
            {
                // 物品控制台：占满中栏，输入行还在，命令行随手能用
                BuildItemConsole(midX, top, midW, inputY - Gap - top);
                BuildInputRow(inputY);
                return;
            }

            if (dev)
            {
                // 开发者：输出区有多大给多大，卡片不掺和
                BuildLogArea(midX, top, midW, inputY - Gap - top);
            }
            else
            {
                // 混合：上面摆方块，下面留输出。方块是网格状的，给得少了只能看见半行，不好点
                float avail = inputY - Gap - top;
                float cardH = Mathf.Clamp(avail * 0.6f, 170f, 330f);
                BuildCardArea(midX, top, midW, cardH);
                float logY = top + cardH + Gap;
                BuildLogArea(midX, logY, midW, inputY - Gap - logY);
            }

            BuildInputRow(inputY);
        }

        /// <summary>简单：顶部横向分类 + 铺满的卡片墙 + 一行反馈。不摆命令行。</summary>
        private static void BuildSimpleWorkspace()
        {
            float top = TitleH + Gap;
            float feedY = _h - StatusH - Gap - FeedH;

            if (_itemView)
            {
                // 简单模式的物品控制台：整块铺开，下面只留一条反馈
                BuildItemConsole(14f, top, _w - 28f, feedY - Gap - top);
                BuildFeed(14f, feedY, _w - 28f, FeedH);
                return;
            }

            BuildGroupChips(top, 34f);
            top += 34f + Gap;

            BuildCardArea(14f, top, _w - 28f, feedY - Gap - top);
            BuildFeed(14f, feedY, _w - 28f, FeedH);
        }

        /// <summary>左栏：分类 + 当前分类的命令。</summary>
        private static void BuildSide(float y, float h, bool includeDev)
        {
            GameObject side = Ui.New("Side", _panel.transform);
            if (side == null) return;
            Ui.Place(side, 14f, y, SideW, h);

            Image bg = Ui.MakeSliced(side.transform, "SideBg", Ui.Card(), Palette.SlotBg, false);
            if (bg != null) Ui.Place(bg.gameObject, 0f, 0f, SideW, h);

            TextMeshProUGUI h1 = Ui.MakeText(side.transform, "H1", "分类", 12f, Palette.Muted, Ui.AlignLeft, false);
            if (h1 != null) Ui.Place(h1.gameObject, 16f, 12f, 200f, 16f);

            float cy = 34f;
            _groupBtns.Clear();
            BuildGroupButton(side.transform, "all", "全部", cy); cy += 34f;
            List<CmdGroup> groups = Cmds.VisibleGroups(includeDev);
            for (int i = 0; i < groups.Count; i++)
            {
                BuildGroupButton(side.transform, groups[i].Key, groups[i].Name, cy);
                cy += 34f;
            }

            Ui.MakeLine(side.transform, "Line1", 16f, cy + 4f, SideW - 32f, Palette.A(Palette.Divider, 0.5f));
            cy += 16f;

            TextMeshProUGUI h2 = Ui.MakeText(side.transform, "H2", "命令", 12f, Palette.Muted, Ui.AlignLeft, false);
            if (h2 != null) Ui.Place(h2.gameObject, 16f, cy, 200f, 16f);
            cy += 22f;

            _cmdScroll = new UiScroll();
            _cmdScroll.Build(side.transform, "CmdScroll", 8f, cy, SideW - 16f, Mathf.Max(60f, h - cy - 10f));
            _cmdContent = _cmdScroll.Content != null ? _cmdScroll.Content.gameObject : null;

            RebuildCmdList();
        }

        private static void BuildGroupButton(Transform parent, string key, string name, float y)
        {
            bool on = _group == key;
            UiButton b = Ui.MakeButton(parent, "Grp_" + key, "", 14f,
                on ? Palette.NavOn : Palette.BtnIdle,
                on ? Palette.Title : Palette.Body,
                () => SetGroup(key), Ui.AlignLeft);
            if (b == null) return;
            b.Place(14f, y, SideW - 28f, 28f);

            TextMeshProUGUI t = Ui.MakeText(b.Go.transform, "T", name, 14f,
                on ? Palette.Title : Palette.Sub, Ui.AlignLeft, false);
            if (t != null) Ui.Place(t.gameObject, 12f, 5f, SideW - 60f, 18f);

            _groupBtns.Add(b);
        }

        /// <summary>简单模式顶上那排横向分类标签。</summary>
        private static void BuildGroupChips(float y, float h)
        {
            _groupBtns.Clear();
            float x = 14f;
            x = BuildGroupChip("all", "全部", x, y, h);
            List<CmdGroup> groups = Cmds.VisibleGroups(false);
            for (int i = 0; i < groups.Count; i++)
            {
                x = BuildGroupChip(groups[i].Key, groups[i].Name, x, y, h);
            }
        }

        private static float BuildGroupChip(string key, string name, float x, float y, float h)
        {
            bool on = _group == key;
            float w = 26f + name.Length * 15f;
            UiButton b = Ui.MakeButton(_panel.transform, "Grp_" + key, name, 14f,
                on ? Palette.NavOn : Palette.BtnIdle,
                on ? Palette.Title : Palette.Body,
                () => SetGroup(key), Ui.AlignCenter);
            if (b == null) return x;
            b.Place(x, y, w, h);
            _groupBtns.Add(b);
            return x + w + 8f;
        }

        /// <summary>
        /// 侧栏 / 顶部标签选分类：选中的分类直接进它的子菜单，「全部」回母菜单。
        /// </summary>
        private static void SetGroup(string key)
        {
            if (_group == key) return;
            _group = key;
            SyncGroupButtons();
            RebuildCmdList();
            _subOpen = key != "all";
            RebuildCards();      // 方块墙跟着分类一起换，不重新建滚动容器
        }

        /// <summary>分类按钮的高亮：选中的那个亮起来。</summary>
        private static void SyncGroupButtons()
        {
            for (int i = 0; i < _groupBtns.Count; i++)
            {
                UiButton b = _groupBtns[i];
                string k = b.Go != null ? b.Go.name.Substring(4) : "";
                bool on = k == _group;
                b.SetBg(on ? Palette.NavOn : Palette.BtnIdle);
                b.SetColor(on ? Palette.Title : Palette.Sub);
            }
        }

        /// <summary>按当前分类重建命令列表。</summary>
        private static void RebuildCmdList()
        {
            if (_cmdContent == null || _cmdScroll == null) return;

            // 旧行先藏起来再销毁，否则 Destroy 要到帧末才生效，新旧会重叠一帧
            Transform t = _cmdContent.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject go = t.GetChild(i).gameObject;
                Ui.SetActive(go, false);
                UnityEngine.Object.Destroy(go);
            }
            _cmdBtns.Clear();

            List<Cmd> list = new List<Cmd>();
            if (_group == "all")
            {
                // 「全部」也要跟侧栏一致：简单 / 混合模式里不列反射那一堆
                List<CmdGroup> groups = Cmds.VisibleGroups(_mode == ConsoleMode.Developer);
                for (int g = 0; g < groups.Count; g++) list.AddRange(Cmds.InGroup(groups[g].Key));
            }
            else
            {
                list = Cmds.InGroup(_group);
            }

            float y = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                Cmd c = list[i];
                UiButton b = Ui.MakeButton(_cmdContent.transform, "Cmd_" + i, "", 14f,
                    Palette.BtnIdle, Palette.Body, () => PickCommand(c), Ui.AlignLeft);
                if (b == null) continue;
                b.Place(0f, y, _cmdScroll.ViewW, CmdRowH - 4f);

                TextMeshProUGUI title = Ui.MakeText(b.Go.transform, "T", c.Name, 14f,
                    Palette.Key, Ui.AlignLeft, false);
                if (title != null) Ui.Place(title.gameObject, 12f, 6f, _cmdScroll.ViewW - 24f, 17f);

                string hint = c.Usage;
                if (!string.IsNullOrEmpty(c.Preset)) hint = L10n.Raw("▶ " + c.Preset);
                TextMeshProUGUI sub = Ui.MakeText(b.Go.transform, "S",
                    Ui.Cut(L10n.S(hint), 11f, _cmdScroll.ViewW - 24f), 11f, Palette.Muted, Ui.AlignLeft, false);
                if (sub != null) Ui.Place(sub.gameObject, 12f, 24f, _cmdScroll.ViewW - 24f, 15f);

                _cmdBtns.Add(b);
                y += CmdRowH;
            }

            _cmdScroll.SetContentHeight(y);
            _cmdScroll.ToTop();
        }

        /// <summary>输出区。混合 / 开发者模式共用，只是给的尺寸不一样。</summary>
        private static void BuildLogArea(float x, float y, float w, float h)
        {
            GameObject main = Ui.New("Log", _panel.transform);
            if (main == null) return;
            Ui.Place(main, x, y, w, h);

            Image bg = Ui.MakeSliced(main.transform, "LogBg", Ui.Card(), Palette.SlotBg, false);
            if (bg != null) Ui.Place(bg.gameObject, 0f, 0f, w, h);

            TextMeshProUGUI cap = Ui.MakeText(main.transform, "Cap", "输出", 12f,
                Palette.Muted, Ui.AlignLeft, false);
            if (cap != null) Ui.Place(cap.gameObject, 12f, 6f, 160f, 16f);

            float head = 26f;
            _logScroll = new UiScroll();
            _logScroll.Build(main.transform, "LogScroll", 12f, head, w - 24f, Mathf.Max(40f, h - head - 10f));

            // 行对象池：只做够一屏的行，滚到哪儿就画哪儿
            int need = Mathf.CeilToInt(_logScroll.ViewH / LogRowH) + 2;
            _rows.Clear();
            for (int i = 0; i < need; i++)
            {
                TextMeshProUGUI t = Ui.MakeText(_logScroll.Content, "Row" + i, "", LogFont,
                    Palette.Body, Ui.AlignLeft, false);
                if (t == null) continue;
                Ui.Place(t.gameObject, 2f, 0f, _logScroll.ViewW - 6f, LogRowH);
                _rows.Add(t);
            }
        }

        // ── 操作方块：母菜单 → 子菜单 ────────────────────────────────

        /// <summary>方块墙的滚动容器。简单 / 混合模式共用；换页只重铺内容，容器不动。</summary>
        private static void BuildCardArea(float x, float y, float w, float h)
        {
            _cardScroll = new UiScroll();
            _cardScroll.Build(_panel.transform, "CardScroll", x, y, w, h);
            _cardContent = _cardScroll.Content != null ? _cardScroll.Content.gameObject : null;
            RebuildCards();
        }

        /// <summary>
        /// 重铺方块墙。
        /// 没进分类 = 母菜单（一个分类一块大方块）；进了分类 = 这个分类的操作方块。
        /// 方块全部来自命令表（Cmds.Cards），所以「加一个功能」永远只改一处 ——
        /// 命令、Tab 补全、help、方块会自动同时出现，不存在两套清单对不上的问题。
        /// </summary>
        private static void RebuildCards()
        {
            if (_cardContent == null || _cardScroll == null) return;

            Transform t = _cardContent.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject go = t.GetChild(i).gameObject;
                Ui.SetActive(go, false);          // Destroy 帧末才生效，先藏掉免得重叠一帧
                UnityEngine.Object.Destroy(go);
            }
            _cardViews.Clear();
            _cardInputs.Clear();
            try { CloseDrop(); } catch { _dropOpen = null; }   // 方块重建，旧的下拉弹层已经跟着没了

            if (_subOpen) BuildSubPage();
            else BuildMotherPage();
        }

        /// <summary>一屏放几列方块。300 上下最舒服：混和模式 3 列、简单模式 4 列。</summary>
        private static int TileCols(float w)
        {
            return Mathf.Clamp(Mathf.FloorToInt((w + TilePad) / 268f), 2, 4);
        }

        // ── 母菜单：一个分类一块 ──────────────────────────────────────

        private static void BuildMotherPage()
        {
            List<CmdGroup> groups = Cmds.VisibleGroups(_mode == ConsoleMode.Developer);

            float full = _cardScroll.ViewW;
            int cols = TileCols(full);
            float tw = (full - TilePad * (cols - 1)) / cols;

            int total = Cmds.Cards("all").Count;
            int totalCmd = 0;
            for (int i = 0; i < groups.Count; i++) totalCmd += Cmds.InGroup(groups[i].Key).Count;

            int n = 0;
            BuildGroupTile("all", "全部功能", "所有分类的功能都摊在这儿", total, totalCmd,
                (n % cols) * (tw + TilePad), (n / cols) * (GroupTileH + TilePad), tw);
            n++;

            for (int i = 0; i < groups.Count; i++)
            {
                CmdGroup g = groups[i];
                BuildGroupTile(g.Key, g.Name, g.Hint,
                    Cmds.Cards(g.Key).Count, Cmds.InGroup(g.Key).Count,
                    (n % cols) * (tw + TilePad), (n / cols) * (GroupTileH + TilePad), tw);
                n++;
            }

            int rows = (n + cols - 1) / cols;
            _cardScroll.SetContentHeight(Mathf.Max(0f, rows * (GroupTileH + TilePad) - TilePad));
            _cardScroll.ToTop();
        }

        /// <summary>一块分类方块：名字 + 有几个功能 + 一句说明，整块都能点。</summary>
        private static void BuildGroupTile(string key, string name, string hint, int cards, int cmds,
            float x, float y, float w)
        {
            UiButton b = Ui.MakeButton(_cardContent.transform, "GTile_" + key, "", 15f,
                Palette.CardBg, Palette.Body, () => OpenGroup(key), Ui.AlignLeft);
            if (b == null) return;
            b.Place(x, y, w, GroupTileH);
            if (b.Label != null) Ui.SetActive(b.Label.gameObject, false);

            TextMeshProUGUI title = Ui.MakeText(b.Go.transform, "T", name, 17f,
                Palette.Title, Ui.AlignLeft, false);
            if (title != null) Ui.Place(title.gameObject, 16f, 14f, w - 32f, 22f);

            string stat = cmds + " 条命令";
            if (cards > 0) stat += "　" + cards + " 个卡片";
            TextMeshProUGUI st = Ui.MakeText(b.Go.transform, "S", stat, 12f,
                Palette.Value, Ui.AlignLeft, false);
            if (st != null) Ui.Place(st.gameObject, 16f, 40f, w - 32f, 16f);

            if (!string.IsNullOrEmpty(hint))
            {
                TextMeshProUGUI h = Ui.MakeText(b.Go.transform, "H",
                    Ui.Cut(L10n.S(hint), 11f, w - 32f), 11f, Palette.Muted, Ui.AlignLeft, false);
                if (h != null) Ui.Place(h.gameObject, 16f, 62f, w - 32f, 16f);
            }

            TextMeshProUGUI go2 = Ui.MakeText(b.Go.transform, "G", "打开 ▶", 12f,
                Palette.Sub, Ui.AlignLeft, false);
            if (go2 != null) Ui.Place(go2.gameObject, 16f, GroupTileH - 28f, w - 32f, 16f);
        }

        // ── 子菜单：一个功能一块 ──────────────────────────────────────

        private static void BuildSubPage()
        {
            float full = _cardScroll.ViewW;

            UiButton back = Ui.MakeButton(_cardContent.transform, "Back", "◀ 返回", 13f,
                Palette.BtnIdle, Palette.Body, () => OpenGroup(null), Ui.AlignCenter);
            if (back != null) back.Place(0f, 0f, 92f, 28f);

            CmdGroup g = _group == "all" ? null : Cmds.GroupOf(_group);
            string name = g != null ? g.Name : "全部功能";
            string hint = g != null ? g.Hint : "所有分类的功能都摊在这儿";

            TextMeshProUGUI cap = Ui.MakeText(_cardContent.transform, "Cap",
                Ui.Cut(L10n.S(name), 15f, 160f), 15f,
                Palette.Title, Ui.AlignLeft, false);
            if (cap != null) Ui.Place(cap.gameObject, 104f, 5f, 160f, 20f);

            TextMeshProUGUI capHint = Ui.MakeText(_cardContent.transform, "CapH",
                Ui.Cut(L10n.S(hint), 11f, Mathf.Max(60f, full - 290f)), 11f, Palette.Muted, Ui.AlignLeft, false);
            if (capHint != null) Ui.Place(capHint.gameObject, 104f + 170f, 9f, Mathf.Max(60f, full - 290f), 16f);

            List<Card> list = Cmds.Cards(_group);
            int cols = TileCols(full);
            float tw = (full - TilePad * (cols - 1)) / cols;
            float top = 28f + TilePad;

            int col = 0;
            float y = top, rowH = 0f, used = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                Card c = list[i];
                if (c == null) continue;

                float h = TileHeightOf(c);
                if (rowH < h) rowH = h;
                BuildTile(c, col * (tw + TilePad), y, tw, h);

                col++;
                if (col >= cols) { y += rowH + TilePad; used = y; col = 0; rowH = 0f; }
            }
            if (col > 0) used = y + rowH + TilePad;

            if (list.Count == 0)
            {
                TextMeshProUGUI none = Ui.MakeText(_cardContent.transform, "None",
                    "这个分类只有命令，没有卡片 —— 去左边点一条，或者敲命令行。",
                    13f, Palette.Muted, Ui.AlignLeft, false);
                if (none != null) Ui.Place(none.gameObject, 0f, top, full, 20f);
            }

            _cardScroll.SetContentHeight(Mathf.Max(0f, used - TilePad));
            _cardScroll.ToTop();
        }

        /// <summary>方块多高：带下拉框的方块要多留一行（头 + 底下的输入行或按钮）。</summary>
        private static float TileHeightOf(Card c)
        {
            return c == null || c.Pick == null ? TileH : TileH + 12f;
        }

        /// <summary>点分类方块进子菜单；key 传 null 回母菜单。</summary>
        private static void OpenGroup(string key)
        {
            _subOpen = !string.IsNullOrEmpty(key);
            if (_subOpen)
            {
                _group = key;
                SyncGroupButtons();
                RebuildCmdList();
            }
            RebuildCards();
        }

        /// <summary>铺一个功能方块：标题 / 实时读数 / 说明 + 按钮行（输入行 / 下拉框行）。</summary>
        private static void BuildTile(Card c, float x, float y, float w, float h)
        {
            GameObject go = Ui.New("Tile_" + c.Title, _cardContent.transform);
            if (go == null) return;
            Ui.Place(go, x, y, w, h);

            Image rim = Ui.MakeSliced(go.transform, "Rim", Ui.Card(), Palette.CardRim, false);
            if (rim != null) Ui.Place(rim.gameObject, 0f, 0f, w, h);
            Image face = Ui.MakeSliced(go.transform, "Face", Ui.Card(), Palette.CardBg, false);
            if (face != null) Ui.Place(face.gameObject, 2f, 2f, w - 4f, h - 4f);

            TextMeshProUGUI title = Ui.MakeText(go.transform, "T", c.Title, 15f,
                Palette.Title, Ui.AlignLeft, false);
            if (title != null) Ui.Place(title.gameObject, 14f, 12f, w - 28f, 20f);

            // 实时读数单独一行：五个区的声望这种长读数也塞得下，放不下就截
            if (c.Read != null)
            {
                TextMeshProUGUI val = Ui.MakeText(go.transform, "V", "—", 13f,
                    Palette.Value, Ui.AlignLeft, false);
                if (val != null)
                {
                    Ui.Place(val.gameObject, 14f, 36f, w - 28f, 18f);
                    _cardViews.Add(new CardView { Text = val, Src = c.Read, MaxW = w - 28f });
                }
            }

            if (!string.IsNullOrEmpty(c.Note))
            {
                // 先翻成当前语言再按宽度截 —— 反过来的话，英文比中文长一倍会顶出卡片边框。
                // 没有下拉框的卡片下面还有一整行空着，摊两行，英文也放得下。
                bool twoLine = c.Pick == null;
                float noteW = w - 28f;
                float noteY = twoLine ? 56f : 60f;
                TextMeshProUGUI note = Ui.MakeText(go.transform, "N",
                    Ui.Cut(L10n.S(c.Note), 11f, twoLine ? noteW * 2f : noteW),
                    11f, Palette.Muted, Ui.AlignLeft, twoLine);
                if (note != null) Ui.Place(note.gameObject, 14f, noteY, noteW, twoLine ? 30f : 16f);
            }

            // 下拉卡是「头 + 底下再一行」两层内容，从说明下面起铺，卡片底边才留得住；
            // 其它卡只有一行，照旧贴着底边往上放。
            float rowY = c.Pick != null ? 84f : h - 42f;
            if (c.Pick != null) BuildTilePick(go.transform, c, w, x, y, rowY);
            else if (c.Input != null) BuildTileInput(go.transform, c, w, rowY);
            else if (c.Acts != null) BuildTileActs(go.transform, c, w, rowY);
        }

        /// <summary>
        /// 方块上的「下拉框 + 输入行」：先在下拉框里选一个（比如哪个区），
        /// 再填数字，拼成一条命令跑。
        /// 弹层挂在面板底下而不是方块里 —— 挂在方块里会被后来的方块盖住。
        /// </summary>
        private static void BuildTilePick(Transform tile, Card c, float w,
            float tileX, float tileY, float rowY)
        {
            CardPick p = c.Pick;
            if (p == null) return;
            if (p.Provider == null && (p.Names == null || p.Names.Length == 0)) return;

            ItemDrop d = new ItemDrop();
            d.Prefix = p.Prefix;
            d.Provider = p.Provider;
            d.EmptyHint = p.EmptyHint;
            d.Names = p.Names != null ? new List<string>(p.Names) : new List<string>();
            d.Keys = p.Keys != null ? new List<string>(p.Keys) : new List<string>();
            if (d.Names.Count == 0) FillDrop(d);   // 现取的选项：建卡片时先问一次，头上就有字
            d.Sel = Mathf.Clamp(p.Sel, 0, Mathf.Max(0, d.Names.Count - 1));
            d.W = w - 28f;
            d.X = _cardScroll.X + tileX + 14f;
            d.Y = _cardScroll.Y + tileY - _cardScroll.Offset + rowY;
            d.Parent = _panel.transform;

            d.Head = Ui.MakeButton(tile, "Pick_" + p.Prefix, "", 13f,
                Palette.BtnIdle, Palette.Body, () => ToggleDrop(d), Ui.AlignLeft);
            if (d.Head != null) d.Head.Place(14f, rowY, d.W, ItemDropH);
            PaintDropHead(d);
            BuildDropPopup(d);
            d.OnPick = () => { p.Sel = d.Sel; };

            float inputY = rowY + ItemDropH + 6f;
            float btnW = 76f;

            // 只选不填的（比如「触发指定事件」）：下拉框底下就一个执行按钮，没有输入框
            if (p.NoInput)
            {
                UiButton go = Ui.MakeButton(tile, "Go", p.Button, 13f,
                    Palette.BtnRun, Palette.Title,
                    () => RunCardPick(null, d, p, null), Ui.AlignCenter);
                if (go != null) go.Place(w - 14f - 120f, inputY, 120f, CardRowH);
                return;
            }

            float iw = Math.Max(60f, w - 28f - btnW - 8f);

            TMP_InputField f = null;
            f = Ui.MakeInput(tile, "In", p.Hint, 14f,
                Palette.SlotBg, Palette.Body, Palette.Muted,
                s => RunCardPick(f, d, p, s));
            if (f != null)
            {
                Ui.Place(f.gameObject, 14f, inputY, iw, CardRowH);
                _cardInputs.Add(f);
            }

            UiButton b = Ui.MakeButton(tile, "Go", p.Button, 13f,
                Palette.BtnRun, Palette.Title,
                () => RunCardPick(f, d, p, Ui.TextOf(f, "")), Ui.AlignCenter);
            if (b != null) b.Place(14f + iw + 8f, inputY, btnW, CardRowH);
        }

        /// <summary>把下拉框的选项重新取一遍：事件清单这类进档后才读得到，得每次展开时现问。</summary>
        private static void FillDrop(ItemDrop d)
        {
            if (d == null || d.Provider == null) return;
            List<KeyValuePair<string, string>> list = null;
            try { list = d.Provider(); }
            catch (Exception ex) { Core.Debug("[界面] 取下拉选项失败：" + ex.Message); }

            d.Names.Clear();
            d.Keys.Clear();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    d.Keys.Add(list[i].Key);
                    d.Names.Add(list[i].Value);
                }
            }
            if (d.Names.Count == 0) d.Names.Add(d.EmptyHint ?? "（还没得选）");
        }

        /// <summary>下拉框选的 + 框里填的，拼成命令跑。</summary>
        private static void RunCardPick(TMP_InputField f, ItemDrop d, CardPick p, string text)
        {
            if (p == null) return;

            List<string> keys = (d != null && d.Keys.Count > 0)
                ? d.Keys : (p.Keys != null ? new List<string>(p.Keys) : new List<string>());
            if (keys.Count == 0)
            {
                Out.Warn(p.EmptyHint ?? "这个下拉框现在还没得选。");
                return;
            }

            string s = (text ?? "").Trim();
            if (!p.NoInput && s.Length == 0) { Out.Warn("先在框里填内容。"); return; }

            int i = d != null ? d.Sel : p.Sel;
            i = Mathf.Clamp(i, 0, keys.Count - 1);
            if (f != null) Ui.SetTextOf(f, "");
            RunCard(p.Cmd + keys[i] + (p.NoInput ? "" : " " + s));
        }

        /// <summary>方块上的按钮行：平分宽度，按 Tone 取底色（1 主操作蓝 / 2 危险红 / 0 普通）。</summary>
        private static void BuildTileActs(Transform parent, Card c, float w, float y)
        {
            int n = c.Acts.Length;
            if (n <= 0) return;

            float gap = 6f;
            float bw = (w - 28f - gap * (n - 1)) / n;
            float x = 14f;

            for (int i = 0; i < n; i++)
            {
                CardAct act = c.Acts[i];
                if (act == null) continue;

                Color bg = act.Tone == 1 ? Palette.BtnRun
                         : act.Tone == 2 ? Palette.BtnDanger : Palette.BtnIdle;
                Color fg = act.Tone == 0 ? Palette.Body : Palette.Title;

                UiButton b = Ui.MakeButton(parent, "Act" + i, act.Label, 13f, bg, fg,
                    () => RunCard(act.Cmd), Ui.AlignCenter);
                if (b == null) continue;
                b.Place(x, y, bw, CardRowH);
                x += bw + gap;
            }
        }

        /// <summary>方块上的输入行：一个输入框 + 一个执行按钮（回车也认）。</summary>
        private static void BuildTileInput(Transform parent, Card c, float w, float y)
        {
            CardInput ci = c.Input;
            float btnW = 76f;
            float iw = w - 28f - btnW - 8f;

            TMP_InputField f = null;
            f = Ui.MakeInput(parent, "In", ci.Hint, 14f,
                Palette.SlotBg, Palette.Body, Palette.Muted,
                s => RunCardInput(f, ci, s));
            if (f != null)
            {
                Ui.Place(f.gameObject, 14f, y, iw, CardRowH);
                _cardInputs.Add(f);
            }

            UiButton b = Ui.MakeButton(parent, "Go", ci.Button, 13f,
                Palette.BtnRun, Palette.Title,
                () => RunCardInput(f, ci, Ui.TextOf(f, "")), Ui.AlignCenter);
            if (b != null) b.Place(14f + iw + 8f, y, btnW, CardRowH);
        }

        // ══════════════════════════════════════════════════════════════
        //  物品控制台
        //
        //  独立的一屏：顶栏（分类 / 筛选 / 搜索 / 收藏）+ 物品网格 + 翻页 + 右栏表单。
        //  格子里的图是游戏自己的物品贴图（RenderHandler 从图集取）；
        //  点一格选中，右栏填数量、选位置，最后点生成 —— 和手敲 spawn 走同一条命令。
        // ══════════════════════════════════════════════════════════════

        public static bool ItemView { get { return _itemView; } }

        /// <summary>切到物品控制台 / 切回命令界面。顶栏按钮和 ui 命令都走这里。</summary>
        public static void ShowItems(bool on)
        {
            if (_itemView == on) return;
            _itemView = on;
            if (_open) Rebuild();
            Out.Line(on ? "打开物品控制台：点一件货 → 填数量 → 选位置 → 生成。"
                        : "回到命令界面。");
        }

        /// <summary>面板重建时把物品控制台留下的引用全清掉。</summary>
        private static void KillItemConsole()
        {
            CloseClearAllDialog();
            _btnItems = null;
            _itemRoot = null;
            _itemGrid = null;
            _itemSelIcon = null;
            _itemSelName = null;
            _itemSelInfo = null;
            _itemPageText = null;
            _itemPrev = _itemNext = null;
            _itemCount = null;
            _itemSearch = null;
            _itemDesk = _itemStore = null;
            _itemFavBtn = null;
            _dropDir = _dropKind = _dropOpen = null;
            _itemCells.Clear();
            _itemGridW = _itemGridH = 0f;
            _itemLastQuery = "\u0000";
        }

        private static void BuildItemConsole(float x, float y, float w, float h)
        {
            LoadFavs();

            _itemRoot = Ui.New("ItemConsole", _panel.transform);
            if (_itemRoot == null) return;
            Ui.Place(_itemRoot, x, y, w, h);

            Image bg = Ui.MakeSliced(_itemRoot.transform, "Bg", Ui.Card(), Palette.SlotBg, true);
            if (bg != null) Ui.Place(bg.gameObject, 0f, 0f, w, h);

            const float pad = 12f;
            const float gap = 10f;
            const float rightW = 224f;
            const float pagerH = 34f;

            float innerW = w - pad * 2f;
            float innerH = h - pad * 2f;
            float gridTop = pad + ItemDropH + gap;
            float gridBottom = pad + innerH - pagerH - gap;
            float gridW = Mathf.Max(240f, innerW - rightW - gap);
            float gridH = Mathf.Max(140f, gridBottom - gridTop);

            BuildItemToolbar(pad, pad, innerW, rightW);
            BuildItemGrid(pad, gridTop, gridW, gridH);
            BuildItemPager(pad, gridW, gridBottom + 3f);
            BuildItemSide(pad + innerW - rightW, gridTop, rightW, innerH - ItemDropH - gap);

            // 弹层最后建：这样它盖在网格和右栏上面，而不是被它们压住
            BuildDropPopup(_dropDir);
            BuildDropPopup(_dropKind);

            _itemLastQuery = _itemQuery;
            RefreshItemGrid();
            RefreshItemSide();
        }

        // ── 顶栏：分类 / 筛选 / 搜索 / 收藏 / 库存操作 ────────────────

        private static void BuildItemToolbar(float x, float y, float w, float rightW)
        {
            const float favW = 104f;
            const float clearW = 140f;
            float searchX = x + 360f;
            float favX = x + w - favW - clearW - 8f;
            float searchW = Mathf.Max(120f, favX - searchX - 8f);

            // 分类：全部分类 + 游戏里的 29 个物品目录
            List<string> keys = new List<string> { "" };
            List<string> names = new List<string> { "全部分类" };
            List<KeyValuePair<string, string>> dirs = GameApi.ItemDirectories();
            for (int i = 0; i < dirs.Count; i++)
            {
                keys.Add(dirs[i].Key);
                names.Add(dirs[i].Value);
            }
            int sel = keys.IndexOf(_itemDir);
            // 这两个头要装下「分类：全部分类」这种长度的字，窄了就会被截成「分类：全部…」
            _dropDir = BuildItemDrop("分类", x, y, 176f, keys, names, sel < 0 ? 0 : sel);
            if (_dropDir != null)
            {
                _dropDir.OnPick = () =>
                {
                    _itemDir = _dropDir.Keys[_dropDir.Sel];
                    _itemPage = 0;
                    RefreshItemGrid();
                };
            }

            // 筛选
            _dropKind = BuildItemDrop("筛选", x + 184f, y, 168f,
                new List<string> { "all", "fav", "own" },
                new List<string> { "全部物品", "只看收藏", "店里已有" }, _itemKind);
            if (_dropKind != null)
            {
                _dropKind.OnPick = () =>
                {
                    _itemKind = _dropKind.Sel;
                    _itemPage = 0;
                    PaintItemFav();
                    RefreshItemGrid();
                };
            }

            _itemSearch = Ui.MakeInput(_itemRoot.transform, "Search",
                "搜物品名或 ID", 13f, Palette.SlotBg, Palette.Body, Palette.Muted, ItemSearchGo);
            if (_itemSearch != null)
            {
                Ui.Place(_itemSearch.gameObject, searchX, y, searchW, ItemDropH);
                _cardInputs.Add(_itemSearch);
                Ui.SetTextOf(_itemSearch, _itemQuery);
            }

            _itemFavBtn = Ui.MakeButton(_itemRoot.transform, "Fav", "★ 收藏", 13f,
                Palette.BtnIdle, Palette.Body, ToggleFavFilter, Ui.AlignCenter);
            if (_itemFavBtn != null) _itemFavBtn.Place(favX, y, favW, ItemDropH);
            PaintItemFav();

            UiButton clear = Ui.MakeButton(_itemRoot.transform, "ClearAll", "清空库存…", 12f,
                Palette.BtnDanger, Palette.Title, ClearAllGo, Ui.AlignCenter);
            if (clear != null) clear.Place(x + w - clearW, y, clearW, ItemDropH);
        }

        private static void ItemSearchGo(string s)
        {
            _itemQuery = (s ?? "").Trim();
            _itemLastQuery = _itemQuery;
            _itemPage = 0;
            RefreshItemGrid();
        }

        private static void ToggleFavFilter()
        {
            _itemKind = _itemKind == 1 ? 0 : 1;
            if (_dropKind != null)
            {
                _dropKind.Sel = _itemKind;
                PaintDropHead(_dropKind);
                RepaintDropRows(_dropKind);
            }
            _itemPage = 0;
            PaintItemFav();
            RefreshItemGrid();
        }

        private static void PaintItemFav()
        {
            if (_itemFavBtn == null) return;
            bool on = _itemKind == 1;
            _itemFavBtn.SetBg(on ? Palette.NavOn : Palette.BtnIdle);
            _itemFavBtn.SetColor(on ? Palette.Title : Palette.Body);
        }

        // ── 下拉框 ────────────────────────────────────────────────────

        private static ItemDrop BuildItemDrop(string prefix, float x, float y, float w,
            List<string> keys, List<string> names, int sel)
        {
            ItemDrop d = new ItemDrop();
            d.Keys = keys;
            d.Names = names;
            d.Prefix = prefix;
            d.X = x;
            d.Y = y;
            d.W = w;
            d.Sel = Mathf.Clamp(sel, 0, Mathf.Max(0, names.Count - 1));

            d.Head = Ui.MakeButton(_itemRoot.transform, "Drop_" + prefix, "", 13f,
                Palette.BtnIdle, Palette.Body, () => ToggleDrop(d), Ui.AlignLeft);
            if (d.Head != null) d.Head.Place(x, y, w, ItemDropH);
            PaintDropHead(d);
            return d;
        }

        private static void BuildDropPopup(ItemDrop d)
        {
            if (d == null) return;
            Transform parent = d.Parent != null ? d.Parent : (_itemRoot != null ? _itemRoot.transform : null);
            if (parent == null) return;

            const float rowH = 26f;
            float popW = Mathf.Max(d.W, 150f);
            // 分类有二十多个，弹层要留够高度，剩下的靠滚轮滚（以前封顶 260，下面那截根本够不着）
            float popH = Mathf.Min(360f, d.Names.Count * rowH + 10f);

            d.Popup = Ui.New("DropPopup_" + d.Prefix, parent);
            if (d.Popup == null) return;
            Ui.Place(d.Popup, d.X, d.Y + ItemDropH + 4f, popW, popH);

            Image rim = Ui.MakeSliced(d.Popup.transform, "Rim", Ui.Card(), Palette.CardRim, false);
            if (rim != null) Ui.Place(rim.gameObject, 0f, 0f, popW, popH);
            Image face = Ui.MakeSliced(d.Popup.transform, "Face", Ui.Card(), Palette.CardBg, true);
            if (face != null) Ui.Place(face.gameObject, 2f, 2f, popW - 4f, popH - 4f);

            UiScroll sc = new UiScroll();
            sc.Build(d.Popup.transform, "Scroll", 5f, 5f, popW - 10f, popH - 10f);
            if (sc.Content == null) return;
            sc.Stick = false;
            d.Scroll = sc;

            d.Rows.Clear();
            for (int i = 0; i < d.Names.Count; i++)
            {
                int idx = i;
                UiButton b = Ui.MakeButton(sc.Content, "Row" + i, d.Names[i], 13f,
                    Palette.BtnIdle, Palette.Body, () => PickDrop(d, idx), Ui.AlignLeft);
                if (b != null) b.Place(0f, i * rowH, sc.ViewW - 2f, rowH - 2f);
                d.Rows.Add(b);
            }
            sc.SetContentHeight(d.Names.Count * rowH);
            sc.ToTop();
            RepaintDropRows(d);
            Ui.SetActive(d.Popup, false);
        }

        private static void PaintDropHead(ItemDrop d)
        {
            if (d == null || d.Head == null) return;
            string n = (d.Sel >= 0 && d.Sel < d.Names.Count) ? d.Names[d.Sel] : "—";
            // 按钮自己会按「宽度 - 左右留白」再截一次，所以这里得先给末尾那个 ▼ 留出地儿，
            // 不然 ▼ 会被第二次截断吃掉，头上就只剩个省略号了
            d.Head.SetText(Ui.Cut(L10n.S(d.Prefix + "：" + n), 13f, d.W - 48f) + "▼");
        }

        private static void RepaintDropRows(ItemDrop d)
        {
            if (d == null) return;
            for (int i = 0; i < d.Rows.Count; i++)
            {
                UiButton b = d.Rows[i];
                if (b == null) continue;
                bool on = i == d.Sel;
                b.SetBg(on ? Palette.NavOn : Palette.BtnIdle);
                b.SetColor(on ? Palette.Title : Palette.Body);
            }
        }

        private static void ToggleDrop(ItemDrop d)
        {
            _dropFrame = Time.frameCount;
            if (_dropOpen == d) { CloseDrop(); return; }
            OpenDrop(d);
        }

        private static void OpenDrop(ItemDrop d)
        {
            _dropFrame = Time.frameCount;
            if (_dropOpen != null && _dropOpen != d) Ui.SetActive(_dropOpen.Popup, false);
            _dropOpen = d;
            if (d == null) return;

            // 选项是现取的（事件清单得进档才读得到）：展开时重取一遍，
            // 行数是照当时那份选项建的，所以弹层整个重建。
            if (d.Provider != null)
            {
                FillDrop(d);
                int max = Mathf.Max(0, d.Names.Count - 1);
                if (d.Sel > max) d.Sel = max;
                if (d.Head != null) PaintDropHead(d);
                if (d.Popup != null) UnityEngine.Object.Destroy(d.Popup);
                d.Popup = null;
                d.Rows.Clear();
                BuildDropPopup(d);
                if (d.OnPick != null)
                {
                    try { d.OnPick(); }
                    catch (Exception ex) { Core.Debug("[界面] 下拉框同步选择失败：" + ex.Message); }
                }
            }

            Ui.SetActive(d.Popup, true);
            // 提到最上面：弹层可能被后建的区域（输出区、状态栏）盖住
            try { if (d.Popup != null) d.Popup.transform.SetAsLastSibling(); } catch { }
            // 每次打开从头看起，省得还停在上次滚到的位置
            if (d.Scroll != null) d.Scroll.ToTop();
        }

        private static void CloseDrop()
        {
            if (_dropOpen != null) Ui.SetActive(_dropOpen.Popup, false);
            _dropOpen = null;
        }

        private static void PickDrop(ItemDrop d, int idx)
        {
            if (d == null) return;
            d.Sel = Mathf.Clamp(idx, 0, Mathf.Max(0, d.Keys.Count - 1));
            PaintDropHead(d);
            RepaintDropRows(d);
            CloseDrop();

            if (d.OnPick != null)
            {
                try { d.OnPick(); }
                catch (Exception ex) { Core.Debug("[界面] 下拉框选择失败：" + ex.Message); }
            }
        }

        /// <summary>
        /// 点在下拉框外面就把它收起来。头按钮和选项各自管自己的点击，
        /// 这里只处理「点到别处去」，并且跳过刚点开的那一帧（否则会被自己立刻关掉）。
        /// </summary>
        private static void PollDropClose()
        {
            if (_dropOpen == null) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (Time.frameCount == _dropFrame) return;

            try
            {
                Vector2 m = Input.mousePosition;
                ItemDrop d = _dropOpen;
                RectTransform pop = Ui.Rect(d.Popup);
                if (pop != null && RectTransformUtility.RectangleContainsScreenPoint(pop, m, null)) return;
                if (d.Head != null && d.Head.Rect != null
                    && RectTransformUtility.RectangleContainsScreenPoint(d.Head.Rect, m, null)) return;
                CloseDrop();
            }
            catch { }
        }

        // ── 网格 ──────────────────────────────────────────────────────

        private static void BuildItemGrid(float x, float y, float w, float h)
        {
            _itemGridW = w;
            _itemGridH = h;
            _itemGrid = Ui.New("ItemGrid", _itemRoot.transform);
            if (_itemGrid != null) Ui.Place(_itemGrid, x, y, w, h);
        }

        /// <summary>按当前分类 / 筛选 / 搜索重铺这一页的格子。</summary>
        private static void RefreshItemGrid()
        {
            if (_itemGrid == null) return;

            Transform t = _itemGrid.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject go = t.GetChild(i).gameObject;
                Ui.SetActive(go, false);          // Destroy 帧末才生效，先藏掉免得叠一帧
                UnityEngine.Object.Destroy(go);
            }
            _itemCells.Clear();
            Ui.PruneHovers();                     // 旧格子的悬停记录一起丢掉

            List<string> all = ItemFilterList();
            int per = GridCols * GridRows;
            int pages = Mathf.Max(1, (all.Count + per - 1) / per);
            if (_itemPage >= pages) _itemPage = pages - 1;
            if (_itemPage < 0) _itemPage = 0;

            float gap = 8f;
            float cw = (_itemGridW - gap * (GridCols - 1)) / GridCols;
            float ch = (_itemGridH - gap * (GridRows - 1)) / GridRows;
            int start = _itemPage * per;

            for (int i = 0; i < per; i++)
            {
                int idx = start + i;
                if (idx >= all.Count) break;
                MakeItemCell(all[idx], (i % GridCols) * (cw + gap), (i / GridCols) * (ch + gap), cw, ch);
            }

            if (_itemPageText != null)
            {
                try
                {
                    _itemPageText.text = Palette.Rt(L10n.S(Palette.TagVal((_itemPage + 1) + " / " + pages)
                        + Palette.TagMuted("　共 " + all.Count + " 件")));
                }
                catch { }
            }
            Ui.SetActive(_itemPrev != null ? _itemPrev.Go : null, all.Count > per);
            Ui.SetActive(_itemNext != null ? _itemNext.Go : null, all.Count > per);
        }

        private static List<string> ItemFilterList()
        {
            List<string> list = new List<string>();
            List<GameApi.ItemRef> cat = GameApi.CatalogItems();

            for (int i = 0; i < cat.Count; i++)
            {
                GameApi.ItemRef r = cat[i];
                if (_itemDir.Length > 0 && r.Dir != _itemDir) continue;
                if (_itemKind == 1 && !_favs.Contains(r.Id)) continue;
                if (_itemKind == 2 && !GameApi.OwnsItem(r.Id)) continue;

                if (_itemQuery.Length > 0)
                {
                    string name = GameApi.ItemName(r.Id);
                    bool hit = (name != null && name.IndexOf(_itemQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        || r.Id.IndexOf(_itemQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hit) continue;
                }
                list.Add(r.Id);
            }
            return list;
        }

        private static void MakeItemCell(string id, float x, float y, float w, float h)
        {
            bool sel = id == _itemSelId;
            UiButton b = Ui.MakeButton(_itemGrid.transform, "Cell_" + id, "", 11f,
                sel ? Palette.NavOn : Palette.CardBg, Palette.Body, () => PickItem(id), Ui.AlignCenter);
            if (b == null) return;
            b.Place(x, y, w, h);

            float side = Mathf.Max(18f, Mathf.Min(w - 14f, h - 24f));
            Sprite sp = GameApi.ItemIcon(id);
            if (sp != null)
            {
                Image img = Ui.MakeSprite(b.Go.transform, "Icon", false);
                if (img != null)
                {
                    try { img.sprite = sp; img.color = Color.white; } catch { }
                    Ui.Place(img.gameObject, (w - side) * 0.5f, 3f, side, side);
                }
            }
            else
            {
                TextMeshProUGUI q = Ui.MakeText(b.Go.transform, "NoIcon", "?", 13f,
                    Palette.Muted, Ui.AlignCenter, false);
                if (q != null) Ui.Place(q.gameObject, 0f, (h - 24f) * 0.5f - 8f, w, 18f);
            }

            TextMeshProUGUI name = Ui.MakeText(b.Go.transform, "N",
                Ui.Cut(GameApi.ItemName(id), 10f, w - 8f), 10f, Palette.Sub, Ui.AlignCenter, false);
            if (name != null) Ui.Place(name.gameObject, 4f, h - 19f, w - 8f, 15f);

            bool fav = _favs.Contains(id);
            UiButton star = Ui.MakeButton(b.Go.transform, "Star", fav ? "★" : "☆", 12f,
                Palette.A(Palette.SlotBg, 0.9f), fav ? Palette.Value : Palette.Muted,
                () => ToggleFavItem(id), Ui.AlignCenter);
            if (star == null) return;
            star.Place(w - 22f, 3f, 19f, 19f);

            _itemCells.Add(new ItemCell { Id = id, Btn = b, Star = star });
        }

        /// <summary>选一格：只改底色的高亮，不重铺整页（重铺会把悬停和滚动都抖一下）。</summary>
        private static void PickItem(string id)
        {
            _itemSelId = id;
            for (int i = 0; i < _itemCells.Count; i++)
            {
                ItemCell c = _itemCells[i];
                if (c == null || c.Btn == null) continue;
                c.Btn.SetBg(c.Id == _itemSelId ? Palette.NavOn : Palette.CardBg);
            }
            RefreshItemSide();
        }

        private static void BuildItemPager(float x, float gridW, float y)
        {
            float cx = x + gridW * 0.5f;

            _itemPrev = Ui.MakeButton(_itemRoot.transform, "Prev", "◀", 13f,
                Palette.BtnIdle, Palette.Body, () => TurnItemPage(-1), Ui.AlignCenter);
            if (_itemPrev != null) _itemPrev.Place(cx - 118f, y, 30f, 28f);

            _itemPageText = Ui.MakeText(_itemRoot.transform, "Page", "", 13f,
                Palette.Body, Ui.AlignCenter, false);
            if (_itemPageText != null) Ui.Place(_itemPageText.gameObject, cx - 82f, y + 5f, 164f, 18f);

            _itemNext = Ui.MakeButton(_itemRoot.transform, "Next", "▶", 13f,
                Palette.BtnIdle, Palette.Body, () => TurnItemPage(1), Ui.AlignCenter);
            if (_itemNext != null) _itemNext.Place(cx + 88f, y, 30f, 28f);
        }

        private static void TurnItemPage(int delta)
        {
            _itemPage += delta;
            RefreshItemGrid();
        }

        // ── 右栏：数量 / 选中的货 / 位置 / 生成 ───────────────────────

        private static void BuildItemSide(float x, float y, float w, float h)
        {
            GameObject box = Ui.New("ItemSide", _itemRoot.transform);
            if (box == null) return;
            Ui.Place(box, x, y, w, h);

            Image bg = Ui.MakeSliced(box.transform, "Bg", Ui.Card(), Palette.CardBg, true);
            if (bg != null) Ui.Place(bg.gameObject, 0f, 0f, w, h);

            const float px = 12f;
            float iw = w - px * 2f;
            float cy = 12f;

            TextMeshProUGUI ql = Ui.MakeText(box.transform, "QL", "数量", 13f,
                Palette.Sub, Ui.AlignLeft, false);
            if (ql != null) Ui.Place(ql.gameObject, px, cy + 6f, 44f, 18f);

            _itemCount = Ui.MakeInput(box.transform, "Count", "1", 14f,
                Palette.SlotBg, Palette.Body, Palette.Muted, null);
            if (_itemCount != null)
            {
                Ui.Place(_itemCount.gameObject, px + 48f, cy, iw - 48f, ItemDropH);
                Ui.SetTextOf(_itemCount, "1");
                _cardInputs.Add(_itemCount);
            }
            cy += ItemDropH + 12f;

            // 选中的货：草图里这块是占位区，放名字 / ID / 估价最有用
            float prev = Mathf.Max(64f, Mathf.Min(96f, iw));
            Image slot = Ui.MakeSliced(box.transform, "PrevSlot", Ui.Chip(), Palette.SlotBg, false);
            if (slot != null) Ui.Place(slot.gameObject, (w - prev) * 0.5f, cy, prev, prev);

            _itemSelIcon = Ui.MakeSprite(box.transform, "PrevIcon", false);
            if (_itemSelIcon != null)
                Ui.Place(_itemSelIcon.gameObject, (w - prev) * 0.5f + 8f, cy + 8f, prev - 16f, prev - 16f);
            cy += prev + 8f;

            _itemSelName = Ui.MakeText(box.transform, "SelName", "", 14f,
                Palette.Title, Ui.AlignCenter, false);
            if (_itemSelName != null) Ui.Place(_itemSelName.gameObject, px, cy, iw, 20f);
            cy += 21f;

            _itemSelInfo = Ui.MakeText(box.transform, "SelInfo", "", 11f,
                Palette.Muted, Ui.AlignCenter, true);
            if (_itemSelInfo != null) Ui.Place(_itemSelInfo.gameObject, px, cy, iw, 34f);
            cy += 38f;

            TextMeshProUGUI ll = Ui.MakeText(box.transform, "LL", "生成的位置", 13f,
                Palette.Sub, Ui.AlignLeft, false);
            if (ll != null) Ui.Place(ll.gameObject, px, cy, iw, 18f);
            cy += 22f;

            _itemDesk = Ui.MakeButton(box.transform, "ToDesk", "", 13f,
                Palette.BtnIdle, Palette.Body, () => PickItemLoc(false), Ui.AlignLeft);
            if (_itemDesk != null) _itemDesk.Place(px, cy, iw, 28f);
            cy += 32f;

            _itemStore = Ui.MakeButton(box.transform, "ToStore", "", 13f,
                Palette.BtnIdle, Palette.Body, () => PickItemLoc(true), Ui.AlignLeft);
            if (_itemStore != null) _itemStore.Place(px, cy, iw, 28f);
            cy += 38f;

            UiButton gen = Ui.MakeButton(box.transform, "Gen", "生成", 15f,
                Palette.BtnRun, Palette.Title, SpawnGo, Ui.AlignCenter);
            if (gen != null) gen.Place(px, cy, iw, 36f);

            PaintItemLoc();
        }

        private static void PickItemLoc(bool toStore)
        {
            _itemToStore = toStore;
            PaintItemLoc();
        }

        /// <summary>两个位置二选一，勾中的那个用主色，一眼看出货会落到哪。</summary>
        private static void PaintItemLoc()
        {
            if (_itemDesk != null)
            {
                _itemDesk.SetText((_itemToStore ? "□ " : "■ ") + "桌子（柜台）");
                _itemDesk.SetBg(_itemToStore ? Palette.BtnIdle : Palette.NavOn);
                _itemDesk.SetColor(_itemToStore ? Palette.Body : Palette.Title);
            }
            if (_itemStore != null)
            {
                _itemStore.SetText((_itemToStore ? "■ " : "□ ") + "仓库（后间）");
                _itemStore.SetBg(_itemToStore ? Palette.NavOn : Palette.BtnIdle);
                _itemStore.SetColor(_itemToStore ? Palette.Title : Palette.Body);
            }
        }

        /// <summary>把选中的货、数量、位置攒成一条 spawn 命令，跟手敲走同一条路。</summary>
        private static void SpawnGo()
        {
            if (string.IsNullOrEmpty(_itemSelId))
            {
                Out.Warn("先在网格里点一件货。");
                return;
            }

            int count = 1;
            string cs = (Ui.TextOf(_itemCount, "") ?? "").Trim();
            if (cs.Length > 0) int.TryParse(cs, out count);
            if (count < 1) count = 1;

            RunCard("spawn " + _itemSelId + " " + count + " " + (_itemToStore ? "store" : "desk"));
        }

        private static void ClearAllGo()
        {
            if (_clearAllDialog != null || _itemRoot == null) return;

            RectTransform rootRect = Ui.Rect(_itemRoot);
            if (rootRect == null) return;
            float rootW = rootRect.rect.width;
            float rootH = rootRect.rect.height;
            if (rootW <= 0f || rootH <= 0f) return;

            _clearAllDialog = Ui.New("ClearAllDialog", _itemRoot.transform);
            if (_clearAllDialog == null) return;
            Ui.Place(_clearAllDialog, 0f, 0f, rootW, rootH);

            Image dim = Ui.MakeImage(_clearAllDialog.transform, "Dim", new Color(0f, 0f, 0f, 0.68f), true);
            if (dim != null) Ui.Place(dim.gameObject, 0f, 0f, rootW, rootH);

            float dialogW = Mathf.Min(460f, rootW - 24f);
            const float dialogH = 190f;
            float dx = (rootW - dialogW) * 0.5f;
            float dy = (rootH - dialogH) * 0.5f;
            Image card = Ui.MakeSliced(_clearAllDialog.transform, "Card", Ui.Card(), Palette.CardBg, true);
            if (card != null) Ui.Place(card.gameObject, dx, dy, dialogW, dialogH);

            TextMeshProUGUI title = Ui.MakeText(_clearAllDialog.transform, "Title",
                "确认清空所有物品？", 18f, Palette.Title, Ui.AlignCenter, false);
            if (title != null) Ui.Place(title.gameObject, dx + 20f, dy + 20f, dialogW - 40f, 28f);

            TextMeshProUGUI warning = Ui.MakeText(_clearAllDialog.transform, "Warning",
                "即将移除柜台、后仓和前场的全部物品。本操作无法撤销，请先备份存档。", 14f, Palette.Err, Ui.AlignCenter, true);
            if (warning != null) Ui.Place(warning.gameObject, dx + 24f, dy + 60f, dialogW - 48f, 56f);

            float buttonW = Mathf.Min(142f, (dialogW - 44f) * 0.5f);
            const float buttonGap = 12f;
            float buttonX = dx + (dialogW - buttonW * 2f - buttonGap) * 0.5f;
            float buttonY = dy + dialogH - 48f;
            UiButton cancel = Ui.MakeButton(_clearAllDialog.transform, "Cancel", "取消", 14f,
                Palette.BtnIdle, Palette.Body, CloseClearAllDialog, Ui.AlignCenter);
            if (cancel != null) cancel.Place(buttonX, buttonY, buttonW, 34f);

            UiButton confirm = Ui.MakeButton(_clearAllDialog.transform, "Confirm", "确认清空", 14f,
                Palette.BtnDanger, Palette.Title, ConfirmClearAll, Ui.AlignCenter);
            if (confirm != null) confirm.Place(buttonX + buttonW + buttonGap, buttonY, buttonW, 34f);

            try { _clearAllDialog.transform.SetAsLastSibling(); } catch { }
        }

        private static void CloseClearAllDialog()
        {
            if (_clearAllDialog != null) UnityEngine.Object.Destroy(_clearAllDialog);
            _clearAllDialog = null;
        }

        private static void ConfirmClearAll()
        {
            CloseClearAllDialog();
            int n;
            string err = GameApi.ClearAllItems(out n);
            if (err != null) { Out.Warn("清空物品：" + err + "（已销毁 " + n + " 件）"); return; }
            if (n == 0) { Out.Line("店里本来就是空的。"); return; }
            Out.Ok("已清空 " + Palette.TagVal(n + " 件") + " 物品。");
        }

        private static void RefreshItemSide()
        {
            bool has = !string.IsNullOrEmpty(_itemSelId);
            Sprite sp = has ? GameApi.ItemIcon(_itemSelId) : null;

            if (_itemSelIcon != null)
            {
                try { _itemSelIcon.sprite = sp; _itemSelIcon.color = Color.white; } catch { }
                Ui.SetActive(_itemSelIcon.gameObject, sp != null);
            }
            if (_itemSelName != null)
            {
                try
                {
                    _itemSelName.text = Palette.Rt(L10n.S(has
                        ? Palette.TagVal(Ui.Cut(GameApi.ItemName(_itemSelId), 14f, 200f))
                        : Palette.TagMuted("还没选物品")));
                }
                catch { }
            }
            if (_itemSelInfo != null)
            {
                try
                {
                    _itemSelInfo.text = Palette.Rt(L10n.S(has
                        ? Palette.TagMuted("ID " + Ui.Cut(_itemSelId, 11f, 200f)) + "\n"
                          + Palette.TagMuted("估价 ") + Palette.TagVal(Fmt.Money(GameApi.ItemPrice(_itemSelId)))
                        : Palette.TagMuted("点左边一件货，填好数量、选好位置，再点生成")));
                }
                catch { }
            }
        }

        // ── 收藏（写在 配置文件里，重启还在）─────────────────

        private static void LoadFavs()
        {
            if (_favLoaded) return;
            _favLoaded = true;

            string raw = Core.FavoritesPref;
            if (string.IsNullOrEmpty(raw)) return;

            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string s = parts[i].Trim();
                if (s.Length > 0) _favs.Add(s);
            }
        }

        private static void SaveFavs()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in _favs)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(s);
            }
            Core.FavoritesPref = sb.ToString();
        }

        private static void ToggleFavItem(string id)
        {
            bool on = !_favs.Contains(id);
            if (on) _favs.Add(id);
            else _favs.Remove(id);
            SaveFavs();

            // 只改这一格的星，不重铺整页
            for (int i = 0; i < _itemCells.Count; i++)
            {
                ItemCell c = _itemCells[i];
                if (c == null || c.Id != id || c.Star == null) continue;
                c.Star.SetText(on ? "★" : "☆");
                c.Star.SetColor(on ? Palette.Value : Palette.Muted);
            }
            if (_itemKind == 1) RefreshItemGrid();   // 只看收藏时，取消收藏就该从页面上消失
            Out.Line((on ? "已收藏 " : "已取消收藏 ") + Palette.TagKey(GameApi.ItemName(id)));
        }

        // ── 每帧轮询 ──────────────────────────────────────────────────

        /// <summary>每帧调一次：收下拉框 + 盯着搜索框（输入框没有 change 事件，只能轮询）。</summary>
        private static void PollItemConsole()
        {
            if (!_itemView || _itemRoot == null) return;

            PollDropClose();

            if (Time.unscaledTime - _itemPollAt < 0.25f) return;
            _itemPollAt = Time.unscaledTime;

            string q = (Ui.TextOf(_itemSearch, "") ?? "").Trim();
            if (q == _itemLastQuery) return;

            _itemLastQuery = q;
            _itemQuery = q;
            _itemPage = 0;
            RefreshItemGrid();
        }

        /// <summary>简单模式底部那条反馈：只显示最近一条消息，点完按钮至少看得见结果。</summary>
        private static void BuildFeed(float x, float y, float w, float h)
        {
            GameObject go = Ui.New("Feed", _panel.transform);
            if (go == null) return;
            Ui.Place(go, x, y, w, h);

            Image bg = Ui.MakeSliced(go.transform, "Bg", Ui.Chip(), Palette.SlotBg, false);
            if (bg != null) Ui.Place(bg.gameObject, 0f, 0f, w, h);

            _feed = Ui.MakeText(go.transform, "T", "", 13f, Palette.Body, Ui.AlignLeft, false);
            if (_feed != null) Ui.Place(_feed.gameObject, 14f, (h - 18f) * 0.5f, w - 28f, 18f);
        }

        /// <summary>
        /// 刷卡片上的实时读数。0.3 秒一轮 —— 每帧去读游戏字段太浪费，
        /// 而且写 TMP 会触发网格重排，值没变就一个字都别写。
        /// </summary>
        private static void RefreshCards()
        {
            if (_cardViews.Count == 0) return;
            if (Time.unscaledTime - _readAt < 0.3f) return;
            _readAt = Time.unscaledTime;

            for (int i = 0; i < _cardViews.Count; i++)
            {
                CardView v = _cardViews[i];
                if (v == null || v.Text == null) continue;

                string s;
                try { s = v.Src != null ? v.Src() : null; } catch { s = null; }
                if (string.IsNullOrEmpty(s)) s = "—";
                if (v.MaxW > 0f) s = Ui.Cut(s, 13f, v.MaxW);
                if (s == v.Last) continue;

                v.Last = s;
                try { v.Text.text = Palette.Rt(L10n.S(s)); } catch { }
            }
        }

        /// <summary>底栏输入行。只有混合 / 开发者模式才有。</summary>
        private static void BuildInputRow(float y)
        {
            float x = SideW + Gap * 2;
            float btnW = 96f;
            float w = _w - x - Gap - btnW;

            _input = Ui.MakeInput(_panel.transform, "Input", "输入命令，回车执行…　（Tab 补全 / ↑↓ 历史）",
                15f, Palette.SlotBg, Palette.Body, Palette.Muted, Submit);
            if (_input != null) Ui.Place(_input.gameObject, x, y, w, InputH);

            _btnRun = Ui.MakeButton(_panel.transform, "Run", "执行", 15f,
                Palette.BtnRun, Palette.Title, () => Submit(Ui.TextOf(_input, "")), Ui.AlignCenter);
            if (_btnRun != null) _btnRun.Place(x + w + Gap, y, btnW, InputH);
        }

        private static void BuildStatus()
        {
            float y = _h - StatusH;
            bool simple = _mode == ConsoleMode.Simple;
            float x = simple ? 14f : SideW + Gap * 2;

            _statusLeft = Ui.MakeText(_panel.transform, "StL", "", 12f, Palette.Muted, Ui.AlignLeft, false);
            if (_statusLeft != null) Ui.Place(_statusLeft.gameObject, x, y + 6f, 340f, 16f);

            _statusMid = Ui.MakeText(_panel.transform, "StM", "", 12f, Palette.Muted, Ui.AlignLeft, false);
            if (_statusMid != null) Ui.Place(_statusMid.gameObject, x + 340f, y + 6f, 320f, 16f);

            _statusRight = Ui.MakeText(_panel.transform, "StR",
                (simple ? Core.HotkeyText + " 关闭　滚轮翻方块" : Core.HotkeyText + " 关闭　Esc 收起输入"),
                12f, Palette.Muted, Ui.AlignRight, false);
            if (_statusRight != null) Ui.Place(_statusRight.gameObject, _w - 386f - 2f, y + 6f, 370f, 16f);
        }

        // ══════════════════════════════════════════════════════════════
        //  渲染
        // ══════════════════════════════════════════════════════════════

        /// <summary>把可见的那几行画出来。行高固定，所以只要算出起始行就够了。</summary>
        private static void Render(bool force)
        {
            if (_logScroll == null || _rows.Count == 0) return;

            float contentH = _lines.Count * LogRowH;
            if (Mathf.Abs(_logScroll.ContentH - contentH) > 0.5f)
            {
                _logScroll.SetContentHeight(contentH);
                _logScroll.ContentGrew();
                force = true;
            }

            int first = (int)(_logScroll.Offset / LogRowH);
            if (first < 0) first = 0;
            if (first > _lines.Count) first = _lines.Count;
            if (!force && first == _logFirst && _lines.Count == _logCount) return;
            _logFirst = first;
            _logCount = _lines.Count;

            for (int i = 0; i < _rows.Count; i++)
            {
                TextMeshProUGUI t = _rows[i];
                if (t == null) continue;
                int idx = first + i;
                if (idx >= _lines.Count)
                {
                    Ui.SetActive(t.gameObject, false);
                    continue;
                }

                LogLine ln = _lines[idx];
                Ui.SetActive(t.gameObject, true);
                try
                {
                    t.text = Palette.Rt(Ui.Cut(L10n.S(ln.Text), LogFont, _logScroll.ViewW - 8f));
                    t.color = ln.Tone;
                }
                catch { }
                Ui.Place(t.gameObject, 2f, idx * LogRowH, _logScroll.ViewW - 6f, LogRowH);
            }
        }

        private static void SyncStatus()
        {
            if (_statusLeft == null) return;
            try
            {
                string state;
                if (GameApi.Store == null) state = Palette.TagMuted("主菜单 · 未进存档");
                else if (GameApi.WorldReady) state = Palette.TagOk("存档 · 店铺就绪");
                else state = Palette.TagErr("存档 · 库存未就绪");

                int items = GameApi.AllItemIds().Count;
                string left = L10n.S(state + Palette.TagMuted("　物品目录 " + items + " 件"));
                if (_statusLeft.text != left) _statusLeft.text = Palette.Rt(left);

                CmdGroup g = Cmds.GroupOf(_group);
                string mid = L10n.S(_group == "all" ? "全部命令" : (g != null ? g.Name + "　" + g.Hint : ""));
                if (_statusMid != null && _statusMid.text != mid) _statusMid.text = Palette.Rt(mid);
            }
            catch { }
        }
    }
}
