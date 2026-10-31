# Debug Console

An in-game debug toolkit for Probably Stolen by ButterLab.

## Build

Install the .NET SDK and the game. Build with the game installation directory supplied as an MSBuild property:

```powershell
dotnet build "Debug Console.csproj" -c Release -p:GameDir="C:\Games\Probably Stolen"
```

`GameDir` must point to the directory containing `Probably Stolen_Data\Managed\Assembly-CSharp.dll`. The build uses the game and mod framework assemblies from that directory. Output is written to `bin\Release`; the installable mod package is staged under `发版\DebugConsole`.

## License

Licensed under the [PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0). Commercial use is not permitted. See [LICENSE](LICENSE) for the full terms.

## Notes

This is a debug and cheat tool. Some commands directly change game state, including inventory, and may save changes through the game's save process. Back up saves before use.

The mod stores its own preferences in `DebugConsole.json` under the game's persistent data directory.

---

# Debug Console（中文）

Probably Stolen 游戏内调试工具，作者 ButterLab。

## 编译

安装 .NET SDK 和游戏，并在构建时传入游戏安装目录：

```powershell
dotnet build "Debug Console.csproj" -c Release -p:GameDir="C:\Games\Probably Stolen"
```

`GameDir` 应指向包含 `Probably Stolen_Data\Managed\Assembly-CSharp.dll` 的目录。项目会从该目录引用游戏及模组框架程序集。输出位于 `bin\Release`，可安装的模组文件会暂存到 `发版\DebugConsole`。

## 许可证

本项目使用 [PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0)，禁止商用。完整条款见 [LICENSE](LICENSE)。

## 提醒

这是调试和作弊工具。部分命令会直接修改游戏数据（包括库存），也可能通过游戏自身的存档流程保存修改。使用前请备份存档。

模组会在游戏的持久化数据目录中保存自身的 `DebugConsole.json` 设置文件。