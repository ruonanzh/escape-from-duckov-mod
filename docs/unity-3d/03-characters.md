# 03 · 角色

## ⚠️ 先说边界：角色**不做**参数化建模

角色模型是**骨骼 + 蒙皮**（`SkinnedMeshRenderer`），不是静态几何：

```
export --class SkinnedMeshRenderer --field "m_GameObject.#name" --rows 5
→ 73 row(s)；例：Player_Duck_Head / WPN_AHBow / Cone …

search --class GameObject --pattern "CharacterModel"
→ 147 match(es)；例：0_CharacterModel_Custom_Killa / _Tagilla / _Boss_Alex / _Enemy_SnowMan …
```

`Mesh` 资产里与骨骼相关的字段（真实 dump）：

```
m_BindPose / m_BoneNameHashes / m_RootBoneNameHash / m_BonesAABB / m_SkinnedMeshRenderer 侧 m_Bones
```

→ **参数化生成一张蒙皮网格 + 绑定骨骼 + 权重**，工程上不成立（还要匹配游戏动画的骨骼命名）。

## 那角色类能做什么

| 可做 | 做法 |
|---|---|
| **换整体模型**（玩家/NPC/宠物）| ✅ **走社区框架 DCM + YSM** —— 产 **YSM 文本模型**（`ysm.json`），放进 `ModConfigs/DuckovCustomModel/Models`，**不需要 AssetBundle**；需玩家先装 DCM + HarmonyLib。详见 [`05-community.md`](05-community.md) |
| **挂饰 / 附件**（帽子、背包挂件、武器挂件） | 生成静态 mesh，挂到角色的 socket / 模型树的子节点 |
| **换贴图 / 换材质（改色）** | 直接改 `Material`（克隆原材质后改色），不走几何 |

⚠️ 不要自己写角色渲染代码（蒙皮/骨骼/动画）；也不要为了换角色模型去打包 AssetBundle。

`CharacterSubVisuals` 的字段里已经有现成入口（真实 dump）：

```
renderers[]   particles[]   lights[]   sodaPointLights[]   mainModel
```

`mainModel` 指向角色的主模型 —— **换整体模型时以它为锚点**。

## 提取命令

```
action=search  class=GameObject          pattern="CharacterModel"
action=export  class=SkinnedMeshRenderer field="m_GameObject.#name"  rows=20
action=dump    class=GameObject          name="0_CharacterModel_Custom_Killa"  follow=true  depth=4
```

## 待办

- [ ] 确认「挂饰」实际挂点（角色模型树里 socket 的命名 / `sockets` 字段）
- [ ] YSM 模型的仓库/样例找一份（用于确定 `ysm.json` 的几何写法）
