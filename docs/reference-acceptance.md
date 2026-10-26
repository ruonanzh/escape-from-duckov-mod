# reference 验收记录

本文件分开记录实机行为与离线构建。可编译不等于行为正确。
历史实机记录未写版本或日期时，本文件保留「未记录」，不得改用当前游戏版本补齐。

## example_mod

- 对应 SKILL：`.pi/skills/mod-creator/SKILL.md`。
- 最后实机验证版本：未记录。
- 最后实机验证日期：未记录。
- 行为与边界：历史记录没有单独的加载验收证据。当前只确认它是最小骨架，不能声称已完成游戏内验收。
- 离线构建：2026-10-26，游戏 2.3.30，SDK 9.0.319。删除旧 DLL 后构建通过；缺少 `DUCKOV_DIR` 的负向夹具通过。

## weapon_model

- 对应 SKILL：`.pi/skills/replace-gun-model/SKILL.md`。
- 最后实机验证版本：未记录。
- 最后实机验证日期：未记录。
- 行为与边界：历史记录确认 AK103 与 MP5 的模型、挂点和图标替换。配件与原枪结构必须保留。
- 离线构建：2026-10-26，游戏 2.3.30，SDK 9.0.319。删除旧 DLL 后构建通过；缺少 `DUCKOV_DIR` 的负向夹具通过。

## item_model

- 对应 SKILL：`.pi/skills/replace-item-model/SKILL.md`。
- 最后实机验证版本：未记录。
- 最后实机验证日期：未记录。
- 行为与边界：历史记录确认背包、防具、耳机和没有 `ItemGraphic` 的物品。重新实例化与关卡重建后，动态条目仍需有效；未配置物品必须保持原样。
- 离线构建：2026-10-26，游戏 2.3.30，SDK 9.0.319。删除旧 DLL 后构建通过；缺少 `DUCKOV_DIR` 的负向夹具通过。

## character_model

- 对应 SKILL：`.pi/skills/replace-character/SKILL.md`。
- 最后实机验证版本：未记录。
- 最后实机验证日期：2026-10-15；该日期来自角色研究记录，版本号未写入原记录。
- 行为与边界：历史记录确认角色替换、刚性搬运和尺寸调整。手臂不得脱离身体；枪、背包与护甲不得被旧角色隐藏逻辑移除。
- 离线构建：2026-10-26，游戏 2.3.30，SDK 9.0.319。删除旧 DLL 后构建通过；缺少 `DUCKOV_DIR` 的负向夹具通过。

## 复验

运行 `node --experimental-strip-types scripts/test-reference-builds.mjs`。
缺少 SDK 或游戏 DLL 时，脚本输出 SKIP。SKIP 不表示构建通过。
