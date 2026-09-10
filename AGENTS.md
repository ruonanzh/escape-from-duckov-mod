# Escape From Duckov Mod 工作区

这是 **Escape From Duckov**（Team Soda）的 modding 环境，产物为 **C# DLL + info.ini**，不是 JSON mod。

## 资料导航

- `docs/game.md`、`docs/items.md`：游戏机制与物品；`docs/mod-api.md`：mod API；`docs/asset-mods.md`：资源替换方法。
- `docs/api/`：生成的 API 签名；`docs/data/`：数据/本地化 CSV、资源清单。按问题检索，不全量读入；这些是版本快照，不保证对应玩家最新游戏版本。
- `reference/example_mod/`：可编译样例；`libs/`：编译/分发依赖；产物规范见 `.pi/skills/mod-authoring/SKILL.md`。
- `mod-repo.json`：平台路径和依赖声明；`game.version` 记录数据层快照对应版本。
- `.pi/skills/mod-authoring/`：制作、修改、编译和校验方法，任务需要时读取。
- `.pi/extensions/`：环境检查、安装指引、mod 校验工具；以实际返回的执行状态为准。
- `scripts/`：维护者刷新数据层的工具，玩家/agent 不运行；运行时准备与数据层维护是两件事。

## 工作区边界

`docs/`、`.pi/`、`reference/`、`libs/`、`scripts/` 等环境内容由维护者管理。`your_mods/` 是玩家成果区；**源码写入权限由当前 session 的角色和 mod 绑定决定**，不是整个 `your_mods/` 都可写。Game Helper 不编辑源码；获准的环境检查/校验工具可能写自己的缓存或编译产物，不授予通用写权限。
