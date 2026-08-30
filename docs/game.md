# Escape From Duckov

Escape From Duckov 是 PVES 生存 RPG（鸭子世界）。玩家搜刮资源、建藏身处、升级装备，在敌对鸭子的世界里求生。

- Steam appid：**3167020**，支持 **Steam Workshop**
- 官方 mod 仓库：https://github.com/xvrsl/duckov_modding
- 官方 wiki：https://escapefromduckov.net/wiki

## mod 机制

mod 是 **C# 类库编译出的 DLL**（netstandard2.1）。游戏扫描 `Duckov_Data/Mods/` 和 Steam Workshop 订阅，通过 `info.ini` 的 `name` 作命名空间，加载 `<name>.ModBehaviour` 类（继承 `Duckov.Modding.ModBehaviour`，即 MonoBehaviour）。

mod 产物：`<ModName>.dll` + `0Harmony.dll`（Harmony 2.4.1，patch 用）+ `info.ini` + `preview.png`（256×256）。

## 数据流

- 编译引用游戏安装目录的 `TeamSoda.*.dll`、`ItemStatsSystem.dll`、`Unity*.dll`（游戏 DLL，mod 作者本机提供，不进 repo）
- `0Harmony.dll` 从 repo 的 `libs/` 引用，编译后随 mod 分发
- 物品、角色、对话等 API 见 `docs/mod-api.md`
