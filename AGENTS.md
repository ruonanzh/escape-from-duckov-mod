# Escape From Duckov Mod 工作区

这是 **Escape From Duckov**（鸭子世界 PVES 生存 RPG，Team Soda 开发）的 modding 环境。mod 是 **C# 编译出的 DLL**（不是 JSON）。

## 目录

- `docs/`     游戏机制（game.md）、物品数据（items.md）、mod API（mod-api.md）、**资源替换指南（asset-mods.md）**
- `docs/api/` **游戏 API 参考**（反射 dump 的签名，只读，用 grep 检索；含 unity-resources.md = Unity 资源 API）
- `docs/data/` **游戏数据表**（物品/UI/任务等 CSV 含本地化；resources.csv = 美术资源清单，只读，grep 检索）
- `specs/`    mod 制作规范（info.ini schema、目录结构、命名）
- `reference/` 官方示例 mod（可编译、可过 lint 的正确答案）
- `lint/`     mod 校验工具（含编译校验）——**你做 mod 时跑**
- `.pi/extensions/` 运行时工具（registerTool 注册 `check_runtime` / `install_runtime`）——**你做 mod 前直接调这两个工具**
- `scripts/`  **维护工具**（inspect_game / extract_data，repo 作者跑；**你不要跑**，会失败）
- `libs/`     Harmony 2.4.1（编译引用 + 随 mod 分发）
- `mod-repo.json` 机器可读配置（modType/游戏目录声明）
- `your_mods/` **唯一可写目录**，你的每个 mod 放这里（环境其余部分只读）

## 硬规则

mod 只能写到 `your_mods/<mod名>/`，环境的其它目录（.pi/docs/specs/lint/reference/libs/scripts）只读。

## 做 mod（流程概览）

1. 先调 `check_runtime` 工具确认环境就绪（dotnet + 游戏目录）；缺失则调 `install_runtime`
2. 用 **grep** 在 `docs/api/`（接口签名）和 `docs/data/`（物品/文本）里检索你要改的东西，定位到具体 API
3. 读 `specs/` 了解产物结构，在 `your_mods/<mod名>/` 下创建 `<ModName>.csproj` + `ModBehaviour.cs` + `info.ini`
4. 编译：`dotnet build`（lint 会自动做），产出 `<ModName>.dll`
5. 调 `validate_mod` 工具校验（返回 PASS 即通过）

详细步骤、字段说明、常见错误见 skill：`.pi/skills/mod-authoring/`（做 mod 时先加载它）。
