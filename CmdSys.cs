using System;
using System.Collections.Generic;

namespace DebugConsole
{
    /// <summary>控制台自身的命令：帮助、清屏、主题、关闭。</summary>
    internal static class CmdSys
    {
        public static void Register()
        {
            Cmds.Add(new Cmd
            {
                Name = "help",
                Aliases = new[] { "?", "帮助" },
                Usage = "help [命令]",
                Help = "列出全部命令；给个命令名看它的详细用法。",
                Group = "sys",
                Preset = "help",
                Cards = new[]
                {
                    new Card("命令一览", "所有命令和用法", null, new[]
                    {
                        new CardAct("打印命令表", "help", 1),
                    })
                },
                Run = a =>
                {
                    if (a.Has(0))
                    {
                        Cmd c = Cmds.Find(a.At(0));
                        if (c == null) { Out.Err("没有叫「" + a.At(0) + "」的命令。"); return; }
                        PrintOne(c, true);
                        return;
                    }
                    Out.Line(Palette.TagMuted("── 命令一览 ─────────────────────"));
                    // 简单模式不摆反射那一堆，跟侧栏的取舍保持一致
                    List<CmdGroup> groups = Cmds.VisibleGroups(ConsoleUI.Mode != ConsoleMode.Simple);
                    for (int g = 0; g < groups.Count; g++)
                    {
                        CmdGroup grp = groups[g];
                        List<Cmd> list = Cmds.InGroup(grp.Key);
                        if (list.Count == 0) continue;
                        Out.Line(Palette.TagVal("【" + grp.Name + "】") + Palette.TagMuted("　" + grp.Hint));
                        for (int i = 0; i < list.Count; i++) PrintOne(list[i], false);
                    }
                    Out.Line(Palette.TagMuted("Tab 补全命令，↑ ↓ 翻历史，点左边快捷栏直接执行。"));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "clear",
                Aliases = new[] { "cls", "清屏" },
                Usage = "clear",
                Help = "清空输出区。",
                Group = "sys",
                Preset = "clear",
                Cards = new[]
                {
                    new Card("输出区", "清掉这一屏日志", null, new[]
                    {
                        new CardAct("清屏", "clear", 0),
                    })
                },
                Run = a => ConsoleUI.ClearLog()
            });

            Cmds.Add(new Cmd
            {
                Name = "theme",
                Aliases = new[] { "主题" },
                Usage = "theme",
                Help = "在深色 / 亮色之间切换（界面会重建一次）。顶栏那个「亮色 / 深色」按钮走的就是这条。",
                Group = "sys",
                Preset = "theme",
                Cards = new Card[0],
                Run = a => ConsoleUI.ToggleTheme()
            });

            Cmds.Add(new Cmd
            {
                Name = "mode",
                Aliases = new[] { "模式" },
                Usage = "mode [simple|hybrid|dev]",
                Help = "切界面模式：simple 只有操作卡片，hybrid 卡片 + 命令行，dev 纯命令行。",
                Group = "sys",
                Preset = "mode hybrid",
                Hints = new[] { "simple hybrid dev" },
                Cards = new[]
                {
                    new Card("界面模式", "简单只看卡片，混合两者都有", () => ConsoleUI.ModeName(), new[]
                    {
                        new CardAct("简单", "mode simple", 0),
                        new CardAct("混合", "mode hybrid", 0),
                        new CardAct("开发者", "mode dev", 0),
                    })
                },
                Run = a =>
                {
                    if (!a.Has(0)) { Out.Line("当前模式：" + Palette.TagVal(ConsoleUI.ModeName())); return; }
                    ConsoleMode m;
                    if (!ConsoleUI.ParseMode(a.Lower(0), out m))
                    {
                        Out.Err("用法：" + Palette.TagKey("mode simple|hybrid|dev"));
                        return;
                    }
                    ConsoleUI.SetMode(m);
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "ui",
                Aliases = new[] { "面板" },
                Usage = "ui items | back",
                Help = "切界面：items 打开物品控制台（一柜的货点着刷），back 回到命令界面。",
                Group = "sys",
                Preset = "ui items",
                Hints = new[] { "items back" },
                Cards = new Card[0],
                Run = a =>
                {
                    if (!a.Has(0)) { Out.Line("用法：" + Palette.TagKey("ui items | back")); return; }
                    string op = a.Lower(0);
                    if (op == "items" || op == "item" || op == "物品")
                    {
                        ConsoleUI.ShowItems(true);
                        return;
                    }
                    if (op == "back" || op == "返回" || op == "close")
                    {
                        ConsoleUI.ShowItems(false);
                        return;
                    }
                    Out.Err("不认识的写法「" + a.At(0) + "」。用法：" + Palette.TagKey("ui items | back"));
                }
            });

            Cmds.Add(new Cmd
            {
                Name = "close",
                Aliases = new[] { "关闭" },
                Usage = "close",
                Help = "关掉控制台，回到游戏。",
                Group = "sys",
                Preset = "close",
                Cards = new Card[0],
                Run = a => ConsoleUI.Close()
            });

            Cmds.Add(new Cmd
            {
                Name = "about",
                Aliases = new[] { "关于" },
                Usage = "about",
                Help = "这个控制台是什么、怎么用。",
                Group = "sys",
                Cards = new[]
                {
                    new Card("关于", "这个控制台是什么、怎么用", null, new[]
                    {
                        new CardAct("看说明", "about", 0),
                    })
                },
                Run = a =>
                {
                    Out.Line(Palette.TagVal("调试控制台") + Palette.TagMuted("　v" + Core.Version));
                    Out.Line("给店长自己用的调试后台：查看游戏状态、刷钱刷货、调整常用数值。");
                    Out.Line(Palette.TagMuted("· 顶栏可以切三种模式：") + Palette.TagKey("简单")
                        + Palette.TagMuted("（只有操作卡片）/ ") + Palette.TagKey("混合")
                        + Palette.TagMuted("（卡片 + 命令行）/ ") + Palette.TagKey("开发者") + Palette.TagMuted("（纯命令行）"));
                    Out.Line(Palette.TagMuted("· 左边按分类列出命令，点一下就直接执行（带参数的会把命令填进输入框）"));
                    Out.Line(Palette.TagMuted("· 命令行里 Tab 补全、↑↓ 翻历史"));
                }
            });
        }

        private static void PrintOne(Cmd c, bool detail)
        {
            if (detail)
            {
                Out.Line(Palette.TagKey(c.Usage));
                Out.Line("　" + c.Help);
                if (c.Aliases != null && c.Aliases.Length > 0)
                {
                    // 别名是「要照抄打进去」的 token，原样显示，别翻译
                    Out.Line(Palette.TagMuted("　别名：" + L10n.Raw(string.Join(" / ", c.Aliases))));
                }
                Out.Line(Palette.TagMuted("　分类：" + Cmds.GroupName(c.Group)));
                return;
            }

            string line = "  " + Palette.TagKey(c.Usage);
            if (!string.IsNullOrEmpty(c.Help)) line += Palette.TagMuted("　" + c.Help);
            Out.Line(line);
        }
    }
}
