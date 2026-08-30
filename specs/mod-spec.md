# mod 制作规范

## 目录结构

每个 mod 是一个目录：`your_mods/<mod名>/`，包含：

- `<ModName>.csproj` — 编译配置（引用游戏 DLL + Harmony）
- `ModBehaviour.cs` — mod 入口类（继承 `Duckov.Modding.ModBehaviour`）
- `info.ini` — mod 元信息
- `preview.png` — 256×256 预览图（Workshop 上传用，可选）

编译产物：`<ModName>.dll` + `0Harmony.dll`（随 mod 分发）。

## info.ini

```ini
name = MyMod
displayName = 我的 Mod
description = 这个 mod 做了什么

tags = Quality of Life
version = 1.0
publishedFileId = 1234567890
```

| 字段 | 必填 | 说明 |
|---|---|---|
| `name` | ✅ | mod 名，**= 命名空间 = dll 文件名**，合法 C# 命名空间（`^[A-Za-z_][A-Za-z0-9_]*$`） |
| `displayName` | ✅ | 显示名 |
| `description` | ✅ | 描述 |
| `tags` | 可选 | Workshop 标签，逗号分隔 |
| `version` | 可选 | 版本号 |
| `publishedFileId` | 可选 | Workshop 的 mod ID（上传后回填） |

> 上传 Workshop 时 `info.ini` 会被覆盖，别存多余字段。

## ModBehaviour

```csharp
namespace MyMod
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        void Awake() { /* 加载时执行 */ }
        void OnEnable() { /* 订阅游戏事件 */ }
        void OnDisable() { /* 取消订阅 */ }
    }
}
```

- **命名空间 = info.ini 的 name**，类名固定 `ModBehaviour`
- 游戏加载 `<name>.ModBehaviour`，命名空间/类名不匹配则加载失败

## 命名约定

- mod 名 / 命名空间：合法 C# 标识符（`^[A-Za-z_][A-Za-z0-9_]*$`），习惯用 PascalCase（如 `DisplayItemValue`）
- dll 文件名必须 = mod 名（`<ModName>.dll`）

## tags 可选值

Weapon / Equipment & Gear / Loot & Economy / Quality of Life / Cheats & Exploits / Visual Enhancements / Sound / Quest & Progression / Companion & NPC / Collectibles / Gameplay / Multiplayer & Co-op / Utility

> 本规范的可执行版是 `lint/check_mod.mjs`——这里每条约束，lint 都要能校验。
