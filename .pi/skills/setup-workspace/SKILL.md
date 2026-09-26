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
| `check_runtime` | 一次跑全流程：发现 + 校验 + 记录（**首次**或环境可能变化时）| 写运行时状态 |
| `check_game_paths` | **只验**：玩家给了路径、或想确认已记住的还对不对 | **无**（只读、不扫描、不写状态）|
| `set_game_dir` / `set_workshop_dir` / `set_mod_install_dir` | 玩家给了**具体路径** → 记住它（先过判据；不过则内部发现/派生，返回 WARN）| 写运行时状态 |
| `set_game_paths` | 一次给多条路径（至少一条）| 写运行时状态 |
| `install_runtime` | **只给指引**（或报已装），**不执行安装** | 无 |

## 怎么用

- **需要编译而环境未知/已变化**：用 `check_runtime` 核实 dotnet SDK 与游戏目录。已有仍有效的结果就不必每轮重复检查。
- **缺 SDK**：取 `install_runtime` 的安装指引（工具不执行安装）；装好后**再检查一次**确认，再谈编译。
- **找不到游戏目录**：`check_runtime` 会先自己扫 Steam 库（`libraryfolders.vdf` + 各库 `steamapps/common/<游戏名>`）；都不行时再按需要向玩家确认安装位置 —— **不要**用装 SDK 去"解决"找不到游戏的问题。
- **玩家给了路径**：先用 `check_game_paths` 验证，再用对应的 `set_*`（或一次 `set_game_paths`）记录。
- **失败处理**：按错误定位修复；环境缺失/网络阻塞或同类失败重复出现时先解决前置条件，不无限"直到 PASS"。

## 相关技能

- 做 mod 本身（格式、csproj、info.ini、编译校验）→ `mod-creator`。
- 把产物装进游戏 → `mod-installer`。
