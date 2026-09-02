# Escape From Duckov Mod 工作区

Escape From Duckov 的 modding 环境。玩家（和 AI agent）在这个环境里就能直接做 C# mod：读 `docs/` + `specs/` 了解格式，在 `your_mods/<mod名>/` 下写 C# 代码，编译成 DLL，用 `validate_mod` 工具校验。

结构说明见 `AGENTS.md`；做 mod 的详细操作手册见 `.pi/skills/mod-authoring/SKILL.md`。

**维护者**：游戏 patch 后，`python3 scripts/refresh.py` 刷新 `docs/api` + `docs/data`，见 `scripts/README.md`。
