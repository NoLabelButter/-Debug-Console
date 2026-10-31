using System;
using System.Collections.Generic;

namespace DebugConsole
{
    /// <summary>命令的参数包。带一堆取值助手，免得每条命令自己写边界判断。</summary>
    internal sealed class CmdArgs
    {
        private readonly string[] _a;

        public CmdArgs(string[] a)
        {
            _a = a ?? new string[0];
        }

        public int Count { get { return _a.Length; } }
        public string[] Raw { get { return _a; } }
        public bool Has(int i) { return i >= 0 && i < _a.Length; }
        public string At(int i) { return Has(i) ? _a[i] : ""; }
        public string Lower(int i) { return At(i).ToLowerInvariant(); }

        public string Str(int i, string def)
        {
            return Has(i) && _a[i].Length > 0 ? _a[i] : def;
        }

        public int Int(int i, int def)
        {
            int v;
            if (Has(i) && int.TryParse(_a[i], out v)) return v;
            return def;
        }

        public long Long(int i, long def)
        {
            long v;
            if (Has(i) && long.TryParse(_a[i], out v)) return v;
            return def;
        }

        public float Float(int i, float def)
        {
            float v;
            if (Has(i) && float.TryParse(_a[i], out v)) return v;
            return def;
        }

        public bool Bool(int i, bool def)
        {
            if (!Has(i)) return def;
            string s = Lower(i);
            if (s == "1" || s == "on" || s == "true" || s == "yes" || s == "开") return true;
            if (s == "0" || s == "off" || s == "false" || s == "no" || s == "关") return false;
            return def;
        }

        /// <summary>从 from 起到结尾的所有参数，用空格拼回来（写日志、改名这类用）。</summary>
        public string Rest(int from)
        {
            if (from >= _a.Length) return "";
            if (from == _a.Length - 1) return _a[from];
            return string.Join(" ", _a, from, _a.Length - from);
        }
    }

    /// <summary>卡片上的一个按钮。</summary>
    internal sealed class CardAct
    {
        public string Label;   // 按钮文字，如「+1 万」
        public string Cmd;     // 点下去执行的完整命令行，如 "money add 10000"
        public int Tone;       // 0 普通 / 1 主操作（蓝）/ 2 危险（红）

        public CardAct(string label, string cmd, int tone)
        {
            Label = label; Cmd = cmd; Tone = tone;
        }
    }

    /// <summary>卡片上的输入行：填一段内容拼进命令行。</summary>
    internal sealed class CardInput
    {
        public string Hint;    // 输入框占位提示
        public string Prefix;  // 拼在输入内容前面，如 "spawn "
        public string Suffix;  // 拼在后面（可空）
        public string Button;  // 执行按钮文字

        public CardInput(string hint, string prefix, string suffix, string button)
        {
            Hint = hint; Prefix = prefix; Suffix = suffix; Button = button;
        }
    }

    /// <summary>卡片上的「下拉框 + 数字框」行：先选一个选项，再填内容，拼成命令跑。</summary>
    internal sealed class CardPick
    {
        public string Prefix;    // 头上前缀，如「区」
        public string[] Names;   // 选项显示的字，如 上层 / 下层 / 黑市
        public string[] Keys;    // 选项对应的值，如 ul / ll / bm（拼进命令）
        public int Sel;          // 默认选中的那个
        public string Hint;      // 输入框占位提示
        public string Cmd;       // 命令名，如 "rep "
        public string Button;    // 执行按钮文字

        // 选项是现取的那种（事件清单得进档才读得到）：每次展开下拉框都重新问一遍。
        // 返回的每一项：Key = 拼进命令的值，Value = 显示的字。
        public Func<List<KeyValuePair<string, string>>> Provider;
        public string EmptyHint;  // 一个都取不到时提示什么
        public bool NoInput;      // 只选不填：命令 = Cmd + 选中项的值

        public CardPick(string prefix, string[] names, string[] keys, int sel,
            string hint, string cmd, string button)
        {
            Prefix = prefix; Names = names; Keys = keys; Sel = sel;
            Hint = hint; Cmd = cmd; Button = button;
        }
    }

    /// <summary>
    /// 简单 / 混合模式里的一张操作卡片。
    ///
    /// 卡片不是另起一套东西：它就挂在对应的 Cmd 上，用命令行驱动。
    /// 所以「加一个新功能」永远只改一处 —— 命令和卡片一起写，
    /// 开发者模式（命令列表、Tab 补全、help）和简单模式（卡片）会自动同时出现。
    /// </summary>
    internal sealed class Card
    {
        public string Title;          // 卡片标题，如「金钱」
        public string Note;           // 标题下的一句小字（可空）
        public Func<string> Read;     // 实时读数；返回 null 显示「—」
        public CardAct[] Acts;        // 按钮行
        public CardInput Input;       // 输入行（和按钮行二选一）
        public CardPick Pick;         // 下拉框 + 输入行（优先于 Input）
        public string Group;          // 由 Cmds.Add 从所属命令带过来

        public Card(string title, string note, Func<string> read, CardAct[] acts)
        {
            Title = title; Note = note; Read = read; Acts = acts;
        }

        public Card(string title, string note, Func<string> read, CardInput input)
        {
            Title = title; Note = note; Read = read; Input = input;
        }

        public Card(string title, string note, Func<string> read, CardPick pick)
        {
            Title = title; Note = note; Read = read; Pick = pick;
        }
    }

    /// <summary>一条命令。</summary>
    internal sealed class Cmd
    {
        public string Name;          // 主名，例如 money
        public string[] Aliases;     // 别名，例如 cash
        public string Usage;         // 用法串
        public string Help;          // 说明
        public string Group;         // 分组 key
        public string Preset;        // 侧栏单击执行的完整命令；空表示「需要参数，只填入输入框」
        public string[] Hints;       // Tab 补全提示，按第 2、3… 个参数位给候选
        public Card[] Cards;         // 简单 / 混合模式里对应的操作卡片
        public Action<CmdArgs> Run;

        public bool Matches(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.Equals(Name, token, StringComparison.OrdinalIgnoreCase)) return true;
            if (Aliases == null) return false;
            for (int i = 0; i < Aliases.Length; i++)
            {
                if (string.Equals(Aliases[i], token, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    /// <summary>一条分组信息：侧栏的分类标签。</summary>
    internal sealed class CmdGroup
    {
        public string Key;
        public string Name;
        public string Hint;   // 分类一句话说明，侧栏鼠标悬停没做，就放在状态栏里
        public CmdGroup(string key, string name, string hint)
        {
            Key = key; Name = name; Hint = hint;
        }
    }

    /// <summary>
    /// 命令表：注册、解析、执行、补全。
    /// 命令一律登记在这里，控制台只跟这个表打交道，两边不互相知道细节。
    /// </summary>
    internal static class Cmds
    {
        public static readonly List<CmdGroup> Groups = new List<CmdGroup>
        {
            new CmdGroup("money",   "金钱",     "钱和租金，改多改少都在这儿"),
            new CmdGroup("save",    "存档",     "存档、跳天、电闸、硬核、夜晚报告"),
            new CmdGroup("item",    "物品",     "生成 / 查找 / 清理 / 删物品"),
            new CmdGroup("state",   "状态",     "声望、属性、进度开关"),
            new CmdGroup("special", "特殊功能", "强制击杀这类花活"),
            new CmdGroup("reflect", "反射调试", "直接读写游戏里的字段和方法"),
            new CmdGroup("sys",     "系统",     "帮助、清屏、主题、关闭"),
        };

        private static readonly List<Cmd> _all = new List<Cmd>();
        private static readonly List<Card> _cards = new List<Card>();

        public static List<Cmd> All { get { return _all; } }

        public static void Add(Cmd c)
        {
            if (c == null || string.IsNullOrEmpty(c.Name)) return;
            _all.Add(c);

            // 卡片跟命令一起登记：简单模式看到的东西全部从这儿来，
            // 不用另外维护一份「简单模式列表」，自然就不会两边对不上。
            if (c.Cards != null)
            {
                for (int i = 0; i < c.Cards.Length; i++)
                {
                    if (c.Cards[i] == null) continue;
                    c.Cards[i].Group = c.Group;
                    _cards.Add(c.Cards[i]);
                }
            }
        }

        /// <summary>某个分组下的操作卡片；key 传 "all" 就是全部。</summary>
        public static List<Card> Cards(string key)
        {
            List<Card> list = new List<Card>();
            for (int i = 0; i < _cards.Count; i++)
            {
                Card c = _cards[i];
                if (c == null) continue;
                if (key == "all" || c.Group == key) list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 界面上该露出来的分组。includeDev=false 时藏掉「反射调试」——
        /// 简单模式不该一进来就摆一堆字段和方法给人看。
        /// </summary>
        public static List<CmdGroup> VisibleGroups(bool includeDev)
        {
            List<CmdGroup> list = new List<CmdGroup>();
            for (int i = 0; i < Groups.Count; i++)
            {
                if (!includeDev && Groups[i].Key == "reflect") continue;
                list.Add(Groups[i]);
            }
            return list;
        }

        public static CmdGroup GroupOf(string key)
        {
            for (int i = 0; i < Groups.Count; i++)
            {
                if (Groups[i].Key == key) return Groups[i];
            }
            return Groups.Count > 0 ? Groups[0] : null;
        }

        public static string GroupName(string key)
        {
            CmdGroup g = GroupOf(key);
            return g != null ? g.Name : key;
        }

        public static Cmd Find(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Matches(token)) return _all[i];
            }
            return null;
        }

        public static List<Cmd> InGroup(string key)
        {
            List<Cmd> list = new List<Cmd>();
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Group == key) list.Add(_all[i]);
            }
            return list;
        }

        // ── 分词 ──────────────────────────────────────────────────────

        /// <summary>按空白切词，双引号内的空格不切（中文名里有空格也不怕）。</summary>
        public static string[] Tokenize(string line)
        {
            List<string> list = new List<string>();
            if (string.IsNullOrEmpty(line)) return list.ToArray();

            int i = 0;
            while (i < line.Length)
            {
                while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                if (i >= line.Length) break;

                bool quoted = line[i] == '"';
                if (quoted) i++;
                int start = i;
                string token;
                if (quoted)
                {
                    int end = line.IndexOf('"', i);
                    if (end < 0) { token = line.Substring(start); i = line.Length; }
                    else { token = line.Substring(start, end - start); i = end + 1; }
                }
                else
                {
                    while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                    token = line.Substring(start, i - start);
                }
                if (token.Length > 0) list.Add(token);
            }
            return list.ToArray();
        }

        /// <summary>执行一行命令。返回 false 表示不认识这条命令。</summary>
        public static bool Execute(string line)
        {
            string[] parts = Tokenize(line);
            if (parts.Length == 0) return true;

            Cmd c = Find(parts[0]);
            if (c == null)
            {
                Out.Err("不认识的命令：" + Palette.TagVal(parts[0])
                    + "　用 " + Palette.TagKey("help") + " 看全部命令。");
                return false;
            }

            string[] rest = new string[parts.Length - 1];
            Array.Copy(parts, 1, rest, 0, rest.Length);
            try
            {
                c.Run(new CmdArgs(rest));
            }
            catch (Exception ex)
            {
                Out.Err("命令执行中抛异常：" + ex.Message);
                Core.Log.Warning("[命令] " + c.Name + " 执行失败：" + ex);
            }
            return true;
        }

        // ── Tab 补全 ──────────────────────────────────────────────────

        /// <summary>
        /// 按当前输入给出补全候选。第一个词补命令名，后面的词按命令登记的参数提示补。
        /// </summary>
        public static List<string> Complete(string line)
        {
            List<string> res = new List<string>();
            string[] parts = Tokenize(line ?? "");
            bool trailingSpace = line != null && line.Length > 0 && char.IsWhiteSpace(line[line.Length - 1]);

            if (parts.Length == 0)
            {
                for (int i = 0; i < _all.Count; i++) res.Add(_all[i].Name);
                return res;
            }

            if (parts.Length == 1 && !trailingSpace)
            {
                string p = parts[0].ToLowerInvariant();
                for (int i = 0; i < _all.Count; i++)
                {
                    if (_all[i].Name.ToLowerInvariant().StartsWith(p)) res.Add(_all[i].Name);
                }
                return res;
            }

            Cmd c = Find(parts[0]);
            if (c == null || c.Hints == null) return res;

            int argIndex = parts.Length - (trailingSpace ? 0 : 1);
            if (argIndex >= 1 && argIndex <= c.Hints.Length)
            {
                string[] bank = Cmds.Tokenize(c.Hints[argIndex - 1]);
                string p = trailingSpace ? "" : parts[parts.Length - 1].ToLowerInvariant();
                for (int i = 0; i < bank.Length; i++)
                {
                    if (p.Length == 0 || bank[i].ToLowerInvariant().StartsWith(p)) res.Add(bank[i]);
                }
            }
            return res;
        }
    }
}