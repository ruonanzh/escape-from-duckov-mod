# 游戏 API 与实现查询

> **写或修改 mod 前，必须先读本页。** 先确认游戏的实际签名与实现，再写 C#。

本页是 `inspect_game_api` 与 `inspect_game_code` 的唯一速查。游戏数据对象与数值仍用 `inspect_game_data`。

## 工具分工

| 需求 | 工具 |
|---|---|
| 查公开签名 | `docs/api/` 快照 |
| 查一个类型的当前签名、私有成员、反编译、IL 或字符串 | `inspect_game_api` |
| 在整个 dump 中找声明或调用位置 | `inspect_game_code` |
| 读一个类型或一个成员的实现 | `inspect_game_code` |
| 读物品、场景、组件与资源数据 | `inspect_game_data` |

`inspect_game_api` 直接读当前 DLL，不依赖 dump。`inspect_game_code` 读取本地反编译文件。两者都只读，不执行游戏代码。

## inspect_game_api 速查

| action | 用途 |
|---|---|
| `search` | 按类型或成员名搜索当前 DLL |
| `members` | 列出一个类型的成员，包含私有成员 |
| `decompile` | 反编译一个类型或成员 |
| `il` | 读取 IL |
| `strings` | 读取字符串字面量 |

它直接读当前 DLL，不依赖 dump。只问一个类型时优先用它。

默认只查 `mod-repo.json` 的 `compile.references`，并排除 `Unity*.dll`。查 `Assembly-CSharp` 或 Unity 类型时，用 `dll=<名字>` 或 `dll='*'`。零命中先检查 DLL 范围。

## inspect_game_code 速查

| action | 用途与例子 |
|---|---|
| `status` | 查看 dump 是否过期、覆盖哪些程序集、每个程序集有多少文件与失败项 |
| `search` | 按名字或代码片段搜索文件与行号。例：`action=search, target=ItemSetting_Gun` |
| `type` | 读取一个顶层类型。超过 200 行或 48 KB 会截断，其余成员用 `body` 读取。例：`action=type, target=ItemSetting_Gun` |
| `body` | 读取一个成员。例：`action=body, target=ItemSetting_Gun, member=SetTargetBulletType` |
| `dump` | 按 `.pi/lib/decompile.json` 重建。`assemblies=<DLL名>` 可增加本次覆盖，`force=true` 可强制重建 |

`search` 也找调用它的位置。`body` 只按成员声明提取实现，不返回调用位置。重载会一起返回。

## dump 的维护

- dump 在 `~/.gamer-agent-pi/decompiled/escape-from-duckov-mod/`。
- 游戏 DLL 或配置变化时，读取操作会自动重建。不得手动运行其它重建脚本。
- 默认覆盖 `TeamSoda.*` 与 `ItemStatsSystem`。额外程序集会增加时间与磁盘占用。
- 本次加入的程序集会留在当前 dump。下一次不带它的重建会移除它。长期覆盖必须改 `.pi/lib/decompile.json`。
- `Assembly-CSharp` 当前不纳入长期 dump。它的公开签名快照仍在 `docs/api/Assembly-CSharp.md`。出现真实查询缺口时再评估。

## 结果边界

- `status` 的 stale 也返回 PARTIAL。它只表示 dump 过期，读取操作会自动重建。
- `PARTIAL` 与 `INCOMPLETE_DUMP` 表示覆盖不全。先读实际 coverage 与 NEXT，再判断结果能证明什么。
- 覆盖不全时，零命中不表示游戏没有这个符号。`TYPE_NOT_FOUND` 与 `METHOD_NOT_FOUND` 也只表示当前覆盖里没找到。
- 完整覆盖下的零命中只表示当前 dump 没搜到。搜索词、嵌套类型与生成类型仍会影响结果。
- 嵌套类型写在父类型文件里。名字含 `<>` 的编译器生成类型不导出。
- `status` 与 `dump` 会显示每个程序集的 `failed` 数量。单个类型失败不能被当成完整成功。
- 版本不一致只给 WARN。不得把 WARN 当成当前代码已经复验。
