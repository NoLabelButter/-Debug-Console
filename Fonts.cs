using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DebugConsole
{
    /// <summary>
    /// 中文 TMP 字体探测。手法照搬 PSPDA / StockMarket：
    /// 先在场景里找游戏正在用的字体，再从全部 TMP 字体资源里挑一个带中文的。
    /// </summary>
    internal static class Fonts
    {
        private static TMP_FontAsset _ui;
        private static TMP_FontAsset _game;
        private static bool _done;

        public static TMP_FontAsset Font()
        {
            if (!_done) Init();
            return _ui != null ? _ui : _game;
        }

        /// <summary>场景重载后旧字体资源可能已经销毁，下次用到时重新探一遍。</summary>
        public static void Reset()
        {
            _ui = null;
            _game = null;
            _done = false;
            Mark.Reset();
        }

        private static void Init()
        {
            _done = true;
            try
            {
                _game = FromSceneText();
                List<TMP_FontAsset> all = All();
                TMP_FontAsset pick = First(all, "notosanssc");
                if (pick == null) pick = First(all, "notosansjp");
                if (pick == null) pick = First(all, "notosans");
                if (pick == null)
                {
                    for (int i = 0; i < all.Count && pick == null; i++)
                    {
                        string n = Name(all[i]).ToLowerInvariant();
                        if (n.Contains("wdxl") || n.Contains("lubrifont")) continue;
                        if (HasCjk(all[i])) pick = all[i];
                    }
                }
                if (pick == null) pick = HasCjk(_game) ? _game : null;
                _ui = pick;

                Core.Log.Msg("[字体] 控制台字体 = " + (_ui != null ? Name(_ui) : "未找到")
                    + "，中文 " + (HasCjk(_ui) ? "正常" : "可能显示为方块")
                    + "（候选 " + all.Count + " 个）");
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[字体] 初始化失败：" + ex.Message);
            }
        }

        /// <summary>取场景里第一个有字体的 TMP 文本，它的字体就是游戏原字体。</summary>
        private static TMP_FontAsset FromSceneText()
        {
            try
            {
                UnityEngine.Object[] found = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
                if (found == null) return null;
                for (int i = 0; i < found.Length; i++)
                {
                    TextMeshProUGUI t = found[i] as TextMeshProUGUI;
                    if (t == null) continue;
                    TMP_FontAsset f = null;
                    try { f = t.font; } catch { }
                    if (f != null) return f;
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[字体] 找场景字体失败：" + ex.Message);
            }
            return null;
        }

        private static List<TMP_FontAsset> All()
        {
            List<TMP_FontAsset> list = new List<TMP_FontAsset>();
            try
            {
                UnityEngine.Object[] found = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (found == null) return list;
                for (int i = 0; i < found.Length; i++)
                {
                    TMP_FontAsset f = found[i] as TMP_FontAsset;
                    if (f != null) list.Add(f);
                }
            }
            catch (Exception ex)
            {
                Core.Debug("[字体] 枚举字体失败：" + ex.Message);
            }
            return list;
        }

        private static TMP_FontAsset First(List<TMP_FontAsset> all, string key)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (Name(all[i]).ToLowerInvariant().Contains(key) && HasCjk(all[i])) return all[i];
            }
            return null;
        }

        private static string Name(TMP_FontAsset f)
        {
            try { return f == null ? "" : (f.name ?? ""); }
            catch { return ""; }
        }

        /// <summary>
        /// 当前面板字体里有没有这个字形。日志前缀的符号全靠它筛：字体缺哪个字就不画哪个，
        /// 宁可少一个符号，也不能在日志里排出一列方块。
        ///
        /// tryAddCharacter 必须传 true：TMP 图集是「动态」的，字库里有的字要等真正用到
        /// 才会被烘进图集，只查现有图集会把本来能显示的字误判成缺字。
        /// </summary>
        public static bool Has(char c)
        {
            TMP_FontAsset f = Font();
            if (f == null) return false;
            try { return f.HasCharacter(c, true, true); }
            catch { try { return f.HasCharacter(c); } catch { return false; } }
        }

        private static bool HasCjk(TMP_FontAsset f)
        {
            if (f == null) return false;
            // 拿几个最常用的汉字探一探，全中才算「带中文」
            char[] probe = { '调', '试', '物', '品', '金', '钱' };
            for (int i = 0; i < probe.Length; i++)
            {
                bool ok = false;
                try { ok = f.HasCharacter(probe[i], true, true); }
                catch { try { ok = f.HasCharacter(probe[i]); } catch { ok = false; } }
                if (!ok) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 日志行的前缀符号。有些符号不是所有字体都有，先问字体，缺了就退到 ASCII。
    ///
    /// 探字体的时机有讲究：模组刚加载时场景里可能一个 TMP 文本都没有，那时候探必然落空。
    /// 所以这里做成「延迟定格」—— 在真正拿到字体之前，每次访问都重试一遍，
    /// 免得开场那几条日志把整套符号永久锁死成 ASCII。
    /// </summary>
    internal static class Mark
    {
        private static bool _done;
        private static string _in = ">";
        private static string _ok = "+";
        private static string _warn = "!";
        private static string _err = "x";
        private static string _dot = "-";

        public static string In { get { Ensure(); return _in; } }     // 玩家输入回显
        public static string Ok { get { Ensure(); return _ok; } }     // 成功
        public static string Warn { get { Ensure(); return _warn; } } // 警告
        public static string Err { get { Ensure(); return _err; } }   // 失败
        public static string Dot { get { Ensure(); return _dot; } }   // 普通输出

        private static void Ensure()
        {
            if (_done || Fonts.Font() == null) return;
            _done = true;
            _in = Pick("›", ">");
            _ok = Pick("✓", "+");
            _warn = "!";
            _err = Pick("×", "x");
            _dot = Pick("·", "-");
        }

        public static void Reset() { _done = false; }

        private static string Pick(string want, string fallback)
        {
            try { return !string.IsNullOrEmpty(want) && Fonts.Has(want[0]) ? want : fallback; }
            catch { return fallback; }
        }
    }
}
