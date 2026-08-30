# Escape From Duckov Mod 工作区

这是 **Escape From Duckov**（鸭子世界 PVES 生存 RPG，Team Soda 开发）的 modding 环境。mod 是 **C# 编译出的 DLL**（不是 JSON）。

## 目录

- `docs/`     游戏机制、物品数据、mod API
- `specs/`    mod 制作规范（info.ini schema、目录结构、命名）
- `reference/` 官方示例 mod（可编译、可过 lint 的正确答案）
- `lint/`     mod 校验工具（含编译校验）
- `tools/`    运行时检查/安装（check_runtime / install_runtime）
- `libs/`     Harmony 2.4.1（编译引用 + 随 mod 分发）
- `mod-repo.json` 机器可读配置（modType/游戏目录声明）
- `your_mods/` **唯一可写目录**，你的每个 mod 放这里（环境其余部分只读）

## 硬规则

mod 只能写到 `your_mods/<mod名>/`，环境的其它目录（docs/specs/lint/tools/reference/libs）只读。

## 做 mod（流程概览）

1. 先跑 `node tools/check_runtime.mjs` 确认环境就绪（dotnet + 游戏目录）
2. 读 `docs/mod-api.md` 了解可用 API，读 `specs/` 了解产物结构
3. 在 `your_mods/<mod名>/` 下创建 `<ModName>.csproj` + `ModBehaviour.cs` + `info.ini`
4. 编译：`dotnet build`（lint 会自动做），产出 `<ModName>.dll`
5. 跑 `node lint/check_mod.mjs your_mods/<mod名>/` 校验（exit 0 = 通过）

详细步骤、字段说明、常见错误见 skill：`.pi/skills/mod-authoring/`（做 mod 时先加载它）。
