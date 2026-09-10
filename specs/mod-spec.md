# mod 产物说明

## 目录结构

每个 mod 是一个目录：`your_mods/<mod名>/`，包含：

- `<ModName>.csproj` — 编译配置（引用游戏 DLL + Harmony）
- `ModBehaviour.cs` — mod 入口类（继承 `Duckov.Modding.ModBehaviour`）
- `info.ini` — mod 元信息
- `preview.png` — 256×256 预览图（Workshop 上传用，可选）

编译产物：`<ModName>.dll` + `0Harmony.dll`（随 mod 分发）。

## info.ini

mod 元信息，字段：

- `name` — mod 名（也是命名空间和 dll 文件名）
- `displayName` — 显示名
- `description` — 描述
- `tags` — Workshop 标签，逗号分隔，可选值见下
- `version` — 版本号
- `publishedFileId` — Workshop 的 mod ID（上传后回填）

## ModBehaviour

mod 入口类，继承 `Duckov.Modding.ModBehaviour`：

```csharp
namespace <ModName>
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        void Awake() { /* 加载时执行 */ }
        void OnEnable() { /* 订阅游戏事件 */ }
        void OnDisable() { /* 取消订阅 */ }
    }
}
```

命名空间通常跟 mod 名一致，类名通常叫 `ModBehaviour`（游戏按 `<name>.ModBehaviour` 加载）。

## tags 可选值

Weapon / Equipment & Gear / Loot & Economy / Quality of Life / Cheats & Exploits / Visual Enhancements / Sound / Quest & Progression / Companion & NPC / Collectibles / Gameplay / Multiplayer & Co-op / Utility

> 字段的必填/格式/命名等约束由 `validate_mod` 工具校验——做错了会返回 FAIL + 定位到具体字段，照着改即可。
