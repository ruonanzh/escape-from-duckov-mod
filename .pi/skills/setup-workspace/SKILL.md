---
name: setup-workspace
description: Escape From Duckov 的环境与路径准备：定位游戏安装目录、Steam 创意工坊内容目录与 mod 安装目录，核实 dotnet 运行时是否就绪。当路径未知/记错、需要验证玩家给的路径、检查运行时、或准备在全新机器上开始做 mod 时读取。
---

# 准备工作区（Escape From Duckov）

把「游戏在哪、三条路径是什么、运行时够不够」弄清楚并记录。**这一步只产出事实**，不产出 mod。
加载技能不等于获得写权限；咨询可以只解释方法。

## 状态文件里有什么（本 repo 的约定）

`.gamer-agent.local.json`（**本工作区目录下**）是运行时状态的唯一来源，由 `.pi/extensions/` 的工具读写。字段：

| 键 | 含义 | 来源 | 谁用 |
|---|---|---|---|
| `gameDir` | 游戏安装目录（Steam 库里的 `steamapps/common/Escape from Duckov`）| 玩家给的，或 `try_set_game_dir` 自动发现 | 编译引用 DLL、install_mod 拼安装目标 |
| `workshopDir` | Steam 创意工坊内容目录（`steamapps/workshop/content/3167020`）| **由 `gameDir` 自动派生**（不用单独设）| **只读参考**：读工坊里现成 mod 的脚本/资源；**不参与安装** |
| `modInstallDir` | mod 安装目标（游戏目录内的 `Duckov.app/Contents/Mods`（macOS）/ `Duckov_Data/Mods`（Windows））| **由 `gameDir` 自动派生**（不用单独设）| install_mod 复制产物到此 |
| `managedDir` | 编译引用托管程序集（`TeamSoda.*.dll` 等）的目录 | **由 `gameDir` + `mod-repo.json` 现算**（工具**不信任**文件里的旧值）| 编译（`validate_mod`）|
| `runtime` | 运行时缓存：`dotnet` 可执行文件路径与版本 | `check_runtime` 探测后写入 | 编译时定位 dotnet |

**两条派生路径不用管**：把 `gameDir` 弄对（`try_set_game_dir` 或 `set_game_dir`），`workshopDir` / `modInstallDir` 会在**当前平台**上一起算好并记录。

**必填与附加**：`gameDir`、`modInstallDir` 都必填 —— 任一不正确 = `FAIL`。
`workshopDir` 只是**只读参考**，**永不产 FAIL**（最多 `WARN`）。
本游戏 `mod-repo.json` 里 `workshop.supported = true`。

## 完成标准

**三条路径都要有交代才算准备好**：`gameDir` 与 `modInstallDir` 必须可用；`workshopDir` 要么可用、要么明说「没找到工坊目录」（它只读，不影响安装）。
后两条由 `gameDir` 派生 —— 把 `gameDir` 弄对就会一起算好。**不要设完 `gameDir` 就收工。**

## 工具分工（别混用；判据是同一份实现，见 `.pi/lib/game-paths.ts`）

| 工具 | 做什么 | 副作用 |
|---|---|---|
| `try_set_game_dir` | **确保游戏目录已就绪**（无参、幂等）：已就绪 → 只回报；没就绪/过期 → 自己找，并记录游戏目录 + 由它派生的 mod 安装目录与创意工坊目录 | 写运行时状态（只在真的记录了时）|
| `set_game_dir` | **记录一个具体的游戏安装目录**（玩家给的，或你自己找到的）：先过判据再落库 | 写运行时状态 |
| `check_game_paths` | **只验**（只读）：核对玩家给的、或已记住的路径对不对——不写记录 | 无 |
| `check_runtime` | **只查 .NET SDK ≥ 8**（并记下 dotnet 路径）；**不找游戏** | 写运行时状态（只写 `runtime` 段）|
| `install_runtime` | **只给指引**（或报已装），**不执行安装** | 无 |

## 怎么用

- **需要编译而环境未知/已变化**：路径用 `try_set_game_dir`（无参，确保就绪）；SDK 用 `check_runtime`。已有仍有效的结果就不必每轮重复检查。
- **缺 SDK**：取 `install_runtime` 的安装指引（工具不执行安装）；装好后**再检查一次**确认，再谈编译。
- **找不到游戏目录**：`try_set_game_dir` 自己会找 —— 它按平台找 Steam 根（Windows 读注册表 + 默认路径；macOS 读 `~/Library/Application Support/Steam`），
  再解析各库的 `libraryfolders.vdf` → `steamapps/common/<游戏名>`。**它失败时，自己接着想办法，不要停在失败上**：
  ① 把玩家的模糊线索（「装在 D 盘」「Steam 里」「下的那个」）变成候选目录，用 `check_game_paths` 逐个只读验证；挑中后用 `set_game_dir` 记录；
  ② 这样都不行，才问玩家 —— 问的时候**先说清你已经试过什么**（找过哪些库、验过哪些候选），并给可照做的入口（Steam → 库 → 右键游戏 → 管理 → 浏览本地文件，或让玩家直接贴路径）。
  **不要**：猜路径、自己建目录、用装 SDK 去「解决」找不到游戏的问题。
- **玩家给了路径**：用 `set_game_dir`（先校验再记录）。

## 相关技能

- 做 mod 本身（格式、csproj、info.ini、编译校验）→ `mod-creator`。
- 把产物装进游戏 → `mod-installer`。
