# Escape From Duckov Mod 工作区

这是 **Escape From Duckov**（Team Soda）的 modding 环境，产物为 **C# DLL + info.ini**，不是 JSON mod。

## 资料导航

- `docs/game.md`、`docs/items.md`：游戏机制与物品；`docs/mod-api.md`：mod API；`docs/asset-mods.md`：资源替换方法。
- `docs/api/`：生成的 API 签名；`docs/data/`：数据/本地化 CSV、资源清单。按问题检索，不全量读入；这些是版本快照，不保证对应玩家最新游戏版本。
- `reference/example_mod/`：可编译样例；`libs/`：编译/分发依赖；产物规范见 `.pi/skills/mod-creator/SKILL.md`。
- `mod-repo.json`：平台路径和依赖声明；`game.version` 记录数据层快照对应版本。 其中 `workshop.supported` 声明本游戏**有没有创意工坊**；`workshopDir` 只是只读参考（不参与安装），判定规则见 `.pi/skills/setup-workspace/SKILL.md`。
- `.pi/skills/`：**三份按职责分开的技能**，任务需要时读对应那份（本 repo 的步骤指导在这里，不在产品提示词里）：
  - `setup-workspace/` —— 游戏在哪、三条路径是什么、dotnet 运行时是否就绪（`check_runtime` / `check_game_paths` / `set_*` / `install_runtime`）；
  - `mod-creator/` —— 做 / 改 mod：目录结构、`info.ini`、csproj 要点、`ModBehaviour`、`validate_mod` 编译校验；
  - `mod-installer/` —— 把产物装进游戏 `Mods/`（`install_mod`），以及装完要玩家做什么。
- `.pi/extensions/`：环境检查、安装指引、mod 校验、装进游戏（`install_mod`）工具；以实际返回的执行状态为准。
- `scripts/`：维护者刷新数据层的工具，玩家/agent 不运行；运行时准备与数据层维护是两件事。

## 工作区边界

`docs/`、`.pi/`、`reference/`、`libs/`、`scripts/` 等环境内容由维护者管理。`your_mods/` 是玩家成果区；**源码写入权限由当前 session 的角色和 mod 绑定决定**，不是整个 `your_mods/` 都可写。Game Helper 不编辑源码；获准的环境检查/校验工具可能写自己的缓存或编译产物，不授予通用写权限。
