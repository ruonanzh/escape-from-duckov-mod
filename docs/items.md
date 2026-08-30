# 物品数据

Duckov 的物品由 `ItemStatsSystem` 管理，通过 typeID（整数）标识。做 mod 时用 `ItemAssetsCollection.InstantiateAsync(typeID)` 生成物品实例。

## 物品分类（wiki）

- **weapons** 武器
- **equipment** 装备
- **food** 食物
- **medicine** 药品
- **creatures** 生物
- **buffs** 增益
- **keys** 钥匙
- **totems** 图腾

> 具体物品的 typeID 与字段见官方 wiki（https://escapefromduckov.net/wiki）。做「添加新物品」类 mod 前，先查 wiki 确认目标物品的 typeID。

## 物品 API（ItemStatsSystem）

```csharp
using ItemStatsSystem;

// 生成物品实例（typeID 见 wiki）
Item glick = ItemAssetsCollection.InstantiateAsync(254);

// 发物品给玩家
ItemUtilities.SendToPlayer(glick);                  // 到玩家储物
ItemUtilities.SendToPlayerCharacter(glick);         // 到角色身上
ItemUtilities.SendToPlayerCharacterInventory(glick);// 到角色背包

// 物品关系
item.IsInPlayerCharacter();  item.IsInPlayerStorage();
item.Detach();  // 卸下物品
```
