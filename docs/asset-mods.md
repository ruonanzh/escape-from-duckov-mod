# 资源替换与增加（Asset Mods）

本指南说明如何做「替换/增加音乐、音效、贴图、模型」类 mod。资源 API 签名见 `docs/api/unity-resources.md`；游戏现有资源用 `inspect_game_data` **现查**（速查见 `cookbook.md` 第 13 节）。

## 资源系统概览

| 资源类型 | 游戏怎么存 | mod 怎么加载自己的 |
|---|---|---|
| 美术（贴图/精灵/网格/材质） | Unity Resources（`.assets`，靠 m_Name 引用） | **AssetBundle**（mod 打包）+ `AssetBundle.LoadFromFile` |
| 音效 | **FMOD**（`StreamingAssets/Master.bank`） | `AudioSource` + `AudioClip`（Unity 原生），或 FMODUnity 加载自己的 bank |

**关键**：游戏用 Resources 系统（不是 Addressables），资源通过 m_Name 引用。mod 用 AssetBundle 打包自己的资源，运行时加载后替换引用。

## 1. 加载 mod 的资源（AssetBundle）

mod 的 AssetBundle 由作者（或用工具）打包，放进 mod 文件夹。运行时加载：

```csharp
// 从 mod 文件夹加载 AssetBundle
var bundlePath = Path.Combine(modFolder, "myassets.bundle");
var bundle = AssetBundle.LoadFromFile(bundlePath);

// 加载资源
Texture2D tex = bundle.LoadAsset<Texture2D>("MyTexture");
Sprite sprite = bundle.LoadAsset<Sprite>("MySprite");
AudioClip clip = bundle.LoadAsset<AudioClip>("MySound");
```

## 2. 替换美术

### 替换物品图标

```csharp
var item = ItemAssetsCollection.InstantiateSync(254); // typeID 用 inspect_game_data 现查
item.Icon = mySprite;  // Item.Icon 是 Sprite（见 docs/api/ItemStatsSystem.md）
```

### 替换材质贴图

```csharp
var material = someRenderer.material;
material.mainTexture = myTexture;  // Texture2D
```

### 替换模型网格

```csharp
var meshFilter = GetComponent<MeshFilter>();
meshFilter.mesh = myMesh;  // Mesh
```

## 3. 增加美术

```csharp
// 创建新对象显示贴图
var go = new GameObject("MySprite");
var renderer = go.AddComponent<SpriteRenderer>();
renderer.sprite = mySprite;
```

## 4. 音效

### 方式 A：Unity AudioSource（简单）

```csharp
var source = gameObject.AddComponent<AudioSource>();
source.clip = myAudioClip;   // 从 AssetBundle 加载
source.Play();
```

### 方式 B：FMOD（游戏用 FMOD，高级）

```csharp
// 加载自己的 FMOD bank
FMODUnity.RuntimeManager.LoadBank("MyBank");  // 见 docs/api/unity-resources.md FMODUnity

// 播放事件
var ev = FMODUnity.RuntimeManager.CreateInstance("event:/MyEvent");
ev.start();
```

## 5. 查找要替换的资源

- **美术**：用 `inspect_game_data` 的 `search`（按名字）或 `export, class=Texture2D, field=["m_Name"]`（按类）
- **物品**：`export, class=Item, field=["typeID","displayName"]`；中文名去游戏自带的本地化里按 key 查
- **音效**：游戏用 FMOD（`StreamingAssets/Master.bank`），事件名需 FMOD 工具解析；mod 做音效主要用方式 A（AudioSource）加载自己的音频

## 6. 资源 API 签名

`docs/api/unity-resources.md` 含 23 个类型的完整签名（DeclaredOnly）：`Resources`、`AssetBundle`、`Texture2D`、`Sprite`、`Material`、`Mesh`、`AudioSource`、`AudioClip`、`FMODUnity.*`。
