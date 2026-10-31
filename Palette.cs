using System;
using UnityEngine;

namespace DebugConsole
{
    /// <summary>
    /// 界面配色表。深色（默认，深蓝夜）/ 亮色（浅灰蓝日）两套。
    /// 所有颜色都从这里取，不在各处写死，方便统一调。
    ///
    /// 颜色是建界面时烙进 Image/Text 里的，所以切风格必须把整块界面拆了重建
    /// （见 ConsoleUI.Rebuild）。
    /// </summary>
    internal static class Palette
    {
        public enum Theme { Dark, Light }

        private static Theme _theme = Theme.Dark;
        public static bool IsLight { get { return _theme == Theme.Light; } }
        public static Theme Current { get { return _theme; } }

        // ── 底色与面板 ────────────────────────────────────────────────
        public static Color PageBg;    // 面板底
        public static Color CardBg;    // 纸面（顶栏、侧栏、日志区）
        public static Color SlotBg;    // 卡内嵌槽（日志区、命令条）
        public static Color CardRim;   // 描边
        public static Color Divider;   // 分隔线

        // ── 文字 ──────────────────────────────────────────────────────
        public static Color Title;     // 标题
        public static Color Body;      // 正文
        public static Color Sub;       // 次级
        public static Color Muted;     // 弱化/说明

        // ── 语义色 ────────────────────────────────────────────────────
        public static Color Accent;    // 主色（青蓝）：标题点缀、选中态
        public static Color Ok;        // 成功（绿）
        public static Color Warn;      // 警告（琥珀）
        public static Color Err;       // 失败（红）
        public static Color Value;     // 数值高亮（金）
        public static Color Key;       // 命令名 / 关键词（青）

        // ── 按钮底 ────────────────────────────────────────────────────
        public static Color BtnIdle;   // 普通
        public static Color BtnRun;    // 主操作（执行）
        public static Color BtnDanger; // 关闭 / 清除
        public static Color NavOn;     // 侧栏选中

        static Palette()
        {
            Apply(Theme.Dark);
        }

        /// <summary>切风格。切完界面颜色不会自己变，得重建面板。</summary>
        public static void SetTheme(Theme t)
        {
            Apply(t);
        }

        private static void Apply(Theme t)
        {
            _theme = t;
            if (t == Theme.Light)
            {
                PageBg = Hex("DDE6F0");
                CardBg = Hex("FFFFFF");
                SlotBg = Hex("EFF4F9");
                CardRim = Hex("A9BFD3");
                Divider = Hex("C2D3E0");

                Title = Hex("17293A");
                Body = Hex("2C3E50");
                Sub = Hex("44586C");
                Muted = Hex("6B7F94");

                Accent = Hex("2C7A9E");
                Ok = Hex("1E8F5F");
                Warn = Hex("A87514");
                Err = Hex("B03A16");
                Value = Hex("A87514");
                Key = Hex("2C7A9E");

                BtnIdle = Hex("D9E3EE");
                BtnRun = Hex("9CC6E4");
                BtnDanger = Hex("E8A392");
                NavOn = Hex("B6D5EA");
            }
            else
            {
                PageBg = Hex("1B2D3F");
                CardBg = Hex("1B2836");
                SlotBg = Hex("162230");
                CardRim = Hex("4F7391");
                Divider = Hex("59809E");

                Title = Hex("F5FAFF");
                Body = Hex("DEE8F5");
                Sub = Hex("C7D9ED");
                Muted = Hex("9EB3CC");

                Accent = Hex("9EC7DE");
                Ok = Hex("45C08A");
                Warn = Hex("FFC24A");
                Err = Hex("E5533D");
                Value = Hex("FFC24A");
                Key = Hex("9EC7DE");

                BtnIdle = Hex("25384A");
                BtnRun = Hex("2E6C93");
                BtnDanger = Hex("B0431C");
                NavOn = Hex("2E5E80");
            }
        }

        /// <summary>#RRGGBB → Color。</summary>
        public static Color Hex(string hex)
        {
            int r = 0, g = 0, b = 0;
            if (!string.IsNullOrEmpty(hex) && hex.Length >= 6)
            {
                try
                {
                    r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    b = Convert.ToInt32(hex.Substring(4, 2), 16);
                }
                catch { }
            }
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        /// <summary>Color → RichText 用的 #RRGGBB。富文本里写死的色值统一从 Palette 反推。</summary>
        public static string HexOf(Color c)
        {
            int r = (int)(c.r * 255f + 0.5f);
            int g = (int)(c.g * 255f + 0.5f);
            int b = (int)(c.b * 255f + 0.5f);
            if (r < 0) r = 0; if (r > 255) r = 255;
            if (g < 0) g = 0; if (g > 255) g = 255;
            if (b < 0) b = 0; if (b > 255) b = 255;
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        /// <summary>
        /// 富文本里写死的深色主题色值 → 当前主题色值。
        /// 命令输出里大量用 &lt;color=#FFC24A&gt; 这类字面量标关键词，这些值是深色主题的，
        /// 切到亮色后压在白纸面上几乎看不见，所以写进 TMP 之前统一过一遍。
        /// 深色主题原样返回，零开销。
        /// </summary>
        public static string Rt(string s)
        {
            if (string.IsNullOrEmpty(s) || _theme == Theme.Dark) return s;
            if (s.IndexOf('#') < 0) return s;
            return s
                .Replace("#FFC24A", HexOf(Value))
                .Replace("#9EC7DE", HexOf(Key))
                .Replace("#9EB3CC", HexOf(Muted))
                .Replace("#45C08A", HexOf(Ok))
                .Replace("#E5533D", HexOf(Err));
        }

        /// <summary>改透明度。</summary>
        public static Color A(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        /// <summary>提亮（t&gt;0）或压暗（t&lt;0）。</summary>
        public static Color Shift(Color c, float t)
        {
            if (t >= 0f)
            {
                return new Color(
                    c.r + (1f - c.r) * t,
                    c.g + (1f - c.g) * t,
                    c.b + (1f - c.b) * t,
                    c.a);
            }
            float k = 1f + t;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        // ── 富文本快捷标签（深色主题字面量，写进 TMP 前会被 Rt 换掉）──────
        public static string TagKey(string s) { return "<color=#9EC7DE>" + s + "</color>"; }
        public static string TagVal(string s) { return "<color=#FFC24A>" + s + "</color>"; }
        public static string TagOk(string s) { return "<color=#45C08A>" + s + "</color>"; }
        public static string TagErr(string s) { return "<color=#E5533D>" + s + "</color>"; }
        public static string TagMuted(string s) { return "<color=#9EB3CC>" + s + "</color>"; }
    }
}