using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DebugConsole
{
    /// <summary>
    /// 游戏 API 的安全包装。所有调用都吞异常、先判单例存在，
    /// 保证任何一条命令失败都只是打一行红字，不会把整个面板带崩。
    /// </summary>
    internal static class GameApi
    {
        private static T ReadPrivate<T>(object target, string field)
        {
            if (target == null) return default(T);
            try
            {
                System.Reflection.FieldInfo info = AccessTools.Field(target.GetType(), field);
                return info == null ? default(T) : (T)info.GetValue(target);
            }
            catch { return default(T); }
        }

        private static void WritePrivate<T>(object target, string field, T value)
        {
            if (target == null) return;
            try
            {
                System.Reflection.FieldInfo info = AccessTools.Field(target.GetType(), field);
                if (info != null) info.SetValue(target, value);
            }
            catch { }
        }

        private static IntPtr Identity(object target)
        {
            return target == null ? IntPtr.Zero : new IntPtr(RuntimeHelpers.GetHashCode(target));
        }

        // ── 就绪判断 ──────────────────────────────────────────────────

        public static PlayerStore Store
        {
            get
            {
                try { return PlayerStore.Instance; }
                catch { return null; }
            }
        }

        public static EmporiumEntry Emporium
        {
            get
            {
                try { return EmporiumEntry.Instance; }
                catch { return null; }
            }
        }

        /// <summary>世界是否可交互（在存档里且店铺已建好）。</summary>
        public static bool WorldReady
        {
            get
            {
                try
                {
                    PlayerStore s = Store;
                    if (s == null) return false;
                    EmporiumEntry e = Emporium;
                    return e != null && e.invElement != null;
                }
                catch { return false; }
            }
        }

        /// <summary>在存档里但店铺还没建好（进档瞬间、过场中）。</summary>
        public static bool InSaveButNotReady
        {
            get { return Store != null && !WorldReady; }
        }

        // ── 金钱 ──────────────────────────────────────────────────────

        public static bool TryGetCash(out int cash)
        {
            cash = 0;
            PlayerStore s = Store;
            if (s == null) return false;
            try
            {
                cash = s.playerCash;
                return true;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 读金钱失败：" + ex.Message);
                return false;
            }
        }

        public static bool TrySetCash(int value)
        {
            PlayerStore s = Store;
            if (s == null) return false;
            if (value < 0) value = 0;
            try
            {
                s.playerCash = value;
                return true;
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[API] 改金钱失败：" + ex.Message);
                return false;
            }
        }

        public static int Cash()
        {
            int v;
            return TryGetCash(out v) ? v : 0;
        }

        // ── 租金 / 日期 ───────────────────────────────────────────────

        public static bool TryGetRent(out int rent, out int days, out int starting)
        {
            rent = 0; days = 0; starting = 0;
            PlayerStore s = Store;
            if (s == null) return false;
            try
            {
                rent = s.rentValue;
                days = s.dayUntilRent;
                starting = s.startingRent;
                return true;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 读租金失败：" + ex.Message);
                return false;
            }
        }

        public static bool TrySetRent(int rent)
        {
            PlayerStore s = Store;
            if (s == null) return false;
            if (rent < 0) rent = 0;
            try { s.rentValue = rent; return true; }
            catch (Exception ex) { Core.Log.Warning("[API] 改租金失败：" + ex.Message); return false; }
        }

        public static bool TrySetRentDays(int days)
        {
            PlayerStore s = Store;
            if (s == null) return false;
            if (days < 0) days = 0;
            try { s.dayUntilRent = days; return true; }
            catch (Exception ex) { Core.Log.Warning("[API] 改缴租天数失败：" + ex.Message); return false; }
        }

        // ── 简单开关字段 ──────────────────────────────────────────────

        public static bool TryGetFlag(string which, out bool value)
        {
            value = false;
            PlayerStore s = Store;
            if (s == null) return false;
            try
            {
                if (which == "power") value = s.isPowerOn;
                else if (which == "hard") value = s.isHardMode;
                else if (which == "aug") value = s.isAugIntroduced;
                else if (which == "augend") value = s.isAugEnded;
                else return false;
                return true;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 读开关失败：" + ex.Message);
                return false;
            }
        }

        public static bool TrySetFlag(string which, bool value)
        {
            PlayerStore s = Store;
            if (s == null) return false;
            try
            {
                if (which == "power") s.isPowerOn = value;
                else if (which == "hard") s.isHardMode = value;
                else if (which == "aug") s.isAugIntroduced = value;
                else if (which == "augend") s.isAugEnded = value;
                else return false;
                return true;
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[API] 改开关失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>写一条夜间报告。颜色参数游戏只认十六进制字符串。</summary>
        public static bool TryNightLog(string text, string hex)
        {
            PlayerStore s = Store;
            if (s == null || string.IsNullOrEmpty(text)) return false;
            try
            {
                if (string.IsNullOrEmpty(hex)) s.AddNightLog(text);
                else s.AddNightLog(text, hex);
                return true;
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[API] 写夜间日志失败：" + ex.Message);
                return false;
            }
        }

        public static bool TrySave()
        {
            PlayerStore s = Store;
            if (s == null) return false;
            try { s.SaveGame(); return true; }
            catch (Exception ex) { Core.Log.Warning("[API] 存档失败：" + ex.Message); return false; }
        }

        // ── 日期推进 ──────────────────────────────────────────────────

        /// <summary>
        /// 结束今天（等于把店门关了、进入结算、翻到第二天）。
        /// 一天一天来而不是一次连跳：中间夹着夜间结算和开店面板，
        /// 一帧内连着调十次，游戏自己的状态机来不及走。
        /// </summary>
        public static bool TryEndDay(out string error)
        {
            error = null;
            PlayerStore s = Store;
            if (s == null) { error = "还没进存档。"; return false; }
            try
            {
                // CanEndDay() 是游戏自己的「现在能不能打烊」检查：手上还有活、还没到打烊时间，
                // 它一律返回 false —— 之前跳天不生效就是卡在这儿。调试台不跟它客气，直接推 EndDay()，
                // 推不动（抛异常）再如实回报。
                bool allowed = true;
                try { allowed = s.CanEndDay(); } catch { }
                if (!allowed) Core.Debug("[API] CanEndDay() 说不行，仍然强推 EndDay()。");
                s.EndDay();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ── 连跳天数 ──────────────────────────────────────────────────

        private static int _skipLeft;
        private static int _skipDone;
        private static int _skipStage = -1;    // 这一天试到第几种入口（-1 = 还没开始）
        private static int _skipDay = -1;      // 动手之前是第几天
        private static bool _skipCalled;       // 这一式已经喊过了（喊过就只等结果，别重复推）
        private static bool _skipBlind;        // 读不到天数时的降级：推一把、等一等，推了就算跳过
        private static float _skipWait;        // 下一帧才能动的时间
        private static float _skipDeadline;    // 这一式等多久没动静就换下一种
        private static float _skipClickAt;     // 面板按钮点一下有动画，隔一会儿才点下一次

        /// <summary>还在连跳中时剩几天（0 表示没在跳）。卡片读数用。</summary>
        public static int SkipLeft { get { return _skipLeft; } }

        /// <summary>今天是第几天。读不到返回 -1。跳天有没有真的生效，全看这个数变不变。</summary>
        public static int DayNumber()
        {
            try
            {
                StoreStation st = StoreStation.Instance;
                if (st != null) return StoreStation.GetDayCounter();
            }
            catch (Exception ex) { Core.Debug("[API] 读天数失败：" + ex.Message); }
            try
            {
                GameMaster gm = GameMaster.current;
                if (gm != null && gm.turnManager != null) return gm.turnManager.GetDays();
            }
            catch { }
            return -1;
        }

        /// <summary>存档状态机眼下停在哪一步。跳不动的时候报给用户，好对症状。</summary>
        private static string StoreStateName()
        {
            try
            {
                PlayerStore s = Store;
                return s == null ? "没进存档" : s.storeState.ToString();
            }
            catch { return "?"; }
        }

        /// <summary>
        /// 推一天。stage 越大越直接：
        ///   0 走游戏自己的打烊流程（EndDay）
        ///   1 直接翻夜 + 开新的一天
        ///   2 先开店再打烊（早上还没开门的时候 EndDay 是推不动的）
        ///   3 三个一起上
        /// 每种入口只喊一次，喊完就看天数有没有动 —— 一帧里连着推会把它的状态机踩乱。
        /// </summary>
        private static bool PushDay(int stage, out string error)
        {
            error = null;
            PlayerStore s = Store;
            if (s == null) { error = "还没进存档。"; return false; }
            try
            {
                switch (stage)
                {
                    case 0:
                        s.EndDay();
                        break;
                    case 1:
                        s.EndNight();
                        s.BeginDay();
                        break;
                    case 2:
                        s.OpenShutter();
                        s.EndDay();
                        break;
                    default:
                        s.OpenShutter();
                        s.EndDay();
                        s.EndNight();
                        s.BeginDay();
                        break;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 排队连跳 N 天。真正推进放在 TickSkip 里一天一天做：
        /// 每跳一天游戏都要走一遍打烊结算 → 清晨面板，一帧内连着调 N 次会把它的状态机踩乱。
        /// </summary>
        public static bool StartSkip(int days, out string error)
        {
            error = null;
            if (days < 1) { error = "天数至少是 1。"; return false; }
            if (days > 100) { days = 100; }
            if (Store == null) { error = "还没进存档，没法跳。"; return false; }
            if (_skipLeft > 0) { error = "上一次连跳还没走完（还剩 " + _skipLeft + " 天）。"; return false; }

            int day = DayNumber();
            _skipBlind = day < 0;
            if (_skipBlind)
            {
                // 读不到天数就当不出错：退化成「推一把、等一等」，只是没法核对推没推动
                Core.Log.Warning("[API] 读不到天数，跳天改用盲推模式。");
                Out.Warn("读不到当前是第几天，改成「推一把就算跳过」的方式，准头差一点。");
            }

            _skipLeft = days;
            _skipDone = 0;
            _skipStage = -1;
            _skipCalled = false;
            _skipWait = Time.unscaledTime + 0.1f;
            return true;
        }

        public static bool CancelSkip()
        {
            if (_skipLeft <= 0) return false;
            _skipLeft = 0;
            _skipStage = -1;
            return true;
        }

        /// <summary>由 Core.OnUpdate 每帧驱动。</summary>
        public static void TickSkip()
        {
            if (_skipLeft <= 0) return;
            try
            {
                if (Store == null) { FinishSkip("存档退出了，连跳停下。"); return; }
                if (Time.unscaledTime < _skipWait) return;

                // 打烊结算 / 清晨这两张面板挡着的时候什么都推不动，顺手替玩家按掉；
                // 按钮点下去有动画，隔一会儿才点第二次，免得重复触发
                if (Time.unscaledTime >= _skipClickAt)
                {
                    bool clicked = TryCloseEndOfDayPanel();
                    if (!clicked) clicked = TryCloseMorningPanel();
                    if (clicked) _skipClickAt = Time.unscaledTime + 1.3f;
                }

                // 新的一天：从第一种入口重新试（盲推模式看不出来推没推动，直接用最猛那式）
                if (_skipStage < 0)
                {
                    _skipStage = _skipBlind ? 3 : 0;
                    _skipCalled = false;
                    _skipDay = DayNumber();
                    _skipDeadline = Time.unscaledTime + 6f;
                }

                if (!_skipCalled)
                {
                    _skipCalled = true;
                    string err;
                    if (!PushDay(_skipStage, out err))
                    {
                        Core.Debug("[API] 跳天第 " + _skipStage + " 种入口报错：" + err);
                        _skipStage++;
                        if (_skipStage > 3)
                        {
                            FinishSkip("跳不动了（" + err + "）。现在停在第 " + _skipDay + " 天，状态："
                                + StoreStateName() + "。");
                            return;
                        }
                        _skipCalled = false;
                        _skipWait = Time.unscaledTime + 0.3f;
                    }
                    return;      // 喊完先让游戏自己走，下一帧再来看结果
                }

                int now = DayNumber();
                if (!_skipBlind && _skipDay >= 0 && now == _skipDay)
                {
                    // 天数没动：还有时间就再等等，超时就换下一种入口
                    if (Time.unscaledTime < _skipDeadline)
                    {
                        _skipWait = Time.unscaledTime + 0.25f;
                        return;
                    }
                    Core.Debug("[API] 第 " + _skipStage + " 种入口没推动天数（还是第 " + now + " 天，状态 "
                        + StoreStateName() + "），换下一种。");
                    _skipStage++;
                    _skipCalled = false;
                    if (_skipStage > 3)
                    {
                        FinishSkip("三种入口都没推动天数。现在停在第 " + _skipDay + " 天，状态："
                            + StoreStateName() + "。");
                        return;
                    }
                    _skipWait = Time.unscaledTime + 0.2f;
                    return;
                }

                // 天数过去了（盲推模式没法核对，按推过算）
                _skipDone++;
                _skipLeft--;
                _skipStage = -1;
                _skipCalled = false;
                _skipWait = Time.unscaledTime + (_skipBlind ? 2.2f : 0.7f);
                if (_skipLeft <= 0) FinishSkip(null);
                else Out.Line(Palette.TagMuted("…跳过 " + _skipDone + " 天")
                    + (now > 0 ? Palette.TagMuted("，今天是第 " + now + " 天") : ""));
            }
            catch (Exception ex)
            {
                FinishSkip("连跳出错：" + ex.Message);
            }
        }

        private static void FinishSkip(string problem)
        {
            int done = _skipDone;
            _skipLeft = 0;
            _skipStage = -1;
            _skipBlind = false;
            if (problem != null) Out.Warn(problem);
            if (done <= 0) return;
            int day = DayNumber();
            Out.Ok("已跳过 " + Palette.TagVal(done + " 天")
                + (day > 0 ? "，现在是第 " + Palette.TagVal(day + " 天") + "。" : "。"));
        }

        /// <summary>
        /// 打烊结算面板开着就替玩家按一下「结束今天」—— 这一步不按，天数根本不会往下走。
        /// 返回是否真的按了。
        /// </summary>
        private static bool TryCloseEndOfDayPanel()
        {
            try
            {
                EndOfDayUIManager ui = EndOfDayUIManager.Instance;
                if (ui == null) return false;
                GameObject panel = null;
                try { panel = ui.UI; } catch { }
                if (panel == null || !panel.activeSelf) return false;
                ui.OnEndDayButtonClick();
                return true;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 关打烊面板失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>清晨面板开着就替玩家按一下「开始营业」。返回是否真的按了。</summary>
        private static bool TryCloseMorningPanel()
        {
            try
            {
                StartOfDayUIManager ui = StartOfDayUIManager.Instance;
                if (ui == null) return false;
                GameObject panel = null;
                try { panel = ui.UIPanel; } catch { }
                if (panel == null || !panel.activeSelf) return false;
                ui.OnStartDayButtonClicked();
                return true;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 关清晨面板失败：" + ex.Message);
                return false;
            }
        }

        // ── 界面导航 / 输入屏蔽 ───────────────────────────────────────

        private static bool _navOff;
        private static bool _inputOff;

        // 关掉过谁就记着谁：关面板时只把这几个原样放开。
        // 早先的写法是「按当前列表把所有人 enabled 一律置回 true」，
        // 一旦期间列表换了人（处理器重新挂载 / 管理器换了实例），当初关掉的那个就再也没人放开，
        // 于是鼠标悬停、点击整套失灵 —— 表现出来就是「有时候能删有时候不能」。
        private static InputActionManager _blockedMgr;
        private static readonly List<InputActionHandler> _blockedHandlers = new List<InputActionHandler>();

        /// <summary>
        /// 把游戏自己的输入管理器整个关掉。
        ///
        /// 只掐 InputActionManager.Update（Harmony 那条）在实际游戏里还是会被按键蹭到 ——
        /// 有的处理器自己也有 Update。这里更彻底：把管理器和它手上那批处理器 enabled 置 false，
        /// Unity 连它们的 Update / 事件都不会调。关面板时按记录原样还回去。
        /// </summary>
        public static void BlockGameInput(bool on)
        {
            if (on == _inputOff) return;
            _inputOff = on;
            try
            {
                if (on) BlockOn();
                else BlockOff();
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 开关游戏输入失败：" + ex.Message);
            }
        }

        private static void BlockOn()
        {
            _blockedMgr = null;
            _blockedHandlers.Clear();

            InputActionManager m = InputActionManager.current;
            if (m == null) return;

            try { m.enabled = false; _blockedMgr = m; } catch { }

            List<InputActionHandler> hs = null;
            try { hs = m.actionHandlers; } catch { }
            if (hs == null) return;

            for (int i = 0; i < hs.Count; i++)
            {
                InputActionHandler h = null;
                try { h = hs[i]; } catch { }
                if (h == null) continue;

                // 本来就没开的（游戏自己在某模式下停掉的）别动，免得回头我们替游戏把它打开了
                bool was = false;
                try { was = h.enabled; } catch { }
                if (!was) continue;
                try { h.enabled = false; } catch { continue; }

                _blockedHandlers.Add(h);
            }
        }

        private static void BlockOff()
        {
            for (int i = 0; i < _blockedHandlers.Count; i++)
            {
                try
                {
                    InputActionHandler h = _blockedHandlers[i];
                    if (h != null) h.enabled = true;
                }
                catch { }
            }
            _blockedHandlers.Clear();

            if (_blockedMgr != null)
            {
                try { _blockedMgr.enabled = true; } catch { }
                _blockedMgr = null;
                return;
            }

            // 当初没记下管理器（取的时候还是 null）就退回按当前实例放开
            try
            {
                InputActionManager m = InputActionManager.current;
                if (m != null) m.enabled = true;
            }
            catch { }
        }

        /// <summary>
        /// 开关 EventSystem 的键盘导航。面板压着游戏界面时，方向键 / 回车如果还能导航，
        /// 就会去点亮后面那些游戏按钮；只关导航不关整个 EventSystem，鼠标照样点我们的按钮。
        /// </summary>
        public static void BlockUiNav(bool on)
        {
            try
            {
                if (on == _navOff) return;
                EventSystem es = EventSystem.current;
                if (es == null) { _navOff = false; return; }
                es.sendNavigationEvents = !on;
                _navOff = on;
            }
            catch (Exception ex)
            {
                _navOff = false;
                Core.Debug("[API] 切换界面导航失败：" + ex.Message);
            }
        }

        public static bool TryRefreshCounter()
        {
            PlayerStore s = Store;
            if (s == null) return false;
            try { s.RefreshCounterItem(); return true; }
            catch (Exception ex) { Core.Debug("[API] 刷新柜台失败：" + ex.Message); return false; }
        }

        // ── 声望（每个区一对静态接口，直接对着游戏读写）───────────────
        //
        // 不要走 GetStoreReputation("ul") 这条字符串 ID 的路：实机上它取不到东西，
        // 五区读数一直是「—」。游戏给每个区都开了专门的 Get/Mod 静态方法，一区一对，
        // 用这个既短又稳。

        /// <summary>一个区：ID、中文名、别名、怎么读、怎么写。</summary>
        internal sealed class RepFaction
        {
            public string Id;
            public string Name;
            public string[] Aliases;   // 别的写法：面板上写「治安部」，手边习惯写「治安区」
            public Func<int> Get;
            public Action<int> Mod;

            public RepFaction(string id, string name, string[] aliases, Func<int> get, Action<int> mod)
            {
                Id = id; Name = name; Aliases = aliases; Get = get; Mod = mod;
            }
        }

        // 名单和顺序照游戏声望面板来：下层区 / 上层区 / 治安部 / 黑市 / 革命军。
        // 卡特尔（StoreReputation 里那个 Cartel）面板上根本不列，这里也不放进来。
        private static readonly List<RepFaction> _repList = new List<RepFaction>
        {
            new RepFaction("ll",  "下层区", new[] { "下层" },
                () => StoreReputation.GetLLReputation(),  v => StoreReputation.ModLLReputation(v)),
            new RepFaction("ul",  "上层区", new[] { "上层" },
                () => StoreReputation.GetULReputation(),  v => StoreReputation.ModULReputation(v)),
            new RepFaction("sec", "治安部", new[] { "治安", "治安区", "治安部区" },
                () => StoreReputation.GetSecReputation(), v => StoreReputation.ModSecReputation(v)),
            new RepFaction("bm",  "黑市",   null,
                () => StoreReputation.GetBMReputation(),  v => StoreReputation.ModBMReputation(v)),
            new RepFaction("rev", "革命军", new[] { "革命", "反抗军", "反抗" },
                () => StoreReputation.GetRevReputation(), v => StoreReputation.ModRevReputation(v)),
        };

        /// <summary>能改声望的区清单（ID + 中文名）。</summary>
        public static List<KeyValuePair<string, string>> RepFactions()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < _repList.Count; i++)
            {
                list.Add(new KeyValuePair<string, string>(_repList[i].Id, _repList[i].Name));
            }
            return list;
        }

        /// <summary>按 ID、中文名或别名找一个区；找不到返回 null。</summary>
        public static RepFaction FindRep(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string t = key.Trim();
            for (int i = 0; i < _repList.Count; i++)
            {
                if (string.Equals(_repList[i].Id, t, StringComparison.OrdinalIgnoreCase)) return _repList[i];
            }
            for (int i = 0; i < _repList.Count; i++)
            {
                if (_repList[i].Name == t) return _repList[i];
            }
            for (int i = 0; i < _repList.Count; i++)
            {
                string[] al = _repList[i].Aliases;
                if (al == null) continue;
                for (int j = 0; j < al.Length; j++)
                {
                    if (al[j] == t) return _repList[i];
                }
            }
            for (int i = 0; i < _repList.Count; i++)
            {
                if (_repList[i].Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return _repList[i];
            }
            return null;
        }

        /// <summary>某个区当前的声望（取不到返回 false）。</summary>
        public static bool TryGetRep(string id, out int rep)
        {
            rep = 0;
            RepFaction f = FindRep(id);
            if (f == null) return false;
            try { rep = f.Get(); return true; }
            catch (Exception ex)
            {
                Core.Debug("[API] 读声望失败（" + f.Id + "）：" + ex.Message);
                return false;
            }
        }

        /// <summary>给某个区单独加/减声望。走游戏自己的接口，该弹的提示照弹。</summary>
        public static bool TryModRep(string id, int delta)
        {
            RepFaction f = FindRep(id);
            if (f == null) return false;
            if (delta == 0) return true;
            try { f.Mod(delta); return true; }
            catch (Exception ex)
            {
                Core.Log.Warning("[API] 改声望失败（" + f.Id + "）：" + ex.Message);
                return false;
            }
        }

        /// <summary>区的中文名（认不出就原样把 token 还回去）。</summary>
        public static string RepName(string id)
        {
            RepFaction f = FindRep(id);
            return f != null ? f.Name : (id ?? "");
        }

        // ── 事件（StoreEventManager 才是游戏真正跑的那套）────────────
        //
        // 之前摸的 EventTriggerManager 在实机上 current 一直是 null —— 它不是本作
        // 的事件系统，所以列表读不出来、触发也没反应。真正管事的是店铺站点上的
        // StoreStation.Instance.storeEventManager，事件蓝图就挂在那儿。

        private static StoreEventManager EventMgr()
        {
            try
            {
                StoreStation st = StoreStation.Instance;
                return st == null ? null : st.storeEventManager;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 取事件管理器失败：" + ex.Message);
                return null;
            }
        }

        /// <summary>事件清单里的一条。</summary>
        internal sealed class EventRef
        {
            public string Id;                       // 蓝图 identifier
            public string Name;                     // 显示名（拿蓝图现造一个实例问出来的）
            public string Kind;                     // 常规 / 威胁 / 氛围
            public StoreEventBlueprint Blueprint;   // 触发时现造实例用

            public EventRef(string id, string name, string kind, StoreEventBlueprint bp)
            {
                Id = id; Name = name; Kind = kind; Blueprint = bp;
            }
        }

        private static List<EventRef> _events;

        private static void CollectEvents(StoreEventManager m,
            List<StoreEventBlueprint> bps, string kind, List<EventRef> into)
        {
            if (bps == null) return;
            for (int i = 0; i < bps.Count; i++)
            {
                StoreEventBlueprint bp = null;
                try { bp = bps[i]; } catch { }
                if (bp == null) continue;

                string id = null;
                try { id = bp.identifier; } catch { }

                // 蓝图里只有「怎么造事件」的委托，名字得先造一个实例才问得出来。
                // 造出来的这个只是拿来读名字，真正触发时会再造一个新的。
                string name = null;
                try
                {
                    StoreEvent ev = bp.storeEventFunc.Invoke();
                    if (ev != null)
                    {
                        name = ev.GetDisplayName();
                        if (string.IsNullOrEmpty(name)) name = ev.displayName;
                    }
                }
                catch (Exception ex) { Core.Debug("[API] 读事件名失败：" + ex.Message); }

                if (string.IsNullOrEmpty(name)) name = id;
                if (string.IsNullOrEmpty(name)) name = "事件 " + (into.Count + 1);
                into.Add(new EventRef(id, name, kind, bp));
            }
        }

        /// <summary>游戏里能触发的全部事件（供水危机这类都在里面）。空结果不缓存，下次会重试。</summary>
        public static List<EventRef> EventList()
        {
            if (_events != null && _events.Count > 0) return _events;

            List<EventRef> list = new List<EventRef>();

            // 这三张蓝图表是静态字段（游戏启动时就填好了），不挂在管理器实例上，
            // 所以就算 StoreStation 还没起来也照样列得出来 —— 只有触发才非要有实例。
            StoreEventManager m = EventMgr();
            try { CollectEvents(m, StoreEventManager.normalEventBlueprints, "常规", list); }
            catch (Exception ex) { Core.Debug("[API] 读常规事件失败：" + ex.Message); }
            try { CollectEvents(m, StoreEventManager.threatEventBlueprints, "威胁", list); }
            catch (Exception ex) { Core.Debug("[API] 读威胁事件失败：" + ex.Message); }
            try { CollectEvents(m, StoreEventManager.cosmeticEventBlueprints, "氛围", list); }
            catch (Exception ex) { Core.Debug("[API] 读氛围事件失败：" + ex.Message); }

            if (list.Count > 0)
            {
                _events = list;
                Core.Log.Msg("[事件] 事件清单已加载，" + list.Count + " 个。");
            }
            return list;
        }

        /// <summary>现在挂在场上的事件名字，用「、」连起来；读不到返回 null。</summary>
        public static string ActiveEventsText()
        {
            StoreEventManager m = EventMgr();
            if (m == null) return null;
            try
            {
                List<StoreEvent> act = m.GetActiveEvents();
                if (act == null || act.Count == 0) return "今天没有事件";
                string text = "";
                for (int i = 0; i < act.Count; i++)
                {
                    StoreEvent e = act[i];
                    if (e == null) continue;
                    string n = null;
                    try { n = e.GetDisplayName(); } catch { }
                    if (string.IsNullOrEmpty(n)) { try { n = e.displayName; } catch { } }
                    if (string.IsNullOrEmpty(n)) continue;
                    if (text.Length > 0) text += "、";
                    text += n;
                }
                return text.Length > 0 ? text : "今天没有事件";
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 读当前事件失败：" + ex.Message);
                return null;
            }
        }

        /// <summary>强行触发第 index 个事件（编号从 1 数，和 event list 打印的一致）。</summary>
        public static string FireEvent(int index, out string name)
        {
            name = null;
            List<EventRef> list = EventList();
            if (list.Count == 0) return "读不到事件列表 —— 事件管理器是进档之后才起来的。";
            if (index < 1 || index > list.Count) return "编号要落在 1 到 " + list.Count + " 之间。";

            StoreEventManager m = EventMgr();
            if (m == null) return "事件管理器没起来。";

            EventRef e = list[index - 1];
            name = e.Name;
            try
            {
                StoreEvent ev = e.Blueprint.storeEventFunc.Invoke();
                if (ev == null) return "这个事件造不出实例。";
                // 第二个参数开调试日志，第三个是「不管条件硬上」—— 命令就是要强推
                m.ActivateEvent(ev, true, true);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>让游戏重新掷一次今天的事件。</summary>
        public static bool RefreshEvents()
        {
            StoreEventManager m = EventMgr();
            if (m == null) return false;
            try
            {
                m.QueueEvent();
                return true;
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[API] 重掷事件失败：" + ex.Message);
                return false;
            }
        }

        // ── 物品 ──────────────────────────────────────────────────────

        private static List<string> _ids;
        private static readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> _noIcon = new HashSet<string>();

        // ── 目录分类（物品控制台的「分类」下拉框）──────────────────────
        // 顺序和中文名照 NotEnoughItems 的分类表来，免得两边叫法不一样。

        private static readonly List<KeyValuePair<string, string>> _dirs =
            new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("MiscItemDirectory", "杂项"),
            new KeyValuePair<string, string>("ToolDirectory", "工具"),
            new KeyValuePair<string, string>("MedsItemDirectory", "医疗"),
            new KeyValuePair<string, string>("FoodItemDirectory", "食物"),
            new KeyValuePair<string, string>("WineDirectory", "酿酒"),
            new KeyValuePair<string, string>("HydroponicDirectory", "种植水培"),
            new KeyValuePair<string, string>("HusbandryDirectory", "养殖"),
            new KeyValuePair<string, string>("MaterialDirectory", "材料"),
            new KeyValuePair<string, string>("GunsItemDirectory", "武器配件"),
            new KeyValuePair<string, string>("GunModDirectory", "武器配件"),
            new KeyValuePair<string, string>("MeleeWeaponItemDirectory", "武器配件"),
            new KeyValuePair<string, string>("ExplosiveItemDirectory", "武器配件"),
            new KeyValuePair<string, string>("ArmorItemDirectory", "武器配件"),
            new KeyValuePair<string, string>("ContainerItemDirectory", "容器设施"),
            new KeyValuePair<string, string>("FurnitureItemDirectory", "容器设施"),
            new KeyValuePair<string, string>("ModuleDirectory", "模块"),
            new KeyValuePair<string, string>("ModItemDirectory", "模块"),
            new KeyValuePair<string, string>("StationMachinery", "机器"),
            new KeyValuePair<string, string>("RuinedMachineDirectory", "废品"),
            new KeyValuePair<string, string>("KeyItemDirectory", "文书"),
            new KeyValuePair<string, string>("OrganDirectory", "医疗"),
            new KeyValuePair<string, string>("AmenitiesItemDirectory", "杂项"),
            new KeyValuePair<string, string>("ConstructionItemDirectory", "材料"),
            new KeyValuePair<string, string>("EquipmentDirectory", "工具"),
            new KeyValuePair<string, string>("ShipItemDirectory", "机器"),
            new KeyValuePair<string, string>("ShipSystemDirectory", "机器"),
            new KeyValuePair<string, string>("TechnicianBackpackDirectory", "背包"),
            new KeyValuePair<string, string>("PlayerAbilityItemDirectory", "杂项"),
            new KeyValuePair<string, string>("UnusedDirectory", "未分类"),
        };

        public static List<KeyValuePair<string, string>> ItemDirectories()
        {
            return _dirs;
        }

        /// <summary>目录里的一个物品：ID + 它属于哪个目录。</summary>
        internal sealed class ItemRef
        {
            public string Id;
            public string Dir;   // 目录类名，如 MiscItemDirectory
            public string Cat;   // 中文分类，如 杂项

            public ItemRef(string id, string dir, string cat)
            {
                Id = id; Dir = dir; Cat = cat;
            }
        }

        private static List<ItemRef> _catalog;

        /// <summary>
        /// 按目录铺开的物品清单（物品控制台的网格用）。
        /// 目录表是进档之后才注册的，所以缓存；空结果不缓存，下次进来会重试。
        /// </summary>
        public static List<ItemRef> CatalogItems()
        {
            if (_catalog != null && _catalog.Count > 0) return _catalog;

            List<ItemRef> list = new List<ItemRef>();
            HashSet<string> seen = new HashSet<string>();
            try
            {
                for (int d = 0; d < _dirs.Count; d++)
                {
                    List<string> raw = null;
                    try { raw = DirectoryMaster.GetIdentifierList<GameItem>(_dirs[d].Key); }
                    catch (Exception ex)
                    {
                        Core.Debug("[物品] 读目录 " + _dirs[d].Key + " 失败：" + ex.Message);
                        continue;
                    }
                    if (raw == null) continue;

                    for (int i = 0; i < raw.Count; i++)
                    {
                        string id = null;
                        try { id = raw[i]; } catch { }
                        if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                        list.Add(new ItemRef(id, _dirs[d].Key, _dirs[d].Value));
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 枚举物品目录失败：" + ex.Message);
            }

            if (list.Count > 0)
            {
                _catalog = list;
                Core.Log.Msg("[物品] 物品控制台目录已加载，" + list.Count + " 件。");
            }
            return list;
        }

        /// <summary>
        /// 全部物品 ID。DirectoryMaster 的目录是进档后才注册的，所以这里会缓存；
        /// 空结果不缓存，下次调用会重试。
        /// </summary>
        public static List<string> AllItemIds()
        {
            if (_ids != null && _ids.Count > 0) return _ids;
            try
            {
                List<string> raw =
                    DirectoryMaster.GetIdentifierList<GameItem>(null);
                if (raw == null || raw.Count == 0) return _ids ?? new List<string>();

                List<string> list = new List<string>(raw.Count);
                for (int i = 0; i < raw.Count; i++)
                {
                    string id = null;
                    try { id = raw[i]; } catch { }
                    if (!string.IsNullOrEmpty(id)) list.Add(id);
                }
                if (list.Count > 0)
                {
                    _ids = list;
                    Core.Log.Msg("[物品] 目录已加载，" + list.Count + " 件。");
                }
                return list;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 读物品目录失败：" + ex.Message);
                return _ids ?? new List<string>();
            }
        }

        /// <summary>丢掉物品目录缓存（换存档时用）。</summary>
        public static void DropItemCache()
        {
            _ids = null;
            _catalog = null;
            _events = null;
            _names.Clear();
            _sprites.Clear();
            _noIcon.Clear();
        }

        /// <summary>
        /// 把一个词解析成物品 ID：先当 ID 认，再当中文名/ID 片段找。
        /// 找不到或匹配到多个（没法确定是哪个）都返回 null。
        /// </summary>
        public static string FindItemId(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            token = token.Trim();
            if (token.Length == 0) return null;

            if (ItemExists(token)) return token;

            string lower = token.ToLowerInvariant();
            List<string> all = AllItemIds();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].ToLowerInvariant() == lower) return all[i];
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (ItemName(all[i]) == token) return all[i];
                // 英文界面下也得认得出中文名，反过来也一样
                if (ItemNameZh(all[i]) == token) return all[i];
            }

            string hit = null;
            for (int i = 0; i < all.Count; i++)
            {
                string name = ItemName(all[i]);
                string zh = ItemNameZh(all[i]);
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                    || zh.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                    || all[i].IndexOf(lower, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (hit != null) return null;   // 匹配到多个，让调用方自己列出来
                    hit = all[i];
                }
            }
            return hit;
        }

        /// <summary>
        /// 物品在游戏里的那张图。
        ///
        /// 走的是 RenderHandler.LoadFromAtlas(spriteAtlasPath, spritePath) ——
        /// 这两个字段就是「图集 + 图集里的位置」，游戏自己画物品用的就是这条路。
        /// 早先用 SpriteDict.GetSprite 查是错的：那是人物/头像的表，查出来全是人。
        /// 取不到返回 null，调用方自己兜底。
        /// </summary>
        public static Sprite ItemIcon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            Sprite cached;
            if (_sprites.TryGetValue(id, out cached)) return cached;
            if (_noIcon.Contains(id)) return null;

            Sprite found = null;
            try
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item != null)
                {
                    string atlas = null, path = null;
                    try { atlas = item.spriteAtlasPath; } catch { }
                    try { path = item.spritePath; } catch { }
                    if (!string.IsNullOrEmpty(atlas) && !string.IsNullOrEmpty(path))
                        found = RenderHandler.LoadFromAtlas(atlas, path);
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 取物品图标失败（" + id + "）：" + ex.Message);
            }

            if (found != null) _sprites[id] = found;
            else _noIcon.Add(id);      // 记一笔，别每次都去图集里翻一遍
            return found;
        }

        /// <summary>
        /// 物品的显示名：中文用游戏自己的显示名，英文走 ItemNames 那张对照表
        /// （对照表查不到就退回中文名，至少不是一串 ID）。
        /// </summary>
        public static string ItemName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string cached;
            if (_names.TryGetValue(id, out cached)) return cached;

            string name = ItemNameZh(id);
            if (L10n.En)
            {
                string en = ItemNames.En(id);
                if (!string.IsNullOrEmpty(en)) name = en;
            }
            try { _names[id] = name; } catch { }
            return name;
        }

        /// <summary>物品的中文显示名，取不到就回落成 ID。</summary>
        public static string ItemNameZh(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            try
            {
                string raw = GeneralHelper.GetDisplayNameFromID(id);
                if (!string.IsNullOrEmpty(raw) && raw != id) return raw;
            }
            catch { }
            return id;
        }

        /// <summary>丢掉物品名缓存（切中英文时用，不然旧语言的名字会一直留着）。</summary>
        public static void DropNameCache()
        {
            _names.Clear();
        }

        /// <summary>这件物品值多少钱（讨价还价后的价格）。</summary>
        public static long ItemValue(GameItem item)
        {
            if (item == null) return 0;
            try { return item.GetNegociatedValue(); }
            catch { return 0; }
        }

        /// <summary>按 ID 取估价（刷物品卡片的预览用）。</summary>
        public static long ItemPrice(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            try { return ItemValue(DirectoryMaster.Item(id, true)); }
            catch { return 0; }
        }

        public static bool ItemExists(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            try { return DirectoryMaster.Has<GameItem>(id); }
            catch { return false; }
        }

        /// <summary>
        /// 生成一件物品塞进店铺库存。toStore 为 true 时放进后仓（库房），否则放柜台。
        /// 返回 null 表示成功，否则返回失败原因。
        /// </summary>
        public static string Spawn(string id, int count, bool toStore)
        {
            if (string.IsNullOrEmpty(id)) return "物品 ID 不能为空。";
            if (count < 1) count = 1;

            if (!WorldReady)
                return Store == null ? "还没进存档，先生成一个档再刷。" : "店铺库存还没就绪，稍等一下再试。";

            int ok = 0;
            for (int n = 0; n < count; n++)
            {
                string err = SpawnOne(id, toStore);
                if (err != null)
                {
                    return ok > 0
                        ? "已生成 " + ok + " 件，第 " + (ok + 1) + " 件失败：" + err
                        : err;
                }
                ok++;
            }
            return null;
        }

        private static string SpawnOne(string id, bool toStore)
        {
            try
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item == null) return "游戏里没有叫「" + id + "」的物品。";

                string real = null;
                try { real = item.identifier; } catch { }
                if (string.IsNullOrEmpty(real)) return "「" + id + "」解析出来是空物品。";

                EmporiumEntry emp = Emporium;
                GameGridInventory inv = null;
                // 名字是反着的：backInvinvElement 才是柜台（客人看得见的桌子），
                // invElement 是后间的主库存。照 RatColonyOptimizer 的标注来（the counter / the main inventory）。
                try { inv = toStore ? emp.invElement : emp.backInvinvElement; }
                catch (Exception ex) { Core.Debug("[API] 取库存失败：" + ex.Message); }
                if (inv == null)
                {
                    try { inv = toStore ? emp.backInvinvElement : emp.invElement; } catch { }
                }
                if (inv == null) return toStore ? "仓库库存对象为空。" : "柜台库存对象为空。";

                SlotMarker slot = null;
                try { slot = inv.TryFindOneValidInventorySlot(item, false); }
                catch (Exception ex) { Core.Debug("[API] 找空位失败：" + ex.Message); }

                if (slot == null)
                {
                    List<GameItem> one =
                        new List<GameItem>();
                    one.Add(item);
                    inv.UncheckedAcceptAll(one);
                }
                else
                {
                    slot.AcceptUnchecked();
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>玩家当前拥有的（店铺里的）物品。</summary>
        public static List<GameItem> OwnedItems()
        {
            List<GameItem> list = new List<GameItem>();
            PlayerStore s = Store;
            if (s == null) return list;
            try
            {
                List<GameItem> raw = s.FindAllItem(true);
                if (raw == null) return list;
                for (int i = 0; i < raw.Count; i++)
                {
                    GameItem it = null;
                    try { it = raw[i]; } catch { }
                    if (it != null) list.Add(it);
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 枚举背包失败：" + ex.Message);
            }
            return list;
        }

        public static bool OwnsItem(string id)
        {
            PlayerStore s = Store;
            if (s == null || string.IsNullOrEmpty(id)) return false;
            try { return s.IsPlayerOwnThisItem(id); }
            catch { return false; }
        }

        /// <summary>销毁一件物品（先从它所在的库存里踢出来，再销毁）。</summary>
        public static string DestroyItem(GameItem item)
        {
            if (item == null) return "物品为空。";
            try
            {
                GameInventory inv = null;
                try { inv = item.parentInventory; } catch { }

                if (inv != null)
                {
                    bool backup = false;
                    try
                    {
                        backup = inv.overrideLockRemove;
                        inv.overrideLockRemove = true;
                    }
                    catch { }
                    try
                    {
                        if (!inv.Expel(item)) return "库存拒绝了移除请求。";
                    }
                    finally
                    {
                        try { inv.overrideLockRemove = backup; } catch { }
                    }
                }
                item.Destroy();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// 鼠标正指着的那件物品（快捷删除用）。
        ///
        /// 游戏里记「鼠标底下是谁」的地方不止一处：右键菜单的语境处理器、左键拖拽的鼠标处理器、
        /// 多选高亮各有一份。只问其中一个经常问不到（有的只管能拖的东西），所以挨个问一遍。
        /// </summary>
        public static GameItem HoveredItem()
        {
            string src;
            return HoveredItem(out src);
        }

        public static GameItem HoveredItem(out string src)
        {
            src = null;

            try
            {
                ItemContextHandler h = ItemContextHandler.current;
                if (h != null)
                {
                    GameItem it = ReadPrivate<GameItem>(h, "currentItem");
                    if (it != null) { src = "右键悬停"; return it; }
                }
            }
            catch { }

            try
            {
                ItemMouseHandler h = ItemMouseHandler.current;
                if (h != null)
                {
                    GameItem it = ReadPrivate<GameItem>(h, "currentItem");
                    if (it != null) { src = "左键悬停"; return it; }
                }
            }
            catch { }

            try
            {
                ItemMultiSelectHandler h = ItemMultiSelectHandler.current;
                if (h != null)
                {
                    GameItem it = ReadPrivate<GameItem>(h, "hoverItem");
                    if (it != null) { src = "多选悬停"; return it; }
                }
            }
            catch { }

            return null;
        }

        /// <summary>三个悬停源各自认到了什么。删不掉时打进提示里，一眼看出是哪条路没通。</summary>
        private static string HoverState()
        {
            string s = "";
            try
            {
                ItemContextHandler h = ItemContextHandler.current;
                s += "右键=" + (h == null ? "无" : (ReadPrivate<GameItem>(h, "currentItem") == null ? "空" : "有"));
            }
            catch { s += "右键=错"; }

            try
            {
                ItemMouseHandler h = ItemMouseHandler.current;
                s += " 左键=" + (h == null ? "无" : (ReadPrivate<GameItem>(h, "currentItem") == null ? "空" : "有"));
            }
            catch { s += " 左键=错"; }

            try
            {
                ItemMultiSelectHandler h = ItemMultiSelectHandler.current;
                s += " 多选=" + (h == null ? "无" : (ReadPrivate<GameItem>(h, "hoverItem") == null ? "空" : "有"));
            }
            catch { s += " 多选=错"; }

            return s;
        }

        // ── 快捷删除：右键选中 + Ctrl+Shift+X ───────────────────────────────

        private static GameItem _picked;       // 右键点中的那件
        private static string _pickedSrc;      // 它当时是从哪条路认出来的
        private static float _pickWindow;      // 右键按下之后的一小段探测窗口

        /// <summary>
        /// 由 Core.OnUpdate 每帧驱动（面板关着的时候才跑）。
        /// 在店里右键点一下物品 → 把它记成「选中的那件」。
        ///
        /// 为什么要先选一下：鼠标底下的判定靠游戏自己的几个悬停处理器，只在特定时机才亮，
        /// 直接问经常问不到；而右键恰恰是游戏用来「指定这一件」的动作，这时候问最准。
        /// 所以右键按下之后留 0.35 秒，每帧问一遍，问到就收工。
        /// </summary>
        public static void TickItemPick()
        {
            try
            {
                if (Input.GetMouseButtonDown(1)) _pickWindow = Time.unscaledTime + 0.35f;
                if (_pickWindow <= 0f) return;
                if (Time.unscaledTime > _pickWindow) { _pickWindow = 0f; return; }

                string src;
                GameItem item = HoveredItem(out src);
                if (item == null) return;

                _pickWindow = 0f;
                _picked = item;
                _pickedSrc = src;

                string name = null;
                try { name = ItemName(item.identifier); } catch { }
                Out.Ok("已选中 " + Palette.TagVal(string.IsNullOrEmpty(name) ? "物品" : name)
                    + Palette.TagMuted("　(" + src + ")") + "，按 Ctrl+Shift+X 删掉。");
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 右键选物品失败：" + ex.Message);
            }
        }

        /// <summary>选中的那件还在不在 —— 它可能已经被别的途径删掉了。</summary>
        public static bool PickedAlive()
        {
            try { return _picked != null; }
            catch { return false; }
        }

        /// <summary>选中物品的读数，给「删除选中」卡用；没选中返回 null（卡片显示 —）。</summary>
        public static string PickedText()
        {
            if (!PickedAlive()) return null;
            string name = null;
            try { name = ItemName(_picked.identifier); } catch { }
            return string.IsNullOrEmpty(name) ? "物品" : name;
        }

        /// <summary>删掉「选中的那件」（卡片按钮走这条；快捷键走 TickItemDelete）。</summary>
        public static string DeletePicked(out string name)
        {
            name = null;
            if (!PickedAlive()) return "还没选中物品 —— 先在店里右键点一下要删的东西。";

            GameItem item = _picked;
            try { name = ItemName(item.identifier); } catch { }
            string err = DestroyItem(item);
            if (err == null)
            {
                _picked = null;
                _pickedSrc = null;
                TryRefreshCounter();
            }
            return err;
        }

        /// <summary>
        /// 由 Core.OnUpdate 每帧驱动（面板关着的时候才跑）。
        /// 按住 Ctrl + Shift 敲一下 X：把「右键选中的那件」删掉；没选过就退回鼠标底下现认的那件。
        ///
        /// 主键特意不用 D：别的模组（Probably Stolen Item Manager）的快速删除是 Shift+D，
        /// 按 Ctrl 也不会把它挡掉，用 D 的话一次按键两个模组一起删。
        /// </summary>
        public static void TickItemDelete()
        {
            try
            {
                bool held = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                         && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                if (!held) return;
                if (!Input.GetKeyDown(KeyCode.X)) return;

                // 选中的那件可能已经被别处删了，先对一下账
                if (_picked != null && !PickedAlive())
                {
                    _picked = null;
                    _pickedSrc = null;
                }

                string src = _pickedSrc;
                GameItem item = _picked;
                if (item == null)
                {
                    item = HoveredItem(out src);
                    if (item == null)
                    {
                        Out.Warn("先右键点一下要删的物品，再按 Ctrl+Shift+X。（" + HoverState() + "）");
                        return;
                    }
                }

                string name = null;
                try { name = ItemName(item.identifier); } catch { }
                string err = DestroyItem(item);
                if (err != null) { Out.Err("删除失败：" + err); return; }

                TryRefreshCounter();
                _picked = null;
                _pickedSrc = null;
                Out.Ok("已删除 " + Palette.TagVal(string.IsNullOrEmpty(name) ? "物品" : name)
                    + Palette.TagMuted("　(" + src + ")"));
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 快捷删除失败：" + ex.Message);
            }
        }

        // ── 物品属性：划过一批 → 开游戏自己的打标器 → 改一条就套给其余各件 ──────
        //
        // 游戏原生的打标器一次只开一件物品，所以这里把它当「样板」用：
        // 界面开着的时候盯着上面那几行，玩家改了哪一行，就把这一行的改动借游戏
        // 自己的写入通道套给同批的其它物品（同一条属性对同一条属性）。

        private static readonly List<GameItem> _swept = new List<GameItem>();   // 这一批要一起改的
        private static IntPtr _sweptLast;               // 上一件划到的（停在货上不动不算新的一笔）
        private static GameItem _labelerSample;         // 界面开着的那件（样板）
        private static bool _labelerWasUp;              // 上一帧界面还开着吗
        private static LabelerElement _writer;          // 藏在屏幕外的写手：借它走游戏的写入通道
        private static readonly Dictionary<string, int> _rowBase = new Dictionary<string, int>();  // 各行上次的下拉框读数

        private static LabelerUIManager LabelerMgr()
        {
            try { return LabelerUIManager.Instance; }
            catch { return null; }
        }

        /// <summary>游戏的打标器界面现在开着吗。</summary>
        private static bool LabelerUp()
        {
            try
            {
                LabelerUIManager m = LabelerMgr();
                return m != null && m.panel != null && m.panel.activeInHierarchy;
            }
            catch { return false; }
        }

        /// <summary>
        /// 打开游戏自己的打标器界面 —— 改物品属性就是在这上面改的。
        ///
        /// 早先这里试过「替玩家写」：造一行藏在屏幕外的 LabelerElement，把下拉框拨到
        /// 目标值再触发 OnDropdownValueChanged。实机证明打标面板关着的时候那条路走不通
        /// （写入行不在活动层级里，游戏不认这一笔），所以改成直接把游戏这扇门推开 ——
        /// 玩家在原生界面上改，显示字和价值修正自然全对，也改不出游戏不认的东西。
        /// </summary>
        public static string OpenLabeler(GameItem item)
        {
            if (item == null) return "没找到物品";

            LabelerUIManager mgr = LabelerMgr();
            if (mgr == null) return "打标器管理器还没就绪";

            try { mgr.OpenUI(item); }
            catch (Exception ex) { return "打开打标器界面失败：" + ex.Message; }

            bool up = false;
            try { up = mgr.panel != null && mgr.panel.activeInHierarchy; } catch { }
            return up ? null : "打标器界面没弹出来";
        }

        /// <summary>店里现在摆着的所有物品（柜台 / 后仓 / 前场三处，空的时候再扫一遍存档）。</summary>
        public static List<GameItem> StoreItems()
        {
            List<GameItem> list = new List<GameItem>();
            EmporiumEntry emp = Emporium;
            if (emp == null) return list;

            List<GameItem> all =
                new List<GameItem>();
            try { CollectInventory(emp.invElement, all); } catch { }
            try { CollectInventory(emp.backInvinvElement, all); } catch { }
            try { CollectInventory(emp.frontInvinvElement, all); } catch { }

            for (int i = 0; i < all.Count; i++) list.Add(all[i]);

            if (list.Count == 0)
            {
                List<GameItem> owned = OwnedItems();
                for (int i = 0; i < owned.Count; i++) list.Add(owned[i]);
            }
            return list;
        }

        /// <summary>
        /// 由 Core.OnUpdate 每帧驱动（只在面板关着的时候跑）。两件事：
        ///
        /// 1) 界面开着的时候：盯着原生那几行下拉框。玩家改哪一行，就把这一行的值
        ///    借游戏自己的写入通道套给同批的其它物品 —— 「一次改一批」就在这里。
        /// 2) 界面关着的时候：按住 Ctrl+Shift+A 划过物品（框选也算），划到哪件就记下哪件，
        ///    松手把游戏自己的打标器界面弹出来给最后那件当样板。
        /// </summary>
        public static void TickPropBrush()
        {
            try
            {
                if (LabelerUp())
                {
                    WatchLabelerEdits();
                    return;
                }

                // 界面刚被关掉：这一批收工
                if (_labelerWasUp)
                {
                    _labelerWasUp = false;
                    _labelerSample = null;
                    _writer = null;
                    _rowBase.Clear();
                    if (_swept.Count > 0)
                    {
                        _swept.Clear();
                        Out.Line(Palette.TagMuted("属性：界面关了，这一批到此为止；再划过物品可以接着来。"));
                    }
                }

                // 必须带 A 才算数：光按 Ctrl+Shift 是系统切输入法的组合，不能拿它当刷子
                bool held = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                         && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                         && Input.GetKey(KeyCode.A);

                if (!held)
                {
                    _sweptLast = IntPtr.Zero;
                    if (_swept.Count > 0) FlushSwept();
                    return;
                }

                // 鼠标划过一件记一件
                GameItem it = HoveredItem();
                if (it != null)
                {
                    IntPtr p = IntPtr.Zero;
                    try { p = Identity(it); } catch { }
                    if (p != IntPtr.Zero && p != _sweptLast) { _sweptLast = p; AddSwept(it); }
                }

                // 框选（游戏自己的多选框）也算：拖着的时候每帧扫一遍，选上的都进这批
                CollectBox();
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 属性划过失败：" + ex.Message);
            }
        }

        /// <summary>游戏多选框里现在选着的那批，也算进这一批。</summary>
        private static void CollectBox()
        {
            try
            {
                ItemMultiSelectHandler h = ItemMultiSelectHandler.current;
                if (h == null) return;
                var sel = ReadPrivate<System.Collections.IList>(h, "selectedItems");
                if (sel == null) return;
                for (int i = 0; i < sel.Count; i++) AddSwept(sel[i] as GameItem);
            }
            catch { }
        }

        /// <summary>往这一批里加一件（重复的忽略），顺手报一声。</summary>
        private static void AddSwept(GameItem it)
        {
            if (it == null) return;
            IntPtr p = IntPtr.Zero;
            try { p = Identity(it); } catch { }
            if (p == IntPtr.Zero) return;

            for (int i = 0; i < _swept.Count; i++)
            {
                try { if (_swept[i] != null && Identity(_swept[i]) == p) return; } catch { }
            }

            string name = null;
            try { name = ItemName(it.identifier); } catch { }
            _swept.Add(it);
            Out.Line(Palette.TagMuted("划过 ") + Palette.TagVal(string.IsNullOrEmpty(name) ? "物品" : name)
                + Palette.TagMuted("（第 " + _swept.Count + " 件）"));
        }

        /// <summary>松手了：把划到最后的那件交给游戏自己的打标器界面当样板。</summary>
        private static void FlushSwept()
        {
            CollectBox();

            GameItem sample = _swept[_swept.Count - 1];
            int n = _swept.Count;
            if (sample == null) { _swept.Clear(); return; }

            string err = OpenLabeler(sample);
            if (err != null) { _swept.Clear(); Out.Warn("属性：划过 " + n + " 件，但 " + err + "。"); return; }

            TryRefreshCounter();
            _labelerSample = sample;
            _labelerWasUp = true;
            _rowBase.Clear();

            if (n > 1)
            {
                Out.Ok(Palette.TagMuted("属性：这一批　") + Palette.TagVal(n + " 件")
                    + Palette.TagMuted("　样板是　") + Palette.TagVal(NameOf(sample))
                    + Palette.TagMuted("。改哪一条，这一批的同一条属性就一起改。"));
            }
            else
            {
                Out.Ok("属性：打标器界面开了，在上面改就行。");
            }
        }

        private static string NameOf(GameItem it)
        {
            string n = null;
            try { n = ItemName(it.identifier); } catch { }
            return string.IsNullOrEmpty(n) ? "这件物品" : n;
        }

        // ── 界面开着的时候：改一条，套一批 ────────────────────────────────

        /// <summary>
        /// 盯着界面上的行：哪一行的下拉框动了，就把那一行的值套给同批的其它物品。
        /// 记的是「上一次的读数」，所以头一帧只记基线，不会把初始状态误判成改动。
        /// </summary>
        private static void WatchLabelerEdits()
        {
            if (_swept.Count == 0) return;     // 不是我们开的界面：玩家自己开的，别插手

            LabelerUIManager mgr = LabelerMgr();
            if (mgr == null) return;

            if (!_labelerWasUp) { _labelerWasUp = true; SnapRows(mgr); return; }

            List<LabelerElement> rows = RowsOf(mgr);
            for (int i = 0; i < rows.Count; i++)
            {
                LabelerElement row = rows[i];
                if (row == null) continue;

                string cat = null;
                try { cat = row.GetCategory(); } catch { }
                if (string.IsNullOrEmpty(cat)) continue;

                TMP_Dropdown dd;
                int v;
                try { dd = row.dropdown; if (dd == null) continue; v = dd.value; } catch { continue; }

                int last;
                if (!_rowBase.TryGetValue(cat, out last)) { _rowBase[cat] = v; continue; }
                if (last == v) continue;
                _rowBase[cat] = v;

                Spread(cat, v, OptionTextOf(dd, v));
            }
        }

        /// <summary>把界面上现有的行按当前读数记成基线。</summary>
        private static void SnapRows(LabelerUIManager mgr)
        {
            _rowBase.Clear();
            List<LabelerElement> rows = RowsOf(mgr);
            for (int i = 0; i < rows.Count; i++)
            {
                LabelerElement row = rows[i];
                if (row == null) continue;
                try
                {
                    string cat = row.GetCategory();
                    if (string.IsNullOrEmpty(cat) || row.dropdown == null) continue;
                    _rowBase[cat] = row.dropdown.value;
                }
                catch { }
            }
        }

        /// <summary>把「某条属性 = 某个值」套给这一批里除样板以外的每一件。</summary>
        private static void Spread(string cat, int value, string optionText)
        {
            LabelerUIManager mgr = LabelerMgr();
            if (mgr == null) return;

            GameItem keep = null;
            try { keep = ReadPrivate<GameItem>(mgr, "curItem"); } catch { }

            int ok = 0, miss = 0, fail = 0;
            for (int i = 0; i < _swept.Count; i++)
            {
                GameItem it = _swept[i];
                if (it == null) continue;
                if (_labelerSample != null)
                {
                    try { if (Identity(it) == Identity(_labelerSample)) continue; } catch { }
                }

                ItemFeature f = FeatureOf(it, cat);
                if (f == null) { miss++; continue; }

                if (WriteLabel(it, f, value, optionText)) ok++; else fail++;
            }

            try { if (keep != null) WritePrivate(mgr, "curItem", keep); } catch { }   // 界面的落点认回样板

            string line = Palette.TagMuted("属性：") + Palette.TagVal(cat) + " → " + Palette.TagVal(optionText ?? "—")
                + Palette.TagMuted("　同批改了　") + Palette.TagVal(ok + " 件");
            if (miss > 0) line += Palette.TagMuted("　没这条属性的跳过了　" + miss + " 件");
            if (fail > 0) line += Palette.TagMuted("　游戏没认的　" + fail + " 件");
            Out.Line(line);
        }

        // ── 借游戏自己的通道写一笔标签 ────────────────────────────────────

        /// <summary>
        /// 藏在屏幕外的一行打标器元素，借它走游戏自己的写入流程。只在界面开着的时候现造：
        /// 界面关着时这行不在活动层级里，游戏不认这一笔（早先「面板关着硬写」那版就这么失效的）。
        /// </summary>
        private static LabelerElement HiddenWriter(LabelerUIManager mgr)
        {
            try
            {
                if (_writer != null && _writer != null && _writer.gameObject != null
                    && _writer.gameObject.activeInHierarchy)
                {
                    return _writer;
                }
            }
            catch { }
            _writer = null;

            try
            {
                if (mgr.elementPrefab == null) return null;
                Transform parent = mgr.panel != null ? mgr.panel.transform
                    : (mgr.verticalLayoutGroup != null ? mgr.verticalLayoutGroup.transform : null);
                if (parent == null) return null;

                GameObject go = UnityEngine.Object.Instantiate<GameObject>(mgr.elementPrefab, parent);
                if (go == null) return null;

                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-6000f, -6000f);
                }

                _writer = go.GetComponent<LabelerElement>();
                if (_writer == null) { UnityEngine.Object.Destroy(go); return null; }
                Core.Debug("[API] 属性：隐藏写入行备好了。");
                return _writer;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 备写入行失败：" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 给一件物品写一笔标签：把写手那一行绑到它这条属性上，拨到目标值，再让游戏自己走一遍
        /// 改动回调。顺序照搬 LabelerMultiSelect 那套（先绑行、再把 ReadPrivate<GameItem>(mgr, "curItem") 指到这件物品，
        /// 游戏才把这一笔算在它头上），写完读回来对账。
        /// </summary>
        private static bool WriteLabel(GameItem item, ItemFeature feature, int value, string optionText)
        {
            LabelerUIManager mgr = LabelerMgr();
            if (mgr == null) return false;

            LabelerElement w = HiddenWriter(mgr);
            if (w == null || w.dropdown == null) return false;

            string before = LabelOf(feature);
            try
            {
                try { w.dropdown.ClearOptions(); } catch { }
                w.InitElement(feature);
                try { WritePrivate(mgr, "curItem", item); } catch { }

                SetDropdownQuiet(w.dropdown, IndexOfOption(w.dropdown, value, optionText));

                string want = null;
                try { want = w.GetSelectedConditionIdentifier(); } catch { }
                try { w.OnDropdownValueChanged(); } catch { }

                string after = LabelOf(feature);
                if (!string.IsNullOrEmpty(want)) return after == want;
                return before != after;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 属性写入失败：" + ex.Message);
                return false;
            }
        }

        private static void SetDropdownQuiet(TMP_Dropdown dd, int value)
        {
            if (dd == null) return;
            try { dd.SetValueWithoutNotify(value); dd.RefreshShownValue(); }
            catch { try { dd.value = value; } catch { } }
        }

        private static int IndexOfOption(TMP_Dropdown dd, int fallback, string optionText)
        {
            try
            {
                var opts = dd.options;
                if (opts == null || opts.Count == 0) return Mathf.Max(0, fallback);
                if (!string.IsNullOrEmpty(optionText))
                {
                    for (int i = 0; i < opts.Count; i++)
                    {
                        var od = opts[i];
                        if (od != null && od.text == optionText) return i;
                    }
                }
                return Mathf.Clamp(fallback, 0, opts.Count - 1);
            }
            catch { return Mathf.Max(0, fallback); }
        }

        private static string OptionTextOf(TMP_Dropdown dd, int index)
        {
            try
            {
                var opts = dd.options;
                if (opts == null || index < 0 || index >= opts.Count) return null;
                var od = opts[index];
                return od != null ? od.text : null;
            }
            catch { return null; }
        }

        /// <summary>这件物品有没有这条可编辑的属性（就是打标器上的一行）。</summary>
        private static ItemFeature FeatureOf(GameItem item, string category)
        {
            try
            {
                var fs = item.itemFeatures;
                if (fs == null) return null;
                for (int i = 0; i < fs.Count; i++)
                {
                    ItemFeature f = fs[i];
                    if (f == null) continue;
                    string c = null;
                    try { c = f.category; } catch { }
                    if (c != category) continue;

                    bool editable = false;
                    try { editable = LabelerHelper.IsEditableLabelFeature(f); } catch { }
                    if (editable) return f;
                }
            }
            catch { }
            return null;
        }

        /// <summary>这条属性现在挂着的是哪个值（写完成对账用）。</summary>
        private static string LabelOf(ItemFeature feature)
        {
            try
            {
                ItemCondition c = feature.GetCurrentItemCondition();
                return c == null ? null : c.identifier;
            }
            catch { return null; }
        }

        /// <summary>界面上的那几行（行挂在竖排布局底下，可能隔着一两层）。</summary>
        private static List<LabelerElement> RowsOf(LabelerUIManager mgr)
        {
            List<LabelerElement> list = new List<LabelerElement>();
            try
            {
                Transform t = mgr.verticalLayoutGroup != null ? mgr.verticalLayoutGroup.transform : null;
                if (t != null) RowsIn(t, list, 0);
            }
            catch { }
            return list;
        }

        private static void RowsIn(Transform t, List<LabelerElement> into, int depth)
        {
            if (t == null || depth > 4) return;

            LabelerElement e = null;
            try { e = t.GetComponent<LabelerElement>(); } catch { }
            if (e != null) into.Add(e);

            int n = 0;
            try { n = t.childCount; } catch { }
            for (int i = 0; i < n; i++)
            {
                Transform c = null;
                try { c = t.GetChild(i); } catch { }
                RowsIn(c, into, depth + 1);
            }
        }

        /// <summary>
        /// 挑一件来开打标器：右键选中那件（右键是游戏用来「指定这一件」的动作，认得最准）；
        /// 没选过就退回鼠标底下现认的。
        /// </summary>
        public static GameItem SampleItem()
        {
            if (PickedAlive()) return _picked;
            return HoveredItem();
        }

        // ── 标签「转真」：把真品质也拨成标签现在写的那个 ────────────────────
        //
        // 游戏里每条属性都挂着一对条件：假条件（fakeCondition，打标器贴上去的那个标签，
        // 看货时按它念）和真条件（realCondition，这件东西实际是什么样）。打标器只换前者，
        // 所以这里借游戏自己的 ItemFeature.SetConditions(假, 真) 把两边都拨成现在挂着的
        // 这个标签 —— 物品的真品质就跟标签对上了（纯冰那类模组也是用这一招把标称变实际）。

        /// <summary>这张卡现在会落在谁身上（卡片读数用）：一批就报几件，一件就报名字。</summary>
        public static string PropTargetText()
        {
            List<GameItem> t = LabelTargets();
            if (t.Count == 0) return null;
            if (t.Count > 1) return "这一批 " + t.Count + " 件";
            return NameOf(t[0]);
        }

        /// <summary>
        /// 转真该落在哪几件上：手上有这一批（Ctrl+Shift+A 划过的 / 框选的）就整批；
        /// 没批就认打标器界面开着的那件，再退回右键选中 / 鼠标底下那件。
        /// </summary>
        private static List<GameItem> LabelTargets()
        {
            List<GameItem> list = new List<GameItem>();
            for (int i = 0; i < _swept.Count; i++)
            {
                if (_swept[i] != null) list.Add(_swept[i]);
            }
            if (list.Count > 0) return list;

            GameItem one = null;
            if (LabelerUp())
            {
                try { LabelerUIManager m = LabelerMgr(); if (m != null) one = ReadPrivate<GameItem>(m, "curItem"); } catch { }
            }
            if (one == null) one = SampleItem();
            if (one != null) list.Add(one);
            return list;
        }

        /// <summary>
        /// 把目标物品的真品质拨成它现在挂着的标签。返回报账文案；null = 一件目标都没找到。
        /// trace = 每件走的是哪条路（水 / 药 / 普通属性），直接摆到控制台输出区里看。
        /// </summary>
        public static string MakeLabelsReal(out int items, out int feats, out List<string> trace)
        {
            items = 0;
            feats = 0;
            trace = new List<string>();

            List<GameItem> targets = LabelTargets();
            if (targets.Count == 0) return null;

            int fail = 0, refilled = 0, cured = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                GameItem it = targets[i];
                if (it == null) continue;

                List<ItemFeature> fs = null;
                try { fs = it.itemFeatures; } catch { }

                string name = null;
                try { name = it.name; } catch { }
                if (string.IsNullOrEmpty(name)) { try { name = it.identifier; } catch { } }
                if (string.IsNullOrEmpty(name)) name = "?";

                string tr = null;
                bool done = false;
                bool waterOK = false, chemOK = false;

                // 一、装水的：真纯度记在液体里，光拨属性没用，得按档位整瓶重灌
                bool water = false;
                try { water = WaterHelper.CanUseWaterContainer(it); } catch { }
                ItemFeature wf = WaterFeature(it);
                if (water || wf != null)
                {
                    string wt = null;
                    if (wf != null && TurnWaterReal(it, wf, out wt)) { done = true; waterOK = true; refilled++; }
                    if (wt != null) tr = wt;
                }

                // 二、药这类化学品：真纯度归 ChemicalProductHelper 管，真假两个条件分开写
                ItemFeature cf = null;
                bool chem = false;
                try { chem = ChemicalProductHelper.HasChemicalPurity(it); } catch { }
                if (!done && chem)
                {
                    try { cf = ChemicalProductHelper.GetChemicalFeature(it); } catch { }
                    string ct = null;
                    if (cf != null && TurnChemicalReal(it, cf, out ct)) { done = true; chemOK = true; cured++; }
                    if (ct != null) tr = tr == null ? ct : tr + "　" + ct;
                }

                // 三、普通属性：真假三对一起拨
                int got = 0;
                if (fs != null)
                {
                    for (int k = 0; k < fs.Count; k++)
                    {
                        ItemFeature f = fs[k];
                        if (f == null) continue;
                        if (waterOK && SameFeature(f, wf)) continue;   // 水那条已经整瓶重灌过
                        if (chemOK && SameFeature(f, cf)) continue;    // 药那条已经整条写过

                        bool editable = false;
                        try { editable = LabelerHelper.IsEditableLabelFeature(f); } catch { }
                        if (!editable) continue;

                        if (TurnReal(f)) { got++; feats++; } else fail++;
                    }
                }

                if (got > 0) { done = true; tr = (tr == null ? "" : tr + "　") + "属性 " + got + " 条转真"; }
                if (done) items++;
                if (tr == null) tr = "没动，这件上的属性类别：" + CatList(it);
                trace.Add("· " + name + "　" + tr);
            }

            if (items == 0 && fail == 0)
                return Palette.TagMuted("属性：这几件上没有打标器能改的属性，没得转。");

            string line = Palette.TagMuted("属性：把 ") + Palette.TagVal(items + " 件")
                + Palette.TagMuted(" 的真品质拨成标签现在写的那个，一共 ") + Palette.TagVal(feats + " 条");
            if (refilled > 0) line += Palette.TagMuted("　另有 ") + Palette.TagVal(refilled + " 瓶水") + Palette.TagMuted(" 是按那一档重灌的");
            if (cured > 0) line += Palette.TagMuted("　另有 ") + Palette.TagVal(cured + " 件药") + Palette.TagMuted(" 的真纯度也按标签写了");
            if (fail > 0) line += Palette.TagMuted("　游戏没认的　" + fail + " 条");
            return line;
        }

        // ── 水：真纯度 ← 标签 ────────────────────────────────────────────
        //
        // 瓶子里的水「实际是哪一档」不记在 ItemFeature 上 —— 那上面挂的只是标签，真纯度在
        // 液体那一套里，由 WaterHelper.GetWaterPurity() 读。所以光拨条件没用（看着就是「只
        // 换了标签」），得按标签写的那一档，用游戏自己的加水接口把瓶子重新灌一遍。纯冰、
        // CustomStart 那几个模组改水也是走这几个接口。

        private const string WaterCat = "CATEGORY_WATER_PURITY";

        /// <summary>水的纯度那条属性（不要求它在打标器上可改；找不到就整件放弃）。</summary>
        private static ItemFeature WaterFeature(GameItem item)
        {
            ItemFeature f = FeatureOf(item, WaterCat);
            if (f != null) return f;
            try { return item.FindItemFeatureByCategory(WaterCat); } catch { return null; }
        }

        /// <summary>
        /// 标签写的是哪一档水：0 纯 1 优质 2 基础 3 幽灵 4 铁锈 5 沟渠
        /// （跟游戏 WATER_DISPLAY / GetPurityArrayIndex 同序）；-1 = 认不出。
        /// </summary>
        private static int WaterKind(ItemCondition c)
        {
            string id = null, show = null;
            try { id = c.identifier; } catch { }
            try { show = c.display; } catch { }

            // 一、认条件标识（英文，换语言也不受影响）
            string s = (id ?? "").ToLowerInvariant();
            if (s.Contains("gutter")) return 5;
            if (s.Contains("rust")) return 4;
            if (s.Contains("ghost")) return 3;
            if (s.Contains("base")) return 2;
            if (s.Contains("quality")) return 1;
            if (s.Contains("pure")) return 0;

            // 二、跟游戏自己那张水档名表对（标签上写的多半就是从这儿来的）
            try
            {
                var names = WaterFeatureHelper.WATER_DISPLAY;
                if (names != null && !string.IsNullOrEmpty(show))
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        string one = null;
                        try { one = names[i]; } catch { }
                        if (string.IsNullOrEmpty(one)) continue;
                        if (show == one || show.Contains(one) || one.Contains(show)) return i;
                    }
                }
            }
            catch { }

            // 三、兜底：按已知的水名认
            string t = (show ?? "").ToLowerInvariant();
            if (t.Contains("沟渠") || t.Contains("gutter")) return 5;
            if (t.Contains("锈") || t.Contains("rust")) return 4;
            if (t.Contains("幽灵") || t.Contains("ghost")) return 3;
            if (t.Contains("基础") || t.Contains("base")) return 2;
            if (t.Contains("优质") || t.Contains("quality")) return 1;
            if (t.Contains("纯") || t.Contains("pure")) return 0;
            return -1;
        }

        /// <summary>那一档水在游戏里叫什么（读游戏自己的表；-1 也走这儿，返回整张表）。</summary>
        private static string WaterGradeName(int kind)
        {
            try
            {
                var names = WaterFeatureHelper.WATER_DISPLAY;
                if (names != null && kind >= 0 && kind < names.Length) return names[kind];
            }
            catch { }
            return "#" + kind;
        }

        /// <summary>诊断串：游戏认得的水档名、纯度值、条件标识（认不出标签时打这个）。</summary>
        private static string WaterCatalog()
        {
            string s = "";
            try
            {
                var names = WaterFeatureHelper.WATER_DISPLAY;
                if (names != null)
                {
                    s += " grades ";
                    for (int i = 0; i < names.Length; i++) s += i + "=" + (names[i] ?? "-") + " ";
                }
            }
            catch { }
            try
            {
                var ps = WaterFeatureHelper.WATER_PURITIES;
                if (ps != null)
                {
                    s += " purities ";
                    for (int i = 0; i < ps.Length; i++) s += i + "=" + ps[i] + " ";
                }
            }
            catch { }
            try
            {
                var list = ItemConditionList.conditionWaterPurity;
                if (list != null)
                {
                    s += " conditions ";
                    for (int i = 0; i < list.Count; i++)
                    {
                        ItemCondition c = list[i];
                        if (c == null) continue;
                        s += "[" + (c.identifier ?? "-") + "/" + (c.display ?? "-") + "] ";
                    }
                }
            }
            catch { }
            return s;
        }

        /// <summary>
        /// 把这瓶水按标签写的那一档真的重灌一遍（真纯度 ← 标签）。灌成了返回 true。
        /// </summary>
        private static bool TurnWaterReal(GameItem item, ItemFeature wf, out string trace)
        {
            trace = null;
            try
            {
                ItemCondition label = null;
                try { label = wf.GetCurrentItemCondition(); } catch { }
                if (label == null) { try { label = wf.fakeCondition; } catch { } }
                if (label == null) { trace = "水：属性上没挂着条件"; return false; }

                int kind = WaterKind(label);
                if (kind < 0)
                {
                    string id = null, show = null;
                    try { id = label.identifier; } catch { }
                    try { show = label.display; } catch { }
                    trace = "水：认不出标签（" + (id ?? "-") + " / " + (show ?? "-") + "），没敢灌";
                    Core.Debug("[API] 灌水：认不出标签是哪一档 " + (id ?? "-") + " / " + (show ?? "-") + "，先不灌。" + WaterCatalog());
                    return false;
                }

                int before = -1, beforeIdx = -1;
                try { before = WaterHelper.GetWaterPurity(item); } catch { }
                try { beforeIdx = WaterFeatureHelper.GetPurityArrayIndex(before); } catch { }

                try { WaterHelper.EmptyContainer(item); } catch { }

                int amount = -1;
                try { amount = WaterHelper.GetFreeCapacity(item); } catch { }
                if (amount <= 0)
                {
                    trace = "水：倒空了却没容量（amount=" + amount + "），没灌成";
                    Core.Debug("[API] 灌水：倒空之后还是没容量（amount=" + amount + "），没灌成");
                    return false;
                }

                switch (kind)
                {
                    case 0: WaterHelper.AddPureWater(item, amount); break;
                    case 1: WaterHelper.FillWithHighQualityWater(item); break;
                    case 2: WaterHelper.FillWithBaseWater(item); break;
                    case 3: WaterHelper.FillWithGhostwater(item); break;
                    case 4: WaterHelper.FillWithRustWaterLowEnd(item); break;
                    default: WaterHelper.AddGutterflow(item, amount); break;
                }

                // 灌完让游戏自己把水的属性/标签对齐（纯冰那几个模组改完水也是这么刷的）
                try { WaterFeatureHelper.InitWaterFeature(item, false); } catch { }
                try { WaterFeatureHelper.UpdateWaterFeature(item, false); } catch { }

                int after = -1, afterIdx = -1;
                try { after = WaterHelper.GetWaterPurity(item); } catch { }
                try { afterIdx = WaterFeatureHelper.GetPurityArrayIndex(after); } catch { }

                trace = "水：按「" + WaterGradeName(kind) + "」重灌　真纯度 " + before + "→" + after
                    + "　档位 " + beforeIdx + "→" + afterIdx;

                Core.Debug("[API] 灌水：" + WaterGradeName(kind) + "　纯度 " + before + "→" + after
                    + "　档位 " + beforeIdx + "→" + afterIdx + "　" + WaterCatalog());

                return afterIdx == kind || after != before;
            }
            catch (Exception ex)
            {
                trace = "水：报错 " + ex.Message;
                Core.Debug("[API] 灌水失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 药这类「化学品」的真纯度由 ChemicalProductHelper 单独管（真、假两个条件分开存）。
        /// 打标器只动假的那半边，所以这里用游戏自己的接口把真、假都按标签写一遍。
        /// </summary>
        private static bool TurnChemicalReal(GameItem item, ItemFeature f, out string trace)
        {
            trace = "药：没找到能改的纯度";
            try
            {
                ItemCondition label = null;
                try { label = f.GetCurrentItemCondition(); } catch { }
                if (label == null) { try { label = f.fakeCondition; } catch { } }
                if (label == null) return false;

                double before = -1;
                try { before = ChemicalProductHelper.GetPurity(item); } catch { }

                ItemCondition mine = CloneCondition(label);
                if (mine == null) { trace = "药：条件抄不出来"; return false; }

                ChemicalProductHelper.SetPurityFeature(item, mine, mine);   // 真、假都按标签写

                double after = -1;
                try { after = ChemicalProductHelper.GetPurity(item); } catch { }

                string show = null;
                try { show = label.display; } catch { }
                trace = "药：真纯度 " + before.ToString("0.##") + "→" + after.ToString("0.##")
                    + "（标签「" + (show ?? "-") + "」）";

                Core.Debug("[API] 药转真：" + (show ?? "-") + "　纯度 " + before.ToString("0.##") + "→" + after.ToString("0.##"));

                return after != before;
            }
            catch (Exception ex)
            {
                trace = "药：报错 " + ex.Message;
                Core.Debug("[API] 药转真失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>两处拿到的属性是不是同一条（比原生指针，别比包装对象）。</summary>
        private static bool SameFeature(ItemFeature a, ItemFeature b)
        {
            if (a == null || b == null) return false;
            try { return ReferenceEquals(a, b); } catch { return false; }
        }

        /// <summary>这件物品上挂着的属性类别（认不出该走哪条路时摆出来看）。</summary>
        private static string CatList(GameItem item)
        {
            string s = "";
            try
            {
                var fs = item.itemFeatures;
                if (fs != null)
                {
                    for (int i = 0; i < fs.Count; i++)
                    {
                        if (fs[i] == null) continue;
                        string c = null;
                        try { c = fs[i].category; } catch { }
                        s += (c ?? "-") + " ";
                    }
                }
            }
            catch { }
            return s == "" ? "（没有属性）" : s;
        }

        /// <summary>把一条条件抄成新对象（照纯冰模组的做法，传引用游戏有不认的时候）。</summary>
        private static ItemCondition CloneCondition(ItemCondition label)
        {
            try
            {
                return new ItemCondition
                {
                    display = label.display,
                    identifier = label.identifier,
                    modValue = label.modValue,
                    modValueDisplay = label.modValueDisplay,
                    isTransformative = label.isTransformative,
                    category = label.category,
                    hiddenAsPublic = label.hiddenAsPublic,
                    customValue = label.customValue,
                    onActivateActionId = label.onActivateActionId,
                    compareValue = label.compareValue,
                };
            }
            catch { return null; }
        }

        /// <summary>
        /// 一条属性「转真」：把「这件东西实际是什么样」整组换成「现在这个标签写的那个」。
        ///
        /// 游戏里每条属性都把真假分成三对存：
        ///   条件　fakeCondition / realCondition
        ///   说明　publicDisplay / actualDisplay
        ///   数值　preExposeValueModifier / valueModifier（还有 usePreExposeValue 这个开关）
        /// 打标器只动每一对的**前一半**（标签那一侧），所以光改条件，物品的数值和说明
        /// 还是原来那套 —— 看着就是「只换了标签」。这里三对一起拨过去，最后关掉
        /// 「未曝光前拿假数值顶替」的开关。
        /// </summary>
        private static bool TurnReal(ItemFeature f)
        {
            try
            {
                ItemCondition label = null;
                try { label = f.GetCurrentItemCondition(); } catch { }
                if (label == null) { try { label = f.fakeCondition; } catch { } }
                if (label == null) return false;

                string wantId = null, wantShow = null;
                int wantVal = 0;
                try { wantId = label.identifier; } catch { }
                try { wantShow = label.display; } catch { }
                try { wantVal = label.modValue; } catch { }

                string wasId = RealId(f), wasShow = RealShow(f);
                int wasVal = RealVal(f);

                // 条件得抄成新对象再传（照着纯冰模组的做法）：把标签上那一个引用直接传两遍，
                // 游戏有不认的时候
                ItemCondition mine = CloneCondition(label);
                if (mine == null) return false;

                f.SetConditions(mine, mine);                // 真条件 ← 标签

                if (wantVal != 0 && wantVal != wasVal)
                {
                    f.SetValueModifier(wantVal);            // 真数值 ← 标签自带的数值
                    f.SetPreExposedValueMod(wantVal, false);
                }
                if (!string.IsNullOrEmpty(wantShow) && wantShow != wasShow)
                {
                    f.SetActualDisplay(wantShow);           // 实际说明 ← 标签的说明
                    f.SetPublicDisplay(wantShow);
                }

                string nowId = RealId(f), nowShow = RealShow(f);
                int nowVal = RealVal(f);

                try
                {
                    Core.Debug("[API] 标签转真：" + (f.category ?? "?")
                        + " 真条件 " + (wasId ?? "空") + " → " + (nowId ?? "空")
                        + "，真数值 " + wasVal + " → " + nowVal
                        + "，实际说明 " + Brief(wasShow) + " → " + Brief(nowShow));
                }
                catch { }

                if (!string.IsNullOrEmpty(wantId)) return nowId == wantId;
                return nowId != wasId || nowVal != wasVal || nowShow != wasShow;
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 标签转真失败：" + ex.Message);
                return false;
            }
        }

        private static string RealId(ItemFeature f)
        {
            try { ItemCondition c = f.realCondition; return c == null ? null : c.identifier; } catch { return null; }
        }

        private static string RealShow(ItemFeature f)
        {
            try { return f.GetActualDisplay(); } catch { return null; }
        }

        private static int RealVal(ItemFeature f)
        {
            try { return f.GetActualValueModifier(); } catch { return 0; }
        }

        /// <summary>日志里截一段说明文字，别把整段塞进去。</summary>
        private static string Brief(string s)
        {
            if (string.IsNullOrEmpty(s)) return "空";
            return s.Length <= 18 ? s : s.Substring(0, 18);
        }

        // ── 特殊功能：强行举枪（只有动画，不开真枪）───────────────────

        private static float _armUntil;        // 手臂要亮到哪一刻（unscaledTime）
        private static bool _armLogged;

        /// <summary>手臂是不是还举着，顺带给出还剩几秒。</summary>
        public static bool ArmUp(out float left)
        {
            left = _armUntil - Time.unscaledTime;
            return _armUntil > 0f && left > 0f;
        }

        /// <summary>
        /// 摆出举枪姿势、给一枪后坐力和枪口火光 —— 纯演出。
        /// 刻意不碰 isShootingMode、不调 Shoot()/CanShoot()，所以不会真打死谁，也不会招来警官。
        ///
        /// 手法照搬 emolunpan 那个模组：非射击模式下游戏每帧都会把手臂藏起来，
        /// 所以点亮之后得靠 TickArmShow 每帧再按一次。
        /// </summary>
        public static string ArmShow(float seconds)
        {
            try
            {
                StoreUIManager ui = null;
                try { ui = StoreUIManager.Instance; } catch { }
                if (ui == null) return "还没进存档，没有可举的手臂。";

                GameObject arm = null;
                try { arm = ui.arm; } catch { }
                if (arm == null) return "取不到 StoreUIManager.arm。";

                Ui.SetActive(arm, true);
                _armUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);

                Arm comp = null;
                try { comp = arm.GetComponent<Arm>(); } catch { }
                if (comp == null) return "手臂对象上没有 Arm 组件。";

                try { comp.TriggerRecoil(); }
                catch (Exception ex) { Core.Debug("[API] 后坐力没播出来：" + ex.Message); }
                try { comp.TriggerMuzzleFlash(); }
                catch (Exception ex) { Core.Debug("[API] 枪口火光没播出来：" + ex.Message); }

                if (!_armLogged)
                {
                    _armLogged = true;
                    Core.Log.Msg("[API] 举枪手臂链 " + ArmChain());
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>收枪：不再往回点亮，并且主动藏一次。</summary>
        public static void ArmOff()
        {
            _armUntil = 0f;
            try
            {
                StoreUIManager ui = StoreUIManager.Instance;
                GameObject arm = ui != null ? ui.arm : null;
                if (arm != null) Ui.SetActive(arm, false);
            }
            catch { }
        }

        /// <summary>
        /// 非射击模式下游戏会自己把手臂藏起来，所以演出窗口内每帧再点亮一次。
        /// 只改 arm 自身的激活状态，不动任何射击相关字段。
        /// </summary>
        public static void TickArmShow()
        {
            if (_armUntil <= 0f) return;
            try
            {
                if (Time.unscaledTime > _armUntil) { _armUntil = 0f; return; }

                StoreUIManager ui = StoreUIManager.Instance;
                GameObject arm = ui != null ? ui.arm : null;
                if (arm == null) { _armUntil = 0f; return; }

                if (!arm.activeSelf) Ui.SetActive(arm, true);
            }
            catch { _armUntil = 0f; }
        }

        /// <summary>
        /// 手臂自己以及上面每一层的激活状态。手臂挂在射击相机下面，
        /// 祖先里只要有一层没激活，activeInHierarchy 就是 false，点了也看不见 —— 出问题时看这行日志。
        /// </summary>
        private static string ArmChain()
        {
            try
            {
                StoreUIManager ui = StoreUIManager.Instance;
                GameObject arm = ui != null ? ui.arm : null;
                if (arm == null) return "(没有手臂对象)";

                string s = "";
                Transform node = arm.transform;
                for (int depth = 0; node != null && depth < 6; depth++)
                {
                    if (depth > 0) s += " < ";
                    s += node.name + "[" + (node.gameObject.activeSelf ? "on" : "off") + "/"
                        + (node.gameObject.activeInHierarchy ? "vis" : "hid") + "]";
                    node = node.parent;
                }
                return s;
            }
            catch (Exception ex)
            {
                return "(读不到：" + ex.Message + ")";
            }
        }

        // ── 特殊功能：强制击杀（举枪 + 走游戏自己的开枪结算）───────────

        /// <summary>柜台前这位顾客叫什么。没人 / 读不到就返回 null。</summary>
        public static string ClientAtCounter()
        {
            try
            {
                PlayerStore s = Store;
                if (s == null || !s.isClientArrived) return null;
                StoreClientInstance inst = s.currentClientInstance;
                StoreClient bp = inst != null ? inst.GetClientBlueprint() : null;
                if (bp == null) return null;
                string n = null;
                try { n = bp.displayName; } catch { }
                if (string.IsNullOrEmpty(n)) { try { n = bp.identifier; } catch { } }
                return n;
            }
            catch { return null; }
        }

        /// <summary>
        /// 强制击杀：先摆个举枪姿势放一枪（后坐力 + 枪口火光），
        /// 再走游戏自己的开枪流程（EnterShootingMode → Shoot），
        /// 中枪动画和倒地收尾都由游戏自己演，我们不碰血量也不硬删人。
        /// 返回 null 表示打出去了，否则是失败原因。
        /// </summary>
        public static string ForceKill(out string who)
        {
            who = ClientAtCounter();

            PlayerStore s = Store;
            if (s == null) return "还没进存档。";

            StoreUIManager ui = null;
            try { ui = StoreUIManager.Instance; } catch { }
            if (ui == null) return "取不到店铺界面。";

            if (s.currentNegociatedItem != null) return "正在讨价还价，先把这笔谈完。";
            if (s.isClientBeingArrested) return "正在处理抓捕，等这段演完。";
            if (s.isHandlingShootout) return "已经打起来了，等这轮演完。";
            if (!s.isClientArrived) return "柜台前没人，等顾客进来再动手。";
            if (string.IsNullOrEmpty(who)) return "取不到柜台前这位的名字。";

            StoreClient bp = null;
            try { bp = s.currentClientInstance != null ? s.currentClientInstance.GetClientBlueprint() : null; } catch { }
            if (bp != null)
            {
                bool no = false;
                try { no = bp.nonShootable; } catch { }
                if (no) return "这位在游戏里标了「开枪打不得」，换个人吧。";
            }

            bool can = false, mode = false;
            try { can = ui.CanShoot(); } catch { }
            try { mode = ui.isShootingMode; } catch { }

            // 一、举枪演出
            string armErr = ArmShow(2.5f);

            // 二、走游戏自己的开枪
            bool shot = false;
            try
            {
                if (!mode && can) ui.EnterShootingMode();
                ui.Shoot();
                shot = ui.isShot;
            }
            catch (Exception ex) { Core.Debug("[API] 开枪结算失败：" + ex.Message); }

            // 三、游戏没把这一枪算进去，就把「已击中」的旗子自己立起来 ——
            //     中枪 / 倒地的收尾照样由游戏的 Update 去走。
            if (!shot)
            {
                try
                {
                    ui.shotClientInstance = s.currentClientInstance;
                    ui.isShot = true;
                    shot = ui.isShot;
                }
                catch (Exception ex) { Core.Debug("[API] 标记命中失败：" + ex.Message); }
            }

            Core.Log.Msg("[API] 强制击杀 " + who
                + "：举枪 " + (armErr == null ? "成" : "败(" + armErr + ")")
                + "　射击模式 " + (mode ? "本来就开着" : (can ? "开起来了" : "打不开"))
                + "　命中旗 " + (shot ? "立起来了" : "没立起来"));

            if (!shot) return "枪响了，但游戏没把这一枪算成命中。看日志里的「强制击杀」那行。";
            return null;
        }

        /// <summary>把某个库存里现在装着的物品收成一个表。</summary>
        private static void CollectInventory(GameInventory inv,
            List<GameItem> into)
        {
            if (inv == null) return;
            try
            {
                List<GameItem> kids = inv.childItems;
                if (kids == null) return;
                for (int i = 0; i < kids.Count; i++)
                {
                    GameItem it = null;
                    try { it = kids[i]; } catch { }
                    if (it != null) into.Add(it);
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 枚举库存失败：" + ex.Message);
            }
        }

        /// <summary>库存里「不是玩家的」物品（顾客自己带来的那部分）。</summary>
        private static List<GameItem> CollectUnowned(GameInventory inv)
        {
            List<GameItem> list =
                new List<GameItem>();
            if (inv == null) return list;
            try
            {
                List<GameItem> kids = inv.childItems;
                if (kids == null) return list;
                for (int i = 0; i < kids.Count; i++)
                {
                    GameItem it = null;
                    try { it = kids[i]; } catch { }
                    if (it == null) continue;
                    bool owned = false;
                    try { owned = GeneralHelper.IsItemOwned(it); } catch { }
                    if (!owned) list.Add(it);
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 枚举库存失败：" + ex.Message);
            }
            return list;
        }

        private static int CountUnowned(GameInventory inv)
        {
            if (inv == null) return 0;
            try
            {
                List<GameItem> kids = inv.childItems;
                if (kids == null) return 0;
                int n = 0;
                for (int i = 0; i < kids.Count; i++)
                {
                    GameItem it = null;
                    try { it = kids[i]; } catch { }
                    if (it == null) continue;
                    bool owned = false;
                    try { owned = GeneralHelper.IsItemOwned(it); } catch { }
                    if (!owned) n++;
                }
                return n;
            }
            catch { return 0; }
        }

        /// <summary>把一批物品从库存里摘出来（失败就整体还原）。</summary>
        private static bool DetachItems(GameInventory inv,
            List<GameItem> items)
        {
            if (inv == null || items == null) return false;
            bool backup = false;
            try { backup = inv.overrideLockRemove; } catch { }
            try
            {
                try { inv.overrideLockRemove = true; } catch { }
                int ok = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem it = items[i];
                    if (it == null) continue;
                    bool done = false;
                    try { done = inv.Expel(it); } catch (Exception ex) { Core.Debug("[API] 摘物品失败：" + ex.Message); }
                    if (done) ok++;
                }
                return ok == items.Count;
            }
            finally
            {
                try { inv.overrideLockRemove = backup; } catch { }
            }
        }

        private static void RestoreItems(GameInventory inv,
            List<GameItem> items)
        {
            if (inv == null || items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                GameItem it = items[i];
                if (it == null) continue;
                bool free = false;
                try { free = it.parentInventory == null; } catch { }
                if (!free) continue;
                try { inv.UncheckedAccept(it); } catch { }
            }
        }

        /// <summary>顾客资料里那些「已经聊过、介绍过」的状态，重掷后要接着用。</summary>
        private static void PreserveClientState(StoreClient from, StoreClient to)
        {
            if (from == null || to == null || from == to) return;
            try { to.displayName = from.displayName; } catch { }
            try { to.spriteName = from.spriteName; } catch { }
            try { to.realName = from.realName; } catch { }
            try { to.isIntroduced = from.isIntroduced; } catch { }
            try { to.isMainDialogueStarted = from.isMainDialogueStarted; } catch { }
            try { to.acceptDealDialoguePlayed = from.acceptDealDialoguePlayed; } catch { }
            try { to.isAugScanned = from.isAugScanned; } catch { }
        }

        /// <summary>把顾客最后一段对话的收尾动作跑一遍（有些顾客的货是在那一步才摆上柜台的）。</summary>
        private static bool InvokeTerminalDialogueAction(StoreClient client)
        {
            try
            {
                Dialogue d = client != null ? client.mainDialogue : null;
                int guard = 0;
                while (d != null && guard < 64)
                {
                    if (d.nextDialogue == null)
                    {
                        if (d.endAction == null) return false;
                        d.endAction.Invoke();
                        d.isEndActionExecuted = true;
                        return true;
                    }
                    d = d.nextDialogue;
                    guard++;
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[API] 触发对话收尾失败：" + ex.Message);
            }
            return false;
        }

        /// <summary>
        /// 刷新当前顾客带来的货：把现在这位顾客的货全部撤掉，按同一个「顾客模板」
        /// 重新掷一份（人还是那个人，带的东西换一批）。
        /// 手法照 StockReroll：蓝图重建 → 保留对话状态 → 换实例 → 撤旧货 → 刷柜台。
        /// 返回 null 表示成功，否则是失败原因。
        /// </summary>
        public static string RerollCustomerStock(out int oldCount, out int newCount)
        {
            oldCount = 0;
            newCount = 0;

            PlayerStore s = Store;
            EmporiumEntry emp = Emporium;
            if (s == null) return "还没进存档。";
            if (emp == null || emp.frontInvinvElement == null) return "店铺还没就绪，等柜台加载出来再刷。";

            try
            {
                StoreUIManager ui = null;
                try { ui = StoreUIManager.Instance; } catch { }

                if (s.currentNegociatedItem != null) return "正在讨价还价，先把这笔谈完。";
                if (s.isClientBeingArrested) return "正在处理抓捕，等这段演完。";
                if (s.isHandlingShootout || (ui != null && ui.isShootingMode)) return "正在枪战，等打完了再刷。";
                if (!s.isClientArrived) return "现在没有顾客在店里。";

                StoreClientInstance inst = s.currentClientInstance;
                StoreClient bp = inst != null ? inst.GetClientBlueprint() : null;
                if (inst == null || bp == null) return "取不到当前顾客的资料。";
                if (bp.isBusinessComplete || bp.reported) return "这位顾客的生意已经做完了，刷不了。";

                string ident = null;
                try { ident = bp.identifier; } catch { }
                if (string.IsNullOrEmpty(ident)) return "这位顾客没有 ID，没法按模板重掷。";

                GameGridInventory front = emp.frontInvinvElement;
                List<GameItem> oldFront = CollectUnowned(front);
                List<GameItem> oldGrid = CollectUnowned(ReadPrivate<GameGridInventory>(inst, "gridInv"));
                oldCount = oldFront.Count + oldGrid.Count;

                StoreClient fresh = null;
                try { fresh = StoreClientListDict.CreateStoreClient(ident); }
                catch (Exception ex) { Core.Debug("[API] 重建顾客模板失败：" + ex.Message); }
                if (fresh == null) return "按模板重建顾客失败（" + ident + "），这个顾客可能没有可重掷的数据。";

                PreserveClientState(bp, fresh);
                try { emp.CloseItemWindowUIWindow(); } catch { }

                if (!DetachItems(front, oldFront))
                {
                    RestoreItems(front, oldFront);
                    return "旧货摘不下来，已经还原，没动你的店。";
                }

                StoreClientInstance next = null;
                try { next = StoreClientInstance.CreateClientInstance(fresh); }
                catch (Exception ex) { Core.Debug("[API] 新建顾客实例失败：" + ex.Message); }
                if (next == null || ReadPrivate<GameGridInventory>(next, "gridInv") == null)
                {
                    RestoreItems(front, oldFront);
                    return "新顾客实例建不出来，旧货已经还原。";
                }

                s.currentClientInstance = next;
                try
                {
                    if (fresh.OnArrival != null) fresh.OnArrival.Invoke();
                    // 有些顾客的货是挂在对话收尾动作里的，到场之后柜台还是空的就得补这一步
                    if (CountUnowned(front) == 0 && CountUnowned(ReadPrivate<GameGridInventory>(next, "gridInv")) == 0)
                        InvokeTerminalDialogueAction(fresh);
                }
                catch (Exception ex)
                {
                    s.currentClientInstance = inst;
                    RestoreItems(front, oldFront);
                    return "新顾客的到场流程出错，已经还原：" + ex.Message;
                }

                newCount = CountUnowned(front) + CountUnowned(ReadPrivate<GameGridInventory>(next, "gridInv"));
                if (newCount <= 0)
                {
                    s.currentClientInstance = inst;
                    RestoreItems(front, oldFront);
                    return "重掷出来的货是空的，已经还原成原来那批。";
                }

                try { GeneralHelper.DestroyGameItems(oldFront); } catch (Exception ex) { Core.Debug("[API] 销毁旧货失败：" + ex.Message); }
                try { GeneralHelper.DestroyGameItems(oldGrid); } catch (Exception ex) { Core.Debug("[API] 销毁旧货失败：" + ex.Message); }
                s.RefreshCounterItem();
                try { emp.Validate(true); } catch { }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// 清空店里所有物品：柜台、后仓、摆在柜台前场的那三处一起收，
        /// 外加兜底把存档里认在你名下的东西也扫一遍。返回 null 表示没出错。
        /// </summary>
        public static string ClearAllItems(out int destroyed)
        {
            destroyed = 0;
            if (Store == null) return "还没进存档。";
            EmporiumEntry emp = Emporium;
            if (emp == null) return "店铺还没就绪。";

            List<GameItem> all = StoreItems();

            int fail = 0;
            for (int i = 0; i < all.Count; i++)
            {
                GameItem it = all[i];
                if (it == null) continue;
                if (DestroyItem(it) == null) destroyed++;
                else fail++;
            }

            TryRefreshCounter();
            try { emp.Validate(true); } catch { }
            return fail > 0 ? "有 " + fail + " 件没能销毁。" : null;
        }

        // ── 鼠标屏蔽（借游戏的 OverlayHandler 屏蔽栈）──────────────────

        private static bool _mouseBlocked;

        public static void BlockWorldMouse(bool on)
        {
            try
            {
                if (on == _mouseBlocked) return;
                OverlayHandler handler = OverlayHandler.current;
                if (handler == null)
                {
                    _mouseBlocked = false;
                    return;
                }
                if (on)
                {
                    handler.BlockMouse();
                    _mouseBlocked = true;
                }
                else
                {
                    _mouseBlocked = false;
                    // 玩家可能按过游戏的强制解除键把栈清空了，别减成负数
                    if (ReadPrivate<int>(handler, "mouseBlockStacks") > 0) handler.UnblockMouse();
                }
            }
            catch (Exception ex)
            {
                _mouseBlocked = false;
                Core.Debug("[API] 鼠标屏蔽切换失败：" + ex.Message);
            }
        }

        public static void ReleaseWorldMouse()
        {
            _mouseBlocked = false;
            try
            {
                OverlayHandler handler = OverlayHandler.current;
                if (handler != null && ReadPrivate<int>(handler, "mouseBlockStacks") > 0) handler.UnblockMouse();
            }
            catch { }
        }
    }
}
