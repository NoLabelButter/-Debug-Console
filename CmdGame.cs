using System;
using System.Collections.Generic;


namespace DebugConsole
{
    /// <summary>数字/数量的显示格式。</summary>
    internal static class Fmt
    {
        public static string Money(long v)
        {
            return v.ToString("N0") + " 元";
        }

        public static string Int(long v)
        {
            return v.ToString("N0");
        }
    }

    /// <summary>
    /// 游戏侧命令：金钱存档 / 物品 / 状态。
    /// 每条命令都自己写输出，控制台不管内容；异常统一由 Cmds.Execute 兜住。
    /// </summary>
    internal static class CmdGame
    {
        public static void Register()
        {
            RegisterStore();
            RegisterItems();
            RegisterState();
            RegisterSpecial();
        }

        /// <summary>没进存档时的统一提示。返回 true 表示「已拦住，别往下走了」。</summary>
        private static bool NeedStore()
        {
            if (GameApi.Store != null) return false;
            Out.Warn("还没进存档。先开一局或读一个档，这条命令才有东西可改。");
            return true;
        }

        // ── 卡片的实时读数 ────────────────────────────────────────────
        // 统一约定：读不到就返回 null，卡片会显示成「—」，不会抛出异常。

        private static string ReadCash()
        {
            if (GameApi.Store == null) return null;
            int v;
            return GameApi.TryGetCash(out v) ? Fmt.Money(v) : null;
        }

        private static string ReadRent()
        {
            if (GameApi.Store == null) return null;
            int rent, days, start;
            if (!GameApi.TryGetRent(out rent, out days, out start)) return null;
            return Fmt.Money(rent) + "　还欠 " + days + " 天";
        }

        private static string ReadFlag(string key, string on, string off)
        {
            if (GameApi.Store == null) return null;
            bool v;
            return GameApi.TryGetFlag(key, out v) ? (v ? on : off) : null;
        }

        /// <summary>「各区声望」卡的读数：跟着下拉框里选中的那个区走。</summary>
        private static string ReadRep(CardPick pick)
        {
            if (GameApi.Store == null) return null;
            if (pick == null || pick.Keys == null || pick.Keys.Length == 0) return null;
            int i = pick.Sel < 0 || pick.Sel >= pick.Keys.Length ? 0 : pick.Sel;
            string name = GameApi.RepName(pick.Keys[i]);
            int v;
            return GameApi.TryGetRep(pick.Keys[i], out v) ? name + " " + v : name + " —";
        }

        private static string ReadOwned()
        {
            if (GameApi.Store == null) return null;
            return GameApi.OwnedItems().Count + " 件";
        }

        private static string ReadCatalog()
        {
            int n = GameApi.AllItemIds().Count;
            return n > 0 ? n + " 件" : null;
        }

        private static string ReadSkip()
        {
            if (GameApi.Store == null) return null;
            int day = GameApi.DayNumber();
            string s = GameApi.SkipLeft > 0 ? "连跳中，还剩 " + GameApi.SkipLeft + " 天" : "空闲";
            return day > 0 ? "第 " + day + " 天 · " + s : s;
        }

        // ══════════════════════════════════════════════════════════════
        //  金钱存档
        // ══════════════════════════════════════════════════════════════

        private static void RegisterStore()
        {
            Cmds.Add(new Cmd
            {
                Name = "money",
                Aliases = new[] { "cash", "钱" },
                Usage = "money [set|add|sub] [数额]",
                Help = "不带参数看当前金钱；set 直接改成这个数；add / sub 加减，写负数一样是减。",
                Group = "money",
                Preset = "money add 10000",
                Hints = new[] { "set add sub" },
                Cards = new[]
                {
                    new Card("金钱", "正数加钱，负数扣钱，填多少改多少", ReadCash,
                        new CardInput("10000 或 -5000", "money add ", "", "改钱"))
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    int cur;
                    if (!GameApi.TryGetCash(out cur))
                    {
                        Out.Err("读不到当前金钱。");
                        return;
                    }

                    if (a.Count == 0)
                    {
                        Out.Line("当前金钱 " + Palette.TagVal(Fmt.Money(cur)));
                        return;
                    }

                    string op = a.Lower(0);
                    if (op == "add" || op == "sub" || op == "set")
                    {
                        if (!a.Has(1))
                        {
                            Out.Err("用法：" + Palette.TagKey("money " + op + " 数额"));
                            return;
                        }
                        long delta = a.Long(1, long.MinValue);
                        if (delta == long.MinValue)
                        {
                            Out.Err("「" + a.At(1) + "」不是个数字。");
                            return;
                        }
                        long target = op == "set" ? delta : (op == "add" ? cur + delta : cur - delta);
                        if (target < 0) target = 0;
                        if (target > int.MaxValue) target = int.MaxValue;

                        if (!GameApi.TrySetCash((int)target))
                        {
                            Out.Err("写入金钱失败。");
                            return;
                        }
                        int now;
                        GameApi.TryGetCash(out now);
                        long diff = (long)now - cur;
                        Out.Ok("金钱 " + Fmt.Money(cur) + " → " + Palette.TagVal(Fmt.Money(now))
                            + Palette.TagMuted("　(" + (diff >= 0 ? "+" : "−") + Fmt.Int(Math.Abs(diff)) + ")"));
                        return;
                    }

                    // 只写了个数字：当 set 用，顺手一点
                    long v2 = a.Long(0, long.MinValue);
                    if (v2 == long.MinValue)
                    {
                        Out.Err("不认识的写法「" + a.At(0) + "」。用法：" + Palette.TagKey("money set|add|sub 数额"));
                        return;
                    }
                    if (v2 < 0) v2 = 0;
                    if (v2 > int.MaxValue) v2 = int.MaxValue;
                    if (!GameApi.TrySetCash((int)v2)) { Out.Err("写入金钱失败。"); return; }
                    Out.Ok("金钱 " + Fmt.Money(cur) + " → " + Palette.TagVal(Fmt.Money(v2)));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "save",
                Aliases = new[] { "存档" },
                Usage = "save",
                Help = "立刻把当前进度写进存档文件。",
                Group = "save",
                Preset = "save",
                Cards = new[]
                {
                    new Card("存档", "把当前进度立刻写进存档文件", null, new[]
                    {
                        new CardAct("立即存档", "save", 1),
                    })
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    if (GameApi.TrySave()) Out.Ok("已写入存档。");
                    else Out.Err("写存档失败，请查看游戏日志。");
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "skip",
                Aliases = new[] { "跳天", "nextday" },
                Usage = "skip [天数 | stop]",
                Help = "快进天数：不带参数跳 1 天；写数字跳这么多天（最多 100）；stop 停下正在走的连跳。",
                Group = "save",
                Preset = "skip 1",
                Hints = new[] { "1 3 7 30 stop" },
                Cards = new[]
                {
                    new Card("跳过一天", "等一整天过去太慢的时候用", ReadSkip, new[]
                    {
                        new CardAct("跳过 1 天", "skip 1", 1),
                    }),
                    new Card("跳过天数", "填几天就跳几天，中途还能停", ReadSkip,
                        new CardInput("7", "skip ", "", "开始跳"))
                },
                Run = a =>
                {
                    if (NeedStore()) return;

                    if (a.Count > 0)
                    {
                        string op = a.Lower(0);
                        if (op == "stop" || op == "cancel" || op == "停")
                        {
                            Out.Line(GameApi.CancelSkip() ? "连跳停下了。" : "现在没在连跳。");
                            return;
                        }
                    }

                    int days = a.Count == 0 ? 1 : a.Int(0, 0);
                    if (days < 1)
                    {
                        Out.Err("用法：" + Palette.TagKey("skip [天数 | stop]") + "　天数得是正数。");
                        return;
                    }

                    int left = GameApi.SkipLeft;
                    if (left > 0) { Out.Warn("上一次连跳还没走完（还剩 " + left + " 天）。"); return; }

                    string err;
                    if (!GameApi.StartSkip(days, out err)) { Out.Err(err); return; }
                    Out.Ok("开始快进 " + Palette.TagVal(days + " 天")
                        + Palette.TagMuted("　一天一天推进，进度会打在下面；想停就 " + Palette.TagKey("skip stop") + "。"));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "rent",
                Aliases = new[] { "租金" },
                Usage = "rent [set 数额 | days 天数]",
                Help = "不带参数看当前租金和还欠几天；set 改金额，days 改倒计时天数。",
                Group = "money",
                Preset = "rent",
                Hints = new[] { "set days" },
                Cards = new[]
                {
                    new Card("租金设置", "填多少就是多少，填 0 免租", ReadRent,
                        new CardInput("500", "rent set ", "", "设置")),
                    new Card("缴租倒计时", "还欠几天到期，填多少就是多少", ReadRent,
                        new CardInput("30", "rent days ", "", "设置"))
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    int rent, days, start;
                    if (!GameApi.TryGetRent(out rent, out days, out start))
                    {
                        Out.Err("读不到租金信息。");
                        return;
                    }

                    if (a.Count == 0)
                    {
                        Out.Line("租金 " + Palette.TagVal(Fmt.Money(rent))
                            + Palette.TagMuted("（初始 " + Fmt.Money(start) + "）")
                            + "　还欠 " + Palette.TagVal(days + " 天"));
                        return;
                    }

                    string op = a.Lower(0);
                    if (op == "set")
                    {
                        int v = a.Int(1, -1);
                        if (v < 0) { Out.Err("用法：" + Palette.TagKey("rent set 数额")); return; }
                        if (!GameApi.TrySetRent(v)) { Out.Err("写入失败。"); return; }
                        Out.Ok("租金已改成 " + Palette.TagVal(Fmt.Money(v)) + "。");
                        return;
                    }
                    if (op == "days")
                    {
                        int v = a.Int(1, -1);
                        if (v < 0) { Out.Err("用法：" + Palette.TagKey("rent days 天数")); return; }
                        if (!GameApi.TrySetRentDays(v)) { Out.Err("写入失败。"); return; }
                        Out.Ok("缴租倒计时已改成 " + Palette.TagVal(v + " 天") + "，别忘了 " + Palette.TagKey("save") + "。");
                        return;
                    }
                    Out.Err("不认识的写法「" + a.At(0) + "」。用法：" + Palette.TagKey("rent [set 数额 | days 天数]"));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "power",
                Aliases = new[] { "电" },
                Usage = "power [on|off]",
                Help = "看 / 改店铺电闸。停电的状态下开门做生意会很难受，调试时常用。",
                Group = "save",
                Preset = "power on",
                Hints = new[] { "on off" },
                Cards = new[]
                {
                    new Card("店铺电闸", null, () => ReadFlag("power", "通电", "断电"), new[]
                    {
                        new CardAct("合闸", "power on", 1),
                        new CardAct("拉闸", "power off", 0),
                    })
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    bool cur;
                    if (!GameApi.TryGetFlag("power", out cur)) { Out.Err("读不到电闸状态。"); return; }
                    if (a.Count == 0)
                    {
                        Out.Line("电闸：" + (cur ? Palette.TagOk("通电") : Palette.TagErr("断电")));
                        return;
                    }
                    if (!a.Has(0) || (a.Lower(0) != "on" && a.Lower(0) != "off"))
                    {
                        Out.Err("用法：" + Palette.TagKey("power on|off"));
                        return;
                    }
                    bool want = a.Bool(0, cur);
                    if (want == cur) { Out.Line(cur ? "电闸本来就是通电的。" : "电闸本来就是断电的。"); return; }
                    if (!GameApi.TrySetFlag("power", want)) { Out.Err("写入失败。"); return; }
                    Out.Ok(want ? "电闸已合上。" : "电闸已断开。");
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "hard",
                Aliases = new[] { "硬核" },
                Usage = "hard [on|off]",
                Help = "看 / 改硬核模式开关。改动只在运行时生效，存档里记的是你开局选的模式。",
                Group = "save",
                Preset = "hard off",
                Hints = new[] { "on off" },
                Cards = new[]
                {
                    new Card("硬核模式", "只在运行时生效，存档里记的是开局选的", () => ReadFlag("hard", "开", "关"), new[]
                    {
                        new CardAct("关闭硬核", "hard off", 2),
                        new CardAct("开启硬核", "hard on", 0),
                    })
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    bool cur;
                    if (!GameApi.TryGetFlag("hard", out cur)) { Out.Err("读不到硬核开关。"); return; }
                    if (a.Count == 0)
                    {
                        Out.Line("硬核模式：" + (cur ? Palette.TagErr("开") : Palette.TagOk("关")));
                        return;
                    }
                    bool want = a.Bool(0, cur);
                    if (!GameApi.TrySetFlag("hard", want)) { Out.Err("写入失败。"); return; }
                    Out.Ok(want ? "硬核模式已打开。" : "硬核模式已关闭。");
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "night",
                Aliases = new[] { "日志" },
                Usage = "night <文本>",
                Help = "往夜间报告里写一条自己的记录（看看报告页排版时很有用）。",
                Group = "save",
                Cards = new[]
                {
                    new Card("夜间报告", "写一条自己的记录进去", null,
                        new CardInput("要写的话…", "night ", "", "写入"))
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    string text = a.Rest(0);
                    if (string.IsNullOrEmpty(text)) { Out.Err("用法：" + Palette.TagKey("night 要写的话")); return; }
                    if (GameApi.TryNightLog("【调试】" + text, null)) Out.Ok("已写入夜间报告。");
                    else Out.Err("写入失败。");
                }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  物品
        // ══════════════════════════════════════════════════════════════

        private const int PageSize = 14;

        private static void RegisterItems()
        {
            Cmds.Add(new Cmd
            {
                Name = "spawn",
                Aliases = new[] { "刷" },
                Usage = "spawn <物品|中文名> [数量] [柜台|仓库]",
                Help = "生成物品放进店里。ID 和中文名都认，中文名匹配到多个会列出来让你挑；"
                     + "最后一项写「仓库」就放后仓，不写放柜台。",
                Group = "item",
                Preset = "spawn 止痛药 5",
                Hints = new[] { "", "1 5 10", "柜台 仓库" },
                Cards = new[]
                {
                    new Card("刷物品", "开物品控制台：一柜的货，点一件就刷", ReadCatalog, new[]
                    {
                        new CardAct("打开物品控制台", "ui items", 0),
                    })
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    if (!a.Has(0))
                    {
                        Out.Err("用法：" + Palette.TagKey("spawn <物品|中文名> [数量] [柜台|仓库]"));
                        return;
                    }

                    List<string> hits;
                    string id = ResolveItem(a.At(0), out hits);
                    if (id == null)
                    {
                        if (hits.Count == 0) Out.Err("找不到叫「" + a.At(0) + "」的物品。用 " + Palette.TagKey("item search 关键词") + " 找找。");
                        else
                        {
                            Out.Warn("「" + a.At(0) + "」匹配到 " + hits.Count + " 件，把 ID 写全一点：");
                            PrintIdList(hits, 12);
                        }
                        return;
                    }

                    int count = a.Int(1, 1);
                    if (count < 1) count = 1;
                    if (count > 200) { Out.Warn("一次最多刷 200 件，已按 200 处理。"); count = 200; }

                    bool toStore = IsStoreToken(a.At(2));
                    string err = GameApi.Spawn(id, count, toStore);
                    if (err != null) { Out.Err(err); return; }
                    Out.Ok("生成 " + Palette.TagVal(count + " × " + GameApi.ItemName(id))
                        + Palette.TagMuted("　(" + id + ")　放进了" + (toStore ? "仓库" : "柜台")));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "item",
                Aliases = new[] { "物品" },
                Usage = "item list [页] | search <关键词> | count <物品> | own <物品>",
                Help = "物品查询。list 翻全部物品，search 按中文名/ID 搜，count / own 查一件货。"
                     + "清空和刷物品都搬到「物品控制台」面板里了（顶栏按钮或 ui items）。",
                Group = "item",
                Preset = "item search 药",
                Hints = new[] { "list search count own", "" },
                Cards = new Card[0],
                Run = a =>
                {
                    if (!a.Has(0)) { PrintItemHelp(); return; }
                    switch (a.Lower(0))
                    {
                        case "list": ItemList(a.Int(1, 1)); break;
                        case "search": ItemSearch(a.Rest(1)); break;
                        case "count": ItemCount(a.At(1)); break;
                        case "own": ItemOwn(a.At(1)); break;
                        default: PrintItemHelp(); break;
                    }
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "reroll",
                Aliases = new[] { "刷新顾客", "reload" },
                Usage = "reroll",
                Help = "把柜台前这位顾客带的货换一批：件数和种类都重掷，跟游戏自己刷新的那套一样。",
                Group = "item",
                Preset = "reroll",
                Cards = new[]
                {
                    new Card("刷新客户物品", "柜台前这位带的货换一批", ReadClient, new[]
                    {
                        new CardAct("刷新", "reroll", 1),
                    })
                },
                Run = a =>
                {
                    int oldN, newN;
                    string err = GameApi.RerollCustomerStock(out oldN, out newN);
                    if (err != null) { Out.Err("刷新客户物品失败：" + err); return; }
                    Out.Ok("顾客的货换了一批：" + Palette.TagVal(oldN + " 件 → " + newN + " 件") + "。");
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "prop",
                Aliases = new[] { "属性", "tag" },
                Usage = "prop [real]",
                Help = "改物品属性（就是打标器上那几条标签）：在店里按住 Ctrl+Shift+A 划过或者框选一批物品，"
                     + "松手弹出游戏自己的打标器界面 —— 在上面改哪一条，这一批里其余的同一条属性都跟着改。"
                     + "只想改一件就直接开界面改，一样用。"
                     + "打标器只贴标签，物品的真品质是另记一笔的；按 prop real（卡片上的「标签转成真品质」）"
                     + "才把真品质也拨成标签现在写的那个（装水的瓶子会按那一档整瓶重灌，不用手动倒）。",
                Group = "item",
                Preset = "prop",
                Hints = new[] { "real" },
                Cards = new[]
                {
                    new Card("改物品属性", "Ctrl+Shift+A 划过一批改标签（整批跟着改）；标签转成真品质 = 真品质也跟标签对齐",
                        ReadProp, new[]
                    {
                        new CardAct("改选中这件", "prop", 1),
                        new CardAct("标签转成真品质", "prop real", 1),
                    })
                },
                Run = a =>
                {
                    if (a.Count > 0)
                    {
                        string mode = a.Lower(0);
                        if (mode == "real" || mode == "true" || mode == "真")
                        {
                            int items, feats;
                            System.Collections.Generic.List<string> trace;
                            string report = GameApi.MakeLabelsReal(out items, out feats, out trace);
                            if (report == null)
                            {
                                Out.Warn("先在店里右键点一件物品（或者把鼠标移到物品上），再来按这个。");
                                return;
                            }
                            if (trace != null)
                            {
                                for (int i = 0; i < trace.Count && i < 6; i++) Out.Line(Palette.TagMuted(trace[i]));
                            }
                            Out.Ok(report);
                            return;
                        }
                    }

                    GameItem it = GameApi.SampleItem();
                    if (it == null)
                    {
                        Out.Warn("先在店里右键点一件物品（或者把鼠标移到物品上），再来开打标器。");
                        return;
                    }

                    string name = null;
                    try { name = GameApi.ItemName(it.identifier); } catch { }
                    string err = GameApi.OpenLabeler(it);
                    if (err != null) { Out.Err("打开打标器界面失败：" + err); return; }

                    Out.Ok("打标器界面开了：改 " + Palette.TagVal(string.IsNullOrEmpty(name) ? "这件物品" : name)
                        + "，在上面改就行。");
                }
            });
        }

        private static void PrintItemHelp()
        {
            Out.Line(Palette.TagKey("item list [页]") + "　翻全部物品 ID + 中文名");
            Out.Line(Palette.TagKey("item search 关键词") + "　按中文名或 ID 搜索");
            Out.Line(Palette.TagKey("item count 物品") + "　柜台里有几件这件货");
            Out.Line(Palette.TagKey("item own 物品") + "　游戏认为你拥有这件货吗");
            Out.Line("生成物品用 " + Palette.TagKey("spawn <物品> [数量] [柜台|仓库]") + "，"
                + "或者在物品控制台里点货刷（" + Palette.TagKey("ui items") + "）。");
            Out.Line("改物品属性用 " + Palette.TagKey("prop") + "（或「改物品属性」卡）：把游戏自己的打标器界面打开。"
                + "批量改就在店里按住 " + Palette.TagKey("Ctrl+Shift+A") + " 划过或框选一批，松手弹出界面，"
                + "改哪一条这一批就跟着改哪一条。");
            Out.Line("打标器只贴标签；想让真品质也变成标签写的那个，用 "
                + Palette.TagKey("prop real") + "（卡片上「标签转成真品质」那一下）。");
        }

        /// <summary>「刷新客户物品」卡的读数：柜台前站着谁（和强制击杀看的是同一条）。</summary>
        private static string ReadClient()
        {
            string who = GameApi.ClientAtCounter();
            return string.IsNullOrEmpty(who) ? "柜台前：没人" : "柜台前：" + who;
        }

        /// <summary>「改物品属性」卡的读数：现在会改哪件（右键选中的，或者鼠标底下那件）。</summary>
        private static string ReadProp()
        {
            string who = GameApi.PropTargetText();
            return string.IsNullOrEmpty(who) ? "选中：还没选" : "选中：" + L10n.Raw(who);
        }

        /// <summary>把玩家输入的一个词解析成物品 ID：先当 ID 认，再当中文名搜。</summary>
        private static string ResolveItem(string token, out List<string> hits)
        {
            hits = new List<string>();
            if (string.IsNullOrEmpty(token)) return null;

            if (GameApi.ItemExists(token)) return token;

            string lower = token.ToLowerInvariant();
            List<string> all = GameApi.AllItemIds();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].ToLowerInvariant() == lower) return all[i];
            }

            // 中文名 / 部分 ID 模糊匹配
            for (int i = 0; i < all.Count; i++)
            {
                string name = GameApi.ItemName(all[i]);
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                    || all[i].IndexOf(lower, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hits.Add(all[i]);
                }
            }
            if (hits.Count == 1) return hits[0];
            return null;
        }

        /// <summary>判断生成位置那一项写的是不是「仓库」（不写、写别的都是柜台）。</summary>
        private static bool IsStoreToken(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            return s == "store" || s == "back" || s == "仓库" || s == "后仓" || s == "库房";
        }

        private static void PrintIdList(List<string> ids, int limit)
        {
            int n = Math.Min(ids.Count, limit);
            for (int i = 0; i < n; i++)
            {
                Out.Line(Palette.TagMuted("· ") + Palette.TagKey(ids[i])
                    + Palette.TagMuted("  " + GameApi.ItemName(ids[i])));
            }
            if (ids.Count > n) Out.Line(Palette.TagMuted("…还有 " + (ids.Count - n) + " 条，再写细一点。"));
        }

        private static void ItemList(int page)
        {
            List<string> ids = GameApi.AllItemIds();
            if (ids.Count == 0)
            {
                Out.Warn("物品目录是空的 —— 目录在进档之后才会注册，先开一局。");
                return;
            }
            int pages = (ids.Count + PageSize - 1) / PageSize;
            if (page < 1) page = 1;
            if (page > pages) page = pages;
            int start = (page - 1) * PageSize;

            Out.Line("物品目录 " + Palette.TagVal(ids.Count + " 件")
                + Palette.TagMuted("　第 " + page + " / " + pages + " 页")
                + "　用 " + Palette.TagKey("item list " + (page < pages ? page + 1 : page)) + " 翻页");
            int end = Math.Min(start + PageSize, ids.Count);
            for (int i = start; i < end; i++)
            {
                string id = ids[i];
                Out.Line(Palette.TagMuted((i + 1).ToString().PadLeft(3, ' ') + "  ")
                    + Palette.TagKey(id) + "  " + GameApi.ItemName(id));
            }
        }

        private static void ItemSearch(string kw)
        {
            if (string.IsNullOrEmpty(kw)) { Out.Err("用法：" + Palette.TagKey("item search 关键词")); return; }
            List<string> ids = GameApi.AllItemIds();
            if (ids.Count == 0) { Out.Warn("物品目录还没就绪，先进一次存档。"); return; }

            List<string> hits = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                string name = GameApi.ItemName(ids[i]);
                if (name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                    || ids[i].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hits.Add(ids[i]);
                }
            }
            if (hits.Count == 0) { Out.Warn("没有匹配「" + kw + "」的物品。"); return; }
            Out.Line("匹配 " + Palette.TagVal(hits.Count + " 件") + "：");
            PrintIdList(hits, 20);
        }

        private static void ItemCount(string token)
        {
            if (string.IsNullOrEmpty(token)) { Out.Err("用法：" + Palette.TagKey("item count <物品>")); return; }

            string id = token;
            if (!GameApi.ItemExists(id))
            {
                List<string> hits;
                id = ResolveItem(token, out hits);
                if (id == null) { Out.Err("找不到物品「" + token + "」。"); return; }
            }

            System.Collections.Generic.List<GameItem> owned = GameApi.OwnedItems();
            int n = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                string oid = null;
                try { oid = owned[i].identifier; } catch { }
                if (string.Equals(oid, id, StringComparison.OrdinalIgnoreCase)) n++;
            }
            Out.Line(GameApi.ItemName(id) + "：" + Palette.TagVal(n + " 件")
                + Palette.TagMuted("　柜台里共 " + owned.Count + " 件货"));
        }

        private static void ItemOwn(string token)
        {
            if (string.IsNullOrEmpty(token)) { Out.Err("用法：" + Palette.TagKey("item own <物品>")); return; }
            string id = token;
            if (!GameApi.ItemExists(id))
            {
                List<string> hits;
                id = ResolveItem(token, out hits);
                if (id == null) { Out.Err("找不到物品「" + token + "」。"); return; }
            }
            bool own = GameApi.OwnsItem(id);
            Out.Line(GameApi.ItemName(id) + "：" + (own ? Palette.TagOk("拥有") : Palette.TagMuted("没有")));
        }

        // ══════════════════════════════════════════════════════════════
        //  状态
        // ══════════════════════════════════════════════════════════════

        private static void RegisterState()
        {
            Cmds.Add(new Cmd
            {
                Name = "stat",
                Aliases = new[] { "状态" },
                Usage = "stat",
                Help = "一屏看完金钱、租金、声望、电闸、硬核开关这些关键数值。",
                Group = "state",
                Preset = "stat",
                Cards = new[]
                {
                    new Card("状态总览", "一屏看完金钱、租金、声望、开关", ReadCash, new[]
                    {
                        new CardAct("打印到输出区", "stat", 1),
                    })
                },
                Run = a => PrintStat()
            });

            Cmds.Add(new Cmd
            {
                Name = "rep",
                Aliases = new[] { "声望" },
                Usage = "rep [区 点数]",
                Help = "不带参数列出每个区的声望；写「区 点数」只改那一个区，正数加、负数扣。"
                     + "区写中文名（下层区 / 上层区 / 治安部 / 黑市 / 革命军）或者 ID（ll / ul / sec / bm / rev）都认，"
                     + "「治安区」「反抗军」这种叫法也认。",
                Group = "state",
                Preset = "rep",
                Hints = new[] { "下层区 上层区 治安部 黑市 革命军" },
                Cards = new[] { RepCard() },
                Run = a =>
                {
                    if (NeedStore()) return;

                    if (a.Count == 0) { PrintReps(); return; }

                    string id = ResolveRep(a.At(0));
                    if (id == null)
                    {
                        Out.Err("认不出「" + a.At(0) + "」是哪个区。");
                        PrintReps();
                        return;
                    }
                    if (!a.Has(1)) { Out.Err("用法：" + Palette.TagKey("rep 区 点数")); return; }

                    int d = a.Int(1, int.MinValue);
                    if (d == int.MinValue) { Out.Err("「" + a.At(1) + "」不是个数字。"); return; }

                    int before;
                    GameApi.TryGetRep(id, out before);
                    if (!GameApi.TryModRep(id, d)) { Out.Err("改声望失败。"); return; }
                    int after;
                    GameApi.TryGetRep(id, out after);
                    Out.Ok(GameApi.RepName(id) + " 声望 " + before + " → " + Palette.TagVal(after.ToString())
                        + Palette.TagMuted("　(" + (d >= 0 ? "+" : "") + d + ")"));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "aug",
                Aliases = new[] { "改造" },
                Usage = "aug [on|off]",
                Help = "看 / 改「改造系统是否已引入」的标记。用来跳过前置剧情直接看后面的内容。",
                Group = "state",
                Preset = "aug on",
                Hints = new[] { "on off" },
                Cards = new[]
                {
                    new Card("改造系统", "跳过前置剧情直接看后面的内容",
                        () => ReadFlag("aug", "已引入", "未引入"), new[]
                    {
                        new CardAct("标记已引入", "aug on", 1),
                        new CardAct("标记未引入", "aug off", 0),
                    })
                },
                Run = a =>
                {
                    if (NeedStore()) return;
                    bool cur;
                    if (!GameApi.TryGetFlag("aug", out cur)) { Out.Err("读不到这个标记。"); return; }
                    if (a.Count == 0)
                    {
                        Out.Line("改造系统已引入：" + (cur ? Palette.TagOk("是") : Palette.TagMuted("否")));
                        return;
                    }
                    bool want = a.Bool(0, cur);
                    if (!GameApi.TrySetFlag("aug", want)) { Out.Err("写入失败。"); return; }
                    Out.Ok("改造系统已引入标记 → " + (want ? "是" : "否"));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "event",
                Aliases = new[] { "事件" },
                Usage = "event [list | fire 编号 | refresh]",
                Help = "list 列出游戏里注册的全部事件触发器；fire 编号 直接触发其中一个（供水危机这种）；"
                     + "refresh 让游戏重掷今天的事件。",
                Group = "state",
                Preset = "event list",
                Hints = new[] { "list fire refresh" },
                Cards = new[]
                {
                    new Card("事件", "当前挂着什么事件，或者让游戏重掷一次",
                        () => GameApi.ActiveEventsText(), new[]
                    {
                        new CardAct("看事件列表", "event list", 0),
                        new CardAct("重掷今天的事件", "event refresh", 0),
                    }),
                    EventFireCard()
                },
                Run = a =>
                {
                    if (NeedStore()) return;

                    if (a.Count == 0 || a.Lower(0) == "list")
                    {
                        List<GameApi.EventRef> list = GameApi.EventList();
                        if (list.Count == 0)
                        {
                            Out.Warn("读不到事件列表 —— 事件系统是进档之后才起来的。");
                            return;
                        }
                        Out.Line(Palette.TagMuted("── 事件列表 ─────────────────────"));
                        for (int i = 0; i < list.Count; i++)
                        {
                            GameApi.EventRef e = list[i];
                            Out.Line("  " + Palette.TagVal((i + 1).ToString())
                                + Palette.TagMuted("　[" + e.Kind + "] ") + Palette.TagKey(e.Name)
                                + Palette.TagMuted("　" + e.Id));
                        }
                        string act = GameApi.ActiveEventsText();
                        if (act != null) Out.Line(Palette.TagMuted("当前挂着：") + act);
                        Out.Line(Palette.TagMuted("触发：") + Palette.TagKey("event fire 编号"));
                        return;
                    }

                    string op = a.Lower(0);
                    if (op == "refresh" || op == "reroll" || op == "刷新")
                    {
                        if (GameApi.RefreshEvents()) Out.Ok("已经让游戏重新掷了一次今天的事件。");
                        else Out.Err("重掷失败，看日志。");
                        return;
                    }
                    if (op == "fire" || op == "触发")
                    {
                        int idx = a.Int(1, -1);
                        if (idx < 1)
                        {
                            Out.Err("用法：" + Palette.TagKey("event fire 编号")
                                + "　编号从 " + Palette.TagKey("event list") + " 里看。");
                            return;
                        }
                        string name;
                        string err = GameApi.FireEvent(idx, out name);
                        if (err != null) { Out.Err("触发失败：" + err); return; }
                        Out.Ok("已把事件推上场：" + Palette.TagVal(name));
                        return;
                    }
                    Out.Err("用法：" + Palette.TagKey("event [list | fire 编号 | refresh]"));
                }
            });
        }

        /// <summary>按中文名或 ID 认出是哪个区；认不出返回 null。</summary>
        private static string ResolveRep(string token)
        {
            GameApi.RepFaction f = GameApi.FindRep(token);
            return f != null ? f.Id : null;
        }

        /// <summary>「各区声望」卡：先在下拉框里挑一个区，再在框里填点数。顺序照游戏面板来。</summary>
        private static Card RepCard()
        {
            CardPick pick = new CardPick("区",
                new[] { "下层区", "上层区", "治安部", "黑市", "革命军" },
                new[] { "ll", "ul", "sec", "bm", "rev" },
                3, "50", "rep ", "改声望");
            return new Card("各区声望", "先挑一个区，再填点数，正数加、负数扣",
                () => ReadRep(pick), pick);
        }

        /// <summary>「触发指定事件」卡：下拉框里挑事件（进档后才读得到，展开时现取），点触发直接推上场。</summary>
        private static Card EventFireCard()
        {
            CardPick pick = new CardPick("事件", null, null, 0, "", "event fire ", "触发");
            pick.NoInput = true;
            pick.Provider = EventOptions;
            pick.EmptyHint = "（还没读到事件，先进档）";
            return new Card("触发指定事件", "下拉框里挑一个事件，点触发就推上场", null, pick);
        }

        /// <summary>事件下拉框的选项：值还是 event list 里的编号，显示用「[类型] 名字」。</summary>
        private static List<KeyValuePair<string, string>> EventOptions()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            List<GameApi.EventRef> evs = GameApi.EventList();
            for (int i = 0; i < evs.Count; i++)
            {
                list.Add(new KeyValuePair<string, string>(
                    (i + 1).ToString(), "[" + evs[i].Kind + "] " + evs[i].Name));
            }
            return list;
        }

        private static void PrintReps()
        {
            List<KeyValuePair<string, string>> list = GameApi.RepFactions();
            Out.Line(Palette.TagMuted("── 各区声望 ─────────────────────"));
            for (int i = 0; i < list.Count; i++)
            {
                int v;
                string val = GameApi.TryGetRep(list[i].Key, out v)
                    ? Palette.TagVal(v.ToString()) : Palette.TagMuted("—");
                Out.Line("  " + Palette.TagKey(list[i].Value)
                    + Palette.TagMuted("　(" + list[i].Key + ")　") + val);
            }
            Out.Line(Palette.TagMuted("改法：") + Palette.TagKey("rep 黑市 50")
                + Palette.TagMuted("　写负数就是扣。"));
        }

        private static void PrintStat()
        {
            if (NeedStore()) return;

            int cash; GameApi.TryGetCash(out cash);
            int rent, days, start; GameApi.TryGetRent(out rent, out days, out start);
            List<KeyValuePair<string, string>> reps = GameApi.RepFactions();
            bool power, hard, aug;
            GameApi.TryGetFlag("power", out power);
            GameApi.TryGetFlag("hard", out hard);
            GameApi.TryGetFlag("aug", out aug);

            Out.Line(Palette.TagMuted("── 当前状态 ─────────────────────"));
            Out.Line("金钱　" + Palette.TagVal(Fmt.Money(cash)));
            Out.Line("租金　" + Palette.TagVal(Fmt.Money(rent))
                + Palette.TagMuted("　还欠 " + days + " 天　起始 " + Fmt.Money(start)));
            string repText = "";
            for (int i = 0; i < reps.Count; i++)
            {
                int v;
                if (!GameApi.TryGetRep(reps[i].Key, out v)) continue;
                if (repText.Length > 0) repText += "　";
                repText += reps[i].Value + " " + v;
            }
            Out.Line("声望　" + (repText.Length > 0 ? Palette.TagVal(repText) : Palette.TagMuted("—")));
            Out.Line("电闸　" + (power ? Palette.TagOk("通电") : Palette.TagErr("断电"))
                + "　　硬核　" + (hard ? Palette.TagErr("开") : Palette.TagOk("关"))
                + "　　改造　" + (aug ? Palette.TagOk("已引入") : Palette.TagMuted("未引入")));
            Out.Line("库存　" + Palette.TagVal(GameApi.OwnedItems().Count + " 件")
                + Palette.TagMuted("　物品目录 " + GameApi.AllItemIds().Count + " 件"));
        }

        // ── 特殊功能：强制击杀 ────────────────────────────────────────

        /// <summary>
        /// 场面上的一枪：举枪演出照搬 emolunpan 那个模组（详见 GameApi.ArmShow），
        /// 中枪 / 倒地还是交给游戏自己的开枪结算（详见 GameApi.ForceKill）。
        /// </summary>
        private static void RegisterSpecial()
        {
            Cmds.Add(new Cmd
            {
                Name = "arm",
                Aliases = new[] { "举枪", "击杀" },
                Usage = "arm [kill|秒数|stop]",
                Help = "强制击杀：举枪开一枪，把柜台前这位打死（举枪演出 + 游戏自己的中枪 / 倒地结算）。"
                    + "arm 秒数 只比划不伤人，arm stop 收枪。不给参数就是击杀。",
                Group = "special",
                Preset = "arm kill",
                Hints = new[] { "kill", "stop" },
                Cards = new[]
                {
                    new Card("强制击杀", "举枪开一枪，把柜台前这位打死", ReadKill, new[]
                    {
                        new CardAct("开枪击杀", "arm kill", 2),
                    })
                },
                Run = a =>
                {
                    string k = a.Has(0) ? a.Lower(0) : "";

                    if (k == "stop" || k == "off" || k == "收枪")
                    {
                        GameApi.ArmOff();
                        Out.Ok("手臂收下去了。");
                        return;
                    }

                    if (k.Length == 0 || k == "kill" || k == "击杀" || k == "杀" || k == "打死")
                    {
                        string who;
                        string killErr = GameApi.ForceKill(out who);
                        if (killErr != null) { Out.Warn(killErr); return; }
                        GameApi.ArmOff();
                        Out.Ok("一枪打出去了：" + Palette.TagErr(who) + " 中枪倒地，后面交给游戏自己收尾。");
                        return;
                    }

                    float sec = a.Float(0, 3f);
                    if (sec <= 0f) sec = 3f;
                    if (sec > 60f) sec = 60f;
                    string err = GameApi.ArmShow(sec);
                    if (err != null) { Out.Err("举枪失败：" + err); return; }
                    Out.Ok("手臂已经举起来了（" + sec.ToString("0.#") + " 秒）。");
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "del",
                Aliases = new[] { "删除", "delete" },
                Usage = "del",
                Help = "删掉右键选中的那件物品。在店里右键点一下物品，再敲这条就行 —— "
                     + "更省事的是直接按 Ctrl+Shift+X，面板都不用开。",
                Group = "item",
                Preset = "del",
                Cards = new[]
                {
                    new Card("删除选中物品", "在店里右键点一下物品选中，按 Ctrl+Shift+X 直接删（不用开面板）",
                        GameApi.PickedText, new[]
                    {
                        new CardAct("删掉选中", "del", 2),
                    })
                },
                Run = a =>
                {
                    string name;
                    string err = GameApi.DeletePicked(out name);
                    if (err != null) { Out.Warn(err); return; }
                    Out.Ok("已删除 " + Palette.TagVal(string.IsNullOrEmpty(name) ? "物品" : name) + "。");
                }
            });
        }

        /// <summary>「强制击杀」卡的读数：柜台前站着谁。</summary>
        private static string ReadKill()
        {
            string who = GameApi.ClientAtCounter();
            if (string.IsNullOrEmpty(who)) return "柜台前：没人";
            return "柜台前：" + who;
        }
    }
}