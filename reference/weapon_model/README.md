# weapon_model（示例 mod）—— 替换某把武器的模型

**完整正确做法**：整个 mod = 一句配置 + 一个 GLB。

## 用法

```
WeaponModelSwap/                 ← 装进游戏的目录名要和 info.ini 的 name 一致
  ├─ WeaponModelSwap.dll
  ├─ info.ini
  ├─ config.json                 ← { "target": "MP5", "model": "gun.glb" }
  └─ gun.glb                     ← 你的模型（Tripo 导出的 GLB；建议 export_orientation="-x"）
```

`config.json`：
| 字段 | 说明 |
|---|---|
| `target` | 武器名的一段（大小写不敏感 ✓ `MP5` 会匹配 `SMG_MP5_Normal`）|
| `typeIDs` | 或者直接给 typeID（如 `[655]`；用 `inspect_game_data` 查）|
| `model` | 本目录下的 GLB（相对路径或绝对路径 ✓）|

## 能力在库里（`libs/mod-kit`）

| 文件 | 干什么 |
|---|---|
| `GltfLoader.cs` | 读 GLB → `Mesh` + 贴图（右手系→左手系：取反 X / 反转绕序 / UV 翻转；握把归零）|
| `WeaponModel.cs` | 换到枪上：关旧零件（`WPN_*`/`HideIf_*` ✓ 留 `ShowIf_*` 与特效）→ 挂到原枪身变换帧 → **对齐**（用游戏那把枪的包围盒当基准）→ 克隆游戏材质 + 换贴图 |
| `GameApi.cs` | 找玩家 / 手持物 / 层级 |
| `Json.cs` | 读 `config.json` |

## 编译与验证

```bash
export DUCKOV_DIR="<游戏安装目录>"
dotnet build -c Release        # 产物 bin/Release/WeaponModelSwap.dll
```
装进游戏后进关卡，**手里拿目标武器**（如 MP5）→ 模型会在 2 秒内换掉。
日志看 `~/Library/Logs/...` 或游戏 Player.log：`[WeaponModel] …` 会写出命中/隐藏数/锚点/材质/包围盒。
