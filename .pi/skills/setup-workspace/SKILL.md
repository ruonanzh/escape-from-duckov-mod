---
name: setup-workspace
description: Escape From Duckov 的环境与路径准备：定位游戏安装目录、Steam 创意工坊内容目录与 mod 安装目录，核实 dotnet 运行时是否就绪。当路径未知/记错、需要验证玩家给的路径、检查运行时、或准备在全新机器上开始做 mod 时读取。
---

# 准备工作区（Escape From Duckov）

把「游戏在哪、三条路径是什么、运行时够不够」弄清楚并记录。**这一步只产出事实**，不产出 mod。
加载技能不等于获得写权限；咨询可以只解释方法。

## 三条路径是什么（本 repo 的约定）

| 键（状态文件 `.gamer-agent.local.json`）| 含义 | 谁用 |
|---|---|---|
| `gameDir` | 游戏安装目录（Steam 库里的 `steamapps/common/Escape from Duckov`）| 编译引用 DLL、install_mod 拼安装目标 |
| `workshopDir` | Steam 创意工坊内容目录（`steamapps/workshop/content/3167020`）| **只读参考**：读工坊里现成 mod 的脚本/资源；**不参与安装** |
| `modInstallDir` | mod 安装目标（游戏目录内的 `Duckov.app/Contents/Mods`（macOS）/ `Duckov_Data/Mods`（Windows））| install_mod 复制产物到此 |

**必填与附加（U29）**：`gameDir` 必填；契约声明了 `modInstall`（非 null）时 `modInstallDir` 也必填 —— 任一不正确 = `FAIL`。
`workshopDir` 只是**只读参考**，**永不产 FAIL**（最多 `WARN`）；契约 `workshop.supported: false` 时**完全不看**（不检查、也不出现在结果里）。
本游戏 `mod-repo.json` 里 `workshop.supported = true`。

## 工具分工（别混用；判据是同一份实现，见 `.pi/lib/game-paths.ts`）

| 工具 | 什么时候用 | 副作用 |
|---|---|---|
| `check_runtime` | **只查 .NET SDK ≥ 8**（并记下 dotnet 路径）；**不找游戏** | 写运行时状态（只写 `runtime` 段）|
| `check_game_paths` | **只验**（**不打算改状态**时用它）：玩家只是问问、或想核对已记住的还对不对 | **无**（只读、不扫描、不写状态）|
| `set_game_dir` / `set_workshop_dir` / `set_mod_install_dir` | 玩家给了**具体路径** → 记住它（先过判据；不过则内部发现/派生，返回 WARN）| 写运行时状态 |
| `set_game_paths` | **路径要落地**（准备编译/安装）时用它：给了路径 = 校验并记录；**不带参数 = 自动发现并记录三条**（「发现」的入口）| 写运行时状态 |
| `install_runtime` | **只给指引**（或报已装），**不执行安装** | 无 |

> ⚠️ **验 ≠ 记录**：`check_game_paths` 与 `set_game_paths` 用的是**同一份判据**，区别只在**落不落库** ——
> 目标是**要落地**时**直接**用 `set_game_paths` 即可，不必「先用 `check_game_paths` 验一下、再 set」：
> 那样验完没记录，容易被当成任务已完成（U22 踩过）。

## 怎么用

- **需要编译而环境未知/已变化**：`check_runtime` 核实 dotnet SDK；**路径要落地就用 `set_game_paths`**（带参 = 校验并记录；无参 = 发现并记录三条）；**只想确认、不准备改状态**才用 `check_game_paths`（只读）。已有仍有效的结果就不必每轮重复检查。
- **缺 SDK**：取 `install_runtime` 的安装指引（工具不执行安装）；装好后**再检查一次**确认，再谈编译。
- **找不到游戏目录**：交给 `set_game_paths`（**不带参数**）—— 它自己找 Steam 根（Windows 读注册表 + 默认路径；macOS 读 `~/Library/Application Support/Steam`），
  再解析各库的 `libraryfolders.vdf` → `steamapps/common/<游戏名>`，并把三条路径一起记录。**建议顺序**：
  ① 先让它自己扫（无参调用）；② 玩家给了**模糊线索**（「装在 D 盘」「Steam 里」「下的那个」）就把线索变成候选目录，逐个用 `check_game_paths` 验证，再用 `set_game_paths` 记录；
  ③ **全都不行才问玩家** —— 问的时候：
     - **先说清你已经试过什么**（自动扫过哪些库、验证过哪些候选），不要只说「找不到」；
     - **给玩家可照做的入口**：Steam → 库 → 右键游戏 → 管理 → 浏览本地文件（或让玩家直接贴路径）；
     - 拿到路径后：`check_game_paths` 验证 → `set_game_paths` 记录 → 再回读另外两条。
     **不要**：猜路径、自己建目录、用装 SDK 去「解决」找不到游戏的问题。
- **玩家给了路径**：先用 `check_game_paths` 验证，再用对应的 `set_*`（或一次 `set_game_paths`）记录。
- **失败处理**：按错误定位修复；环境缺失/网络阻塞或同类失败重复出现时先解决前置条件，不无限"直到 PASS"。

## 相关技能

- 做 mod 本身（格式、csproj、info.ini、编译校验）→ `mod-creator`。
- 把产物装进游戏 → `mod-installer`。
