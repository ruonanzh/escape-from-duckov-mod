---
name: mod-installer
description: 把 Escape From Duckov 做好的 mod 产物装进游戏 Mods 目录：目标目录名的规则、重装/同名冲突/身份冲突的处理、事务化替换与安装后要玩家做什么。当玩家要"装进游戏""在游戏里试一下""更新已装的 mod"时读取。
---

# 装进游戏（Escape From Duckov）

`install_mod` 把 `your_mods/<mod名>/` 的产物复制到游戏 `Mods/` 目录。**它不自己探测目标** ——
目标来自 `check_runtime` 写入的状态文件（`modInstallDir`）；没记录时它会 `FAIL` 并让你先跑 `check_runtime`。

## 规则

- **装进去的目录名 = `your_mods/` 下的目录名**（**不是** `info.ini` 的 `name`）：这样一个目录名做单层校验就够了，不可能写到游戏目录之外；你在工作区看到的目录名与游戏 `Mods/` 里的一致，便于对号入座。
- **目录名（我们这边的规则，由 `create_mod_folder` 强制）**：小写字母开头，其后只能是小写字母/数字/下划线，总长 ≤ 40，且不得是 Windows 保留设备名（con/prn/aux/nul/com1-9/lpt1-9）。
- **重装 = 原地更新**：靠安装目录里的 `.pi-mod.json` 标记识别"这是我上次装的"。
- **目标目录被别的 mod 占用**：不覆盖别人，改装成 `<目录名>_pimod`（再撞顺延 `_pimod2`…），返回文案会说明占用者与落点 —— 把它如实告诉玩家。
- **身份**是 `info.ini` 的 `name` = C# 命名空间 = `<name>.dll`（必须为合法 C# 标识符，`validate_mod` 会校验）。它编进 DLL，**改目录名解决不了游戏里的身份冲突** —— 那种冲突要改 mod 名（即改命名空间）才能解决。
- **替换是事务性的**：装新版本时旧版本先挪到旁边、换入成功后才删；失败时旧版本仍在（不会新旧两份都丢）。

## 目标位置（本 repo 的事实）

- macOS：`<gameDir>/Duckov.app/Contents/Mods/`
- Windows：`<gameDir>/Duckov_Data/Mods/`
- **目录不存在不是错误**：全新机器上本来就没有，`install_mod` 会创建它。

## 装完之后

- 让玩家**启动游戏**确认 mod 被加载；游戏已在运行就先重启。
- `install_mod` 成功 ≠ 游戏内验证通过：如实区分"已装进目录""游戏已加载""效果符合预期"三种状态。
- 想确认游戏是否真的加载了它，可以让玩家看日志（`~/Library/Logs/TeamSoda/Duckov/Player.log` 里有 `Sorted mods:` 与 `ModActive_<name>`）。

## 相关技能

- 目标路径没记录/记错 → 先看 `setup-workspace`（`check_runtime` / `check_game_paths`）。
- 产物本身有问题（编译失败、空壳 dll）→ 先看 `mod-creator`（`validate_mod`）。
