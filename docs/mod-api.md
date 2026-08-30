# mod API（Notable APIs）

完整文档见官方 repo 的 `Documents/NotableAPIs.md`。这里整理做 mod 常用的 API。

## 物品相关

```csharp
using ItemStatsSystem;

// 生成物品（typeID 见 docs/items.md / 官方 wiki）
Item item = ItemAssetsCollection.InstantiateAsync(254);
Item itemSync = ItemAssetsCollection.InstantiateSync(254);

// 发物品给玩家
ItemUtilities.SendToPlayer(item, dontMerge: false, sendToStorage: true);
ItemUtilities.SendToPlayerCharacter(item);
ItemUtilities.SendToPlayerCharacterInventory(item);

// 物品关系 / 插槽
item.IsInPlayerCharacter();
item.IsInPlayerStorage();
main.TryPlug(part, emptyOnly: false);  // 配件插到主件
item.Detach();                          // 卸下物品
```

## 角色相关

```csharp
// CharacterMainControl 是角色核心
character.SetTeam(Teams.TeamA);
```

## 对话相关

```csharp
using Dialogues;
using Duckov.UI.DialogueBubbles;

// 底部字幕（异步，注意实例间干扰）
DialogueUI.instance.DoSubtitle(content);

// 对话气泡
DialogueBubblesManager.Show("Hello world!", someGameObject.transform);
```

## 日志

```csharp
Debug.Log("...");  // Unity 日志
// macOS: ~/Library/Logs/TeamSoda/Duckov/Player.log
// Windows: %USERPROFILE%\AppData\LocalLow\TeamSoda\Duckov\Player.log
```
