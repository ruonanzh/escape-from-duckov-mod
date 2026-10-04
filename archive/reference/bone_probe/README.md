# BoneProbe — 角色骨骼挂载参考实现

把**运行时新建的几何**挂到角色骨骼上，并周期性打日志。角色模型的挂载方式以这份实现为准。

## 它做了三件必须处理的事

| # | 事 | 代码 |
|---|---|---|
| 1 | **按骨骼名找骨骼**（YSM 的 `bones[].name` 用游戏骨骼名即可 1:1 挂载）| `FindDeep(model.transform, "Hand.R")` |
| 2 | **层跟角色渲染器**（模型根在 `layer 0`，但相机不渲染 `layer 0`）| `SetLayerRecursive(cube, 身体 SkinnedMeshRenderer 的层)` |
| 3 | **克隆游戏现有材质**（身体材质 `Skin`，shader `SodaCraft/SodaCharacter` 是自定义 URP shader）| `new Material(smr.sharedMaterial)` 后改色 |

另外演示了备用路径：`CharacterModel.rightHandSocket` 是 `private` → 反射取 socket。

## 编译

```bash
export DUCKOV_DIR="<游戏安装目录>"        # 例如 ~/Library/Application Support/Steam/steamapps/common/Escape from Duckov
cd reference/bone_probe
dotnet build -c Release
```

## 装进游戏看效果

把 `bin/Release/BoneProbe.dll`、`info.ini`、`preview.png` 复制到游戏的 `Mods/` 目录
（macOS：`<游戏安装目录>/Duckov.app/Contents/Mods/BoneProbe/`；Windows：`Duckov_Data/Mods/BoneProbe/`），
启动游戏、载入一局：右手会出现一个橙色方块，跟着手走。

日志（同时写入 `Player.log`）：

```
~/Library/Logs/TeamSoda/Duckov/Player.log         # macOS
/tmp/bone_probe.log                                # 本实现的详细日志
```

日志里能看到：模型层级、骨骼路径、身体渲染器的层与材质、相机 `cullingMask`、
以及每 0.5 秒的骨骼/方块世界坐标（方块局部坐标恒为 `(0,0,0)` 表示确实挂在骨骼下）。

## 注意

这是**参考实现**，不是产品 mod：它只做挂载演示，不含 YSM 解析、贴图、动画与配置读取。
