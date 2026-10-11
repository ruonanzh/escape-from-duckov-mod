# Escape From Duckov Mod 工作区

这是 **Escape From Duckov**（Team Soda）的 mod 环境。产物是 **C# DLL 与 `info.ini`**。

## 资料导航

- `docs/game.md` 与 `docs/items.md`：游戏机制与物品。`docs/mod-api.md`：mod API。`docs/asset-mods.md`：资源替换方法。
- `docs/api/`：生成的 API 签名。`docs/data/`：数据、本地化 CSV、资源清单。按问题检索，不得全量读入。这些是版本快照。快照不保证对应玩家最新游戏版本。
- `docs/guides/`：**游戏数据查询指南**（物品、武器、图腾、任务、地图、生物、天赋、建筑、商店、增益 → 数据在哪，以及可复制命令）。**查游戏数据时，必须先读这里，不得靠试**。入口是 `docs/guides/00-overview.md`。
- `reference/example_mod/`、`reference/weapon_model/`、`reference/item_model/`：可编译样例。后两个是「替换枪械模型」与「替换物品模型」的完整答案。`libs/mod-kit/`：**模型运行时库**。它读 GLB，产出 Mesh 与贴图，把模型换到手持武器上，把模型换到物品上，换图标，找玩家与手持物，读 config 的 JSON。`libs/`：编译与分发依赖，含 `0Harmony.dll`。产物规范见 `.pi/skills/mod-creator/SKILL.md`。mod-kit 的完整签名用 `rg "public static" libs/mod-kit/` 现查。
- `mod-repo.json`：平台路径与依赖声明。`game.version` 记录数据层快照对应的版本。`workshop.supported` 声明本游戏是否有创意工坊。`workshopDir` 只是只读参考，不参与安装。判定规则见 `.pi/skills/setup-workspace/SKILL.md`。
- `.pi/skills/`：按职责分开的技能：`setup-workspace`、`mod-creator`、`mod-installer`、`replace-gun-model`、`replace-item-model`。每个技能的用途写在它自己的 description 里。Pi 会把 description 常驻上下文，任务匹配时读对应那份。本工作区的步骤指导都在这些技能里，不在产品提示词里。
- `.pi/extensions/`：工具。运行时与 SDK 检查：`check_runtime`、`install_runtime`。游戏路径：`check_game_paths`、`set_game_dir`、`try_set_game_dir`。游戏数据与 API 探针：`inspect_game_data`、`inspect_game_api`。mod 校验与装机：`validate_mod`、`install_mod`。模型与图标生成：`generate_model`、`generate_image`（走 Tripo）。一切以实际返回的执行状态为准。
- `scripts/`：维护者刷新数据层的工具。玩家与 agent 不得运行。运行时准备与数据层维护是两件事。

## 运行时状态文件

`.gamer-agent.local.json`（**本工作区目录下**）是路径与运行时状态的**唯一来源**，由 `.pi/extensions/` 的工具读写。**只认本工作区的这一份**。机器上别处可能有同名文件（别的克隆或历史残留）。用它会得出错误结论。
**是否就绪以文件里记录的值与工具返回为准**：`ls` 看到目录存在，不等于已记录或已就绪。工作区面板、`validate_mod`、`install_mod` 读的都是记录。字段含义见 `.pi/skills/setup-workspace/SKILL.md`。

## 工作区边界

环境内容由维护者管理。环境内容包括 `docs/`、`.pi/`、`reference/`、`libs/`、`scripts/`。`your_mods/` 是玩家成果区。**源码写入权限由当前 session 的角色与 mod 绑定决定**。不是整个 `your_mods/` 都可以写。Game Helper 不得编辑源码。获准的环境检查与校验工具可以写自己的缓存或编译产物。这些工具不授予通用写权限。
