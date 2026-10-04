# Escape From Duckov Mod 工作区

这是 **Escape From Duckov**（Team Soda）的 modding 环境，产物为 **C# DLL + info.ini**。

## 资料导航

- `docs/game.md`、`docs/items.md`：游戏机制与物品；`docs/mod-api.md`：mod API；`docs/asset-mods.md`：资源替换方法。
- `docs/api/`：生成的 API 签名；`docs/data/`：数据/本地化 CSV、资源清单。按问题检索，不全量读入；这些是版本快照，不保证对应玩家最新游戏版本。
- `docs/guides/`：**游戏数据查询指南**（物品/武器/图腾/任务/地图/生物/天赋/建筑/商店/增益… → 数据在哪 + 可复制命令）。**要查内容数据先读这里，不要靠试**；入口 `docs/guides/00-overview.md`。
- `reference/example_mod/`：可编译样例；`reference/mod-kit/`：**模型运行时库**（`GltfLoader` 读 GLB / 几何 / 贴图 / Unity 适配 / `ItemModelBinder` 换图形 / `GameApi` 找角色与武器）；`reference/bundle_swap/`：运行时把 GLB 换到武器上的验证 demo；`libs/`：编译/分发依赖；产物规范见 `.pi/skills/mod-creator/SKILL.md`。
- `mod-repo.json`：平台路径和依赖声明；`game.version` 记录数据层快照对应版本。 其中 `workshop.supported` 声明本游戏**有没有创意工坊**；`workshopDir` 只是只读参考（不参与安装），判定规则见 `.pi/skills/setup-workspace/SKILL.md`。
- `.pi/skills/`：按职责分开的技能 —— `setup-workspace` / `mod-creator` / `mod-installer`（**何时用 / 管什么写在各自的 description 里** —— Pi 会把它们常驻上下文，任务匹配时读对应那份）。本 repo 的步骤指导都在这些技能里，不在产品提示词里。
- `.pi/extensions/`：环境检查、安装指引、mod 校验、装进游戏（`install_mod`）工具；以实际返回的执行状态为准。
- `scripts/`：维护者刷新数据层的工具，玩家/agent 不运行；运行时准备与数据层维护是两件事。

## 运行时状态文件

`.gamer-agent.local.json`（**本工作区目录下**）是路径与运行时状态的**唯一来源**，由 `.pi/extensions/` 的工具读写。**只认本工作区的这一份** —— 机器上别处（别的克隆、历史残留）可能有同名文件，用它会得出错误结论。
**是否就绪以文件里记录的值 + 工具返回为准**：`ls` 看到目录存在 ≠ 已记录/已就绪（工作区面板、`validate_mod`、`install_mod` 读的都是记录）。字段含义见 `.pi/skills/setup-workspace/SKILL.md`。

## 工作区边界

`docs/`、`.pi/`、`reference/`、`libs/`、`scripts/` 等环境内容由维护者管理。`your_mods/` 是玩家成果区；**源码写入权限由当前 session 的角色和 mod 绑定决定**，不是整个 `your_mods/` 都可写。Game Helper 不编辑源码；获准的环境检查/校验工具可能写自己的缓存或编译产物，不授予通用写权限。
