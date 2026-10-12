// Duckov 3D 编辑器 —— 在右侧栏里用 **Unity 坐标系** 查看 .glb 模型。
//
// 为什么不能"直接 load 就完事"：
//   游戏里的 `libs/mod-kit/GltfLoader.cs:80-91` 读 glb 时做了四件事 ——
//     ① 顶点 x 取反 · ② 法线 x 取反 · ③ UV 的 v 取反 · ④ 三角面绕序反转
//   合起来 = 一次 **X 镜像**（glTF 右手 → Unity 左手，与 Unity 官方导 glTF 同一套）。
//   不照做 → 预览里看到的和进游戏后看到的是**左右相反**的两个模型。
//   ④ 只需 `scale.x = -1`（three 会把法线/绕序一起处理），③ 不用管（three 与 glTF 同规，
//   Unity 侧的 v 翻转是它自己 UV 原点在下方的补偿，两边最终一致）。
//
// 另外两步（游戏里也在做，`GltfLoader` / `WeaponModel`）：
//   · 朝向：`front` 声明 —— `auto` 不转 · `up` 最长轴转 +Y（近战刀）
//   · 握把归零：枪托端起 8%~35% 区间的最低点移到原点（`GuessGrip`，同一套规则）
//
// ⚠️ 本文件是**编辑器自己的实现** —— 与 C# 侧规则保持一致，改一边记得看另一边。

(function () {
  "use strict";

  // ── 桥 ──────────────────────────────────────────────────────────────────────
  var pending = {};
  var reqSeq = 0;
  var state = {
    fileName: "",
    mode: "view",
    revision: null,
    loaded: null,      // { verts:[[x,y,z],...], tris:[i,...] } 原始 glb 坐标
    root: null,        // THREE.Group：镜像 + 朝向 + 归零 + 缩放
    model: null,       // 内层：gltf.scene
    frame: null,       // 包围盒（未缩放前，游戏坐标）
    box: null
  };

  function post(msg) { parent.postMessage(msg, "*"); }
  function fail(message, code) { showError(message); post({ type: "editor:error", requestId: msg_of("req"), code: code || "EDITOR_ERROR", message: message }); }
  function msg_of(k) { return pending[k]; }

  function readFile() {
    return new Promise(function (resolve, reject) {
      var id = "r" + (++reqSeq);
      pending[id] = resolve;
      pending[id + ":err"] = reject;
      post({ type: "editor:readFile", requestId: id });
    });
  }

  function showError(message) {
    var el = document.getElementById("err");
    el.style.display = "block";
    el.textContent = message;
  }

  window.addEventListener("message", function (ev) {
    var d = ev.data || {};
    if (d.type === "editor:init") {
      state.fileName = (d.file && d.file.name) || "";
      state.mode = d.mode || "view";
      document.getElementById("title").textContent = "Duckov 3D 编辑器 · " + state.fileName;
      start();
    } else if (d.type === "editor:fileContent") {
      var fn = pending[d.requestId];
      if (fn) { delete pending[d.requestId]; fn(d); }
    } else if (d.type === "editor:error") {
      var rej = pending[d.requestId + ":err"];
      if (rej) { delete pending[d.requestId + ":err"]; rej(new Error(d.message || d.code)); }
    } else if (d.type === "editor:theme") {
      // 只读预览不区分主题（背景固定浅色，与游戏截图一致）
    }
  });

  // ── 与 C# 侧同源的规则（改这里记得看 GltfLoader.cs） ───────────────────────

  function bounds(points) {
    var mn = [Infinity, Infinity, Infinity], mx = [-Infinity, -Infinity, -Infinity];
    for (var i = 0; i < points.length; i++) {
      for (var c = 0; c < 3; c++) {
        if (points[i][c] < mn[c]) mn[c] = points[i][c];
        if (points[i][c] > mx[c]) mx[c] = points[i][c];
      }
    }
    return { mn: mn, mx: mx, span: [mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2]] };
  }

  function longAxis(span) {
    return span[0] > span[1] ? (span[0] > span[2] ? 0 : 2) : (span[1] > span[2] ? 1 : 2);
  }

  /** `GltfLoader.GuessGrip`：枪托端（−Z 侧）起 8%~35% 区间里的**最低点**（返回要减掉的位移） */
  function guessGrip(points) {
    if (!points.length) return [0, 0, 0];
    var b = bounds(points);
    var ax = longAxis(b.span);
    var lo = b.mn[ax], sp = b.span[ax];
    var l2 = lo + sp * 0.08, h2 = lo + sp * 0.35;
    if (l2 > h2) { var t = l2; l2 = h2; h2 = t; }
    var low = null;
    for (var i = 0; i < points.length; i++) {
      var p = points[i];
      if (p[ax] < l2 || p[ax] > h2) continue;
      if (!low || p[1] < low[1]) low = p;
    }
    if (!low) return [0, 0, 0];
    var g = [(b.mn[0] + b.mx[0]) * 0.5, low[1] + 0.02, (b.mn[2] + b.mx[2]) * 0.5];
    if (ax === 0) g[0] = low[0];
    else if (ax === 2) g[2] = low[2];
    return g;
  }

  // ── 场景 ────────────────────────────────────────────────────────────────────
  var renderer, scene, camera, controls, grid, hull, axes;

  /**
   * ⭐ 自建"摄影棚"环境贴图（给 PBR 做 IBL ✓）。
   *
   * 为什么不用 three 自带的 `RoomEnvironment`：那是一间**全白**的屋子 ✗ ⇒
   * 环境反射一片白 ⇒ 金属件被**洗白**、看不出材质（会被误判成"模型很糟"✗）。
   * 这里用**深色底 + 三块面光**（顶柔光 ✓ 侧暖光 ✓ 背冷光 ✓）⇒ 金属面出现**渐变反射与高光条** ✓
   * 立体感和"成品感"都来自这里 ✓（不接 ORM 贴图 ✓ 颜色仍然只由 baseColor 决定 ✓）。
   */
  function buildStudioEnv(pmrem) {
    var s = new THREE.Scene();
    s.background = new THREE.Color(0x2b2f35);
    function panel(color, intensity, w, h, pos, rot) {
      var m = new THREE.Mesh(
        new THREE.PlaneGeometry(w, h),
        new THREE.MeshBasicMaterial({ color: new THREE.Color(color).multiplyScalar(intensity), side: THREE.DoubleSide })
      );
      m.position.set(pos[0], pos[1], pos[2]);
      m.rotation.set(rot[0], rot[1], rot[2]);
      s.add(m);
    }
    panel(0xffffff, 3.0, 6, 6, [0, 4, 0], [-Math.PI / 2, 0, 0]);      // 顶部柔光
    panel(0xffe6c0, 2.2, 5, 4, [-4, 1.6, 2], [0, Math.PI * 0.42, 0]); // 左前暖光
    panel(0xc6dcff, 1.3, 4, 5, [4, 1.0, -1.2], [0, -Math.PI * 0.42, 0]); // 右侧冷光
    panel(0xffffff, 2.4, 1.4, 6, [0, 1.4, -4.6], [0, 0, 0]);          // 背后窄亮条（勾边）
    var tex = pmrem.fromScene(s, 0.02).texture;
    pmrem.dispose();
    return tex;
  }

  function initScene() {
    var host = document.getElementById("view");
    renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio || 1);
    renderer.setSize(window.innerWidth, window.innerHeight);
    renderer.setClearColor(0xeef0f2, 1);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 0.98;
    // ⚠️ **不开阴影** ✓（用户："影子没必要"）—— 三点光只做明暗与轮廓 ✓ 不投影 ✓
    host.appendChild(renderer.domElement);

    scene = new THREE.Scene();
    // ⭐ 环境贴图必给 ✗ —— 否则 PBR 的金属部分是**全黑**（会造成"模型很糟"的假象）
    var pmrem = new THREE.PMREMGenerator(renderer);
    scene.environment = buildStudioEnv(pmrem);       // ⭐ 自建摄影棚（不是全白的 RoomEnvironment ✗）
    scene.environmentIntensity = 1.0;              // 环境反射稍强 ⇒ 曲面更有立体感 ✓
    scene.add(new THREE.AmbientLight(0xffffff, 0.42));   // 只兜底 ✗ 主体交给方向光 ✓

    // ⭐ 三点光（摄影棚打法 ✓）：key 主光带阴影 ✓ · fill 补暗部 ✓ · rim 勾轮廓 ✓
    var key = new THREE.DirectionalLight(0xfff4e6, 1.85);   // 略暖 ✓
    key.position.set(-3.2, 5.2, 4.2);
    scene.add(key);

    var fill = new THREE.DirectionalLight(0xdfe8ff, 0.55);   // 略偏冷 ✓ 与暖主光形成对比 ✓
    fill.position.set(4.2, 1.6, 3.0);
    scene.add(fill);

    var rim = new THREE.DirectionalLight(0xffffff, 1.05);     // ⭐ 轮廓光（从后上方 ✓）= "高级感"的主要来源 ✓
    rim.position.set(2.0, 3.0, -5.0);
    scene.add(rim);

    camera = new THREE.PerspectiveCamera(35, window.innerWidth / window.innerHeight, 0.005, 500);
    controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.12;

    grid = new THREE.GridHelper(2, 20, 0x9aa3ab, 0xc9ced3);   // 2m / 20 格 ⇒ **每格 10cm**
    grid.position.y = 0;
    scene.add(grid);


    // 游戏坐标系标识：+X 红 · +Y 绿 · +Z 蓝（Unity：+Y 上 · +Z 前）
    axes = new THREE.AxesHelper(0.5);
    scene.add(axes);

    // ⭐ 原点标记 = **武器 0 点要对齐的那个点**（游戏角色拿枪的 socket）—— 白十字
    var o = new THREE.Vector3();
    var cs = [new THREE.Vector3(1, 0, 0), new THREE.Vector3(0, 1, 0), new THREE.Vector3(0, 0, 1)];
    var pts = [];
    cs.forEach(function (c) { pts.push(o.clone().addScaledVector(c, -0.035), o.clone().addScaledVector(c, 0.035)); });
    scene.add(new THREE.LineSegments(
      new THREE.BufferGeometry().setFromPoints(pts),
      new THREE.LineBasicMaterial({ color: 0xffffff, depthTest: false })
    ));

    window.addEventListener("resize", function () {
      camera.aspect = window.innerWidth / window.innerHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(window.innerWidth, window.innerHeight);
    });

    (function loop() {
      requestAnimationFrame(loop);
      controls.update();
      renderer.render(scene, camera);
    })();

    document.getElementById("views").innerHTML =
      viewLink("3/4", [-1.2, 0.6, 1.3]) + viewLink("前 +Z", [0, 0, 1]) +
      viewLink("右 +X", [1, 0, 0]) + viewLink("上 +Y", [0, 1, 0.0001]) +
      viewLink("后 −Z", [0, 0, -1]);
    document.getElementById("hint").innerHTML =
      "拖拽=转 · 滚轮=缩放 · 右键=平移<br>Unity 坐标系：<b>+Y</b> 上 · <b>+Z</b> 前（枪口）· <b>+X</b> 右 · 每格 <b>10cm</b>";
  }

  function viewLink(label, dir) {
    return '<a data-dir="' + dir.join(",") + '">' + label + "</a>";
  }

  function setView(dir) {
    var d = new THREE.Vector3(dir[0], dir[1], dir[2]).normalize();
    // ⭐ 对准**包围盒中心** ✗ 不是原点 —— 握把归零后模型主要伸向 +Z，对原点看会把它裁掉 ✗
    // ⚠️ 用的是 `viewBox`（打开的文件 **∪ 参照鸭子**）✗ 不是 `state.box` ——
    //    否则镜头只框打开的模型，参照鸭子永远在框外 ✗（曾经就是）
    var vb = state.viewBox || state.box;
    var center = vb ? vb.getCenter(new THREE.Vector3()) : new THREE.Vector3();
    var dist = (state.viewRadius || state.radius || 1) * 3.0;
    camera.position.copy(center).add(d.multiplyScalar(dist));
    controls.target.copy(center);
    controls.update();
  }

  // ── 组装（顺序与游戏一致） ─────────────────────────────────────────────────
  // ⭐ 金属/光泽的**数值**（贴图已按游戏清掉 ✓）。默认偏哑 ✓；
  //    调试/调参可用 URL：`?mr=0.6,0.35`（metal,rough）—— 只为对齐游戏观感用 ✓
  var MAT = { metal: 0.3, rough: 0.6 };   // ⭐ 定死 B（用户选的档 ✓）
  (function () {
    var q = new URLSearchParams(location.search).get("mr");
    if (!q) return;
    var v = q.split(",");
    if (v.length === 2) { MAT.metal = parseFloat(v[0]); MAT.rough = parseFloat(v[1]); }
  })();

  // ── 参照物：玩家鸭 ─────────────────────────────────────────────────────────
  // ⭐ 它**不参与**「物品」栏的任何启发式 ✗（长轴→+Z/+Y、枪口方向、握把归零都是**武器**的规则）。
  //    也**不做** X 镜像 ✗ —— 这条踩过坑，记下实测：
  //      鸭子 glb 是标准右手 glTF（前伸的右臂在 −X）⇒ 直接渲染时，正对鸭子右臂在**画面左侧** ✓。
  //      再叠一次 X 镜像 ⇒ 右臂跑到 +X ⇒ 正对时跑到**画面右侧** ✗（用户一眼看出「左右反了」）。
  //    模型自己的坐标系里上方就是 +Y、前方就是 +Z ⇒ 直接进来就是站立的 ✓ 不需要任何旋转 ✓
  //    资源是内嵌的（`assets/duck_player.js`）：CSP 是 `connect-src 'none'` ⇒ 运行时不能 fetch ✗
  //    ⭐ **常驻、不给开关** —— 它是背景参照物，不是要调的参数（面板里没有它）
  var REF_MAT = { metal: 0.0, rough: 0.7 };   // ⭐ 鸭子不是金属 ✗（用 MAT 的 0.3 会让白毛变灰 ✗）
  // ⭐ 游戏角色拿枪的 socket（`Hand.R/MeleeWeaponSocket`）在导出坐标下的位置 ——
  //    离线从游戏数据量得（玩家鸭的姿势变了就要重量 ✗）
  var REF_SOCKET = [-0.3972, 0.6139, 0.3622];
  var refDuck = null;

  function loadRefDuck() {
    if (!window.DUCK_REF_B64) return;
    var bin = base64ToBytes(window.DUCK_REF_B64);
    var buf = bin.buffer.slice(bin.byteOffset, bin.byteOffset + bin.byteLength);
    new GLTFLoader().parse(buf, "", function (gltf) {
      var g = new THREE.Group();
      g.name = "refDuck";
      // ⚠️ 这里**不加** X 镜像 ✗，理由见上面那段实测（前视图：右臂在画面左侧 = 对）
      var mir = new THREE.Group();
      mir.add(gltf.scene);
      g.add(mir);
      gltf.scene.traverse(function (o) {
        if (!o.isMesh) return;
        (Array.isArray(o.material) ? o.material : [o.material]).forEach(function (m) {
          if (!m) return;
          m.metalnessMap = null;
          m.roughnessMap = null;
          m.aoMap = null;
          m.metalness = REF_MAT.metal;
          m.roughness = REF_MAT.rough;
          m.side = THREE.DoubleSide;
          m.needsUpdate = true;
        });
      });
      refDuck = g;
      scene.add(g);
      placeRefDuck();
    }, function (e) { console.warn("参照鸭子加载失败", e); });
  }

  function placeRefDuck() {
    if (!refDuck) return;
    // ⭐ 鸭子**站在地面**（脚底贴网格、站在原点）—— 它是参照物，不是被调的东西
    //    要去对齐的是**武器**：模型会被移到 `REF_SOCKET`（见 build 里第 ⑤ 步）
    refDuck.position.set(0, 0, 0);
    refDuck.updateMatrixWorld(true);
    var b = new THREE.Box3().setFromObject(refDuck);
    refDuck.position.set(0, -b.min.y, 0);
    refDuck.updateMatrixWorld(true);
  }

  // ── 挂点（比例 0~1）─────────────────────────────────────────────────────
  // 和游戏同一套坐标：`slots` 的值是 [L,H,D] 比例 ⇒ 在**模型自身包围盒**里取点
  //    L 沿 Z（长轴 / 枪口 +Z）· H 沿 Y · D 沿 X        （同 `WeaponModel.cs` 的换算）
  // 面板里改数字、或在场景里**拖球**，两边同步。调好后把 JSON 抄进 `config.json`（另一个编辑器）
  var SLOT_PRESET = ["Muzzle", "Stock", "Grip", "Scope", "Tec", "pivot"];
  var HANDLE_MAT = { pivot: 0xff8a00, other: 0x00a6c8 };
  var HANDLE_R = 0.012;
  var slotHandles = [];
  var drag = null;
  var raycaster = new THREE.Raycaster();
  var pointer = new THREE.Vector2();
  var dragPlane = new THREE.Plane();

  var opts = {
    kind: "weapon",     // weapon（枪：握把归零） | melee（近战：长轴→+Y） | item（物品：不摆正）
    slots: {},          // { "Muzzle": [L,H,D], ..., "pivot": [L,H,D] }（比例 0~1）
    muzzle: "auto",     // ⭐ 固定"自动"（`MuzzleAtPositiveZ`：两端各看 6%/15%，细的那端是枪管）—— 不给选择
    grip: true,         // ⭐ 只有「枪」用得上（`GuessGrip` 的假设是枪）—— UI 里也只在选枪时露出这一行 ✗
    mirror: false,      // ⭐ 固定**关**：实测加镜像后预览与游戏左右相反 ✗（见 build 第 ③ 步）
    grid: true,         // ⭐ 固定开：网格（每格 10cm）
    size: 0            // 0 = 不改（用模型自己的尺寸）
  };

  function build() {
    if (!state.model) return;
    // ⚠️ 只清 stage ✗（root 里装的是**层级**：mirror/grip/flip/orient/stage —— 清它等于把树拆了 ✗）
    while (state.stage.children.length) state.stage.remove(state.stage.children[0]);

    // ① 原始模型（three 与 glTF 同规 ⇒ 原样加进来）
    state.stage.add(state.model);

    // ② 摆正：**不做** ✗ —— 三类都不摆正，一律**原点对原点**放（用户 2026-10-12 定调）
    //    曾经的「长轴 → +Z/+Y」与「枪口朝 +Z」已删（判据函数 longAxis / muzzleAtPositiveZ
    //    也从本文件移除；要恢复看 git 历史）。
    state.orient.rotation.set(0, 0, 0);
    state.flip.rotation.set(0, 0, 0);
    state.grip.position.set(0, 0, 0);
    state.mirror.scale.set(1, 1, 1);
    state.root.scale.setScalar(1);
    state.root.updateMatrixWorld(true);

    // ③ 镜像：**不做** ✗（用户 2026-10-12 定调，实测为准）
    //    之前这里按「游戏 GltfLoader 会 X 取反」也取反了一次 ✗ —— 那是把
    //    **坐标系转换**当成了**显示效果**。实测反了：
    //      · 加镜像：正对鸭子时右臂在**画面右侧** ✗
    //      · 不加：右臂在**画面左侧** ✓（= 游戏里的样子）
    //    ⚠️ 这个坑只在**不对称模型**上看得出来 —— 枪大多左右对称，所以一直没暴露 ✗
    state.mirror.scale.set(1, 1, 1);

    // ④ 握把归零（顶点级：把握把点移到原点）—— 只有「枪」用得上（`GuessGrip` 的假设是枪）
    if (opts.grip && opts.kind === "weapon") {
      state.mirror.updateMatrixWorld(true);
      var g = guessGrip(samplePoints(state.mirror));
      state.grip.position.set(-g[0], -g[1], -g[2]);
    }

    // ⑤ 移到拿枪的 socket（**枪去找手** ✗ 不是把手拉下来）—— 只有「枪」要这一步
    //    游戏里武器就挂在角色的 `MeleeWeaponSocket` 上 ⇒ 预览也放到那里，看到的才对得上
    state.root.position.set(0, 0, 0);
    if (opts.kind === "weapon") {
      state.root.position.set(REF_SOCKET[0], REF_SOCKET[1], REF_SOCKET[2]);
    }

    // ⑥ 尺寸：最长边 → opts.size（米）
    state.root.updateMatrixWorld(true);
    var box = new THREE.Box3().setFromObject(state.root);
    var size = box.getSize(new THREE.Vector3());
    var longest = Math.max(size.x, size.y, size.z);
    var s = (opts.size > 0 && longest > 1e-6) ? (opts.size / longest) : 1;
    state.root.scale.setScalar(s);

    state.root.updateMatrixWorld(true);
    box = new THREE.Box3().setFromObject(state.root);
    state.box = box;
    state.radius = Math.max(box.getSize(new THREE.Vector3()).length() / 2, 0.05);

    // ⭐ 取景范围 = 打开的模型 **∪ 参照鸭子**（参照物默认在原点站 1.12m ⇒ 不算进去就被裁掉 ✗）
    var viewBox = box.clone();
    if (refDuck && refDuck.visible) viewBox.expandByObject(refDuck);
    state.viewBox = viewBox;
    state.viewRadius = Math.max(viewBox.getSize(new THREE.Vector3()).length() / 2, 0.05);

    // 包围盒线框
    if (hull) { scene.remove(hull); hull.geometry.dispose(); }
    hull = new THREE.Box3Helper(box, 0x2b6cb0);
    scene.add(hull);

    // ⭐ 材质：**只用 baseColor + 法线**，金属/光泽贴图一律丢掉 —— 与游戏一致 ✓
    //   游戏那边只换 baseColor（`ApplyOurTexture`）+ 接法线（`ApplyNormalMap`，且只在 `SodaCraft/` shader 上 ✓），
    //   金属/光滑**没有槽位** ✗ ⇒ glTF 自带的 `metallicRoughnessTexture`（ORM）在游戏里根本不会被用到 ✓
    //   ⇒ 预览要是接上它 ⇒ 会比游戏**亮/金属得多** ✗（看着像"模型很糟"的假象 ✓）
    //   顺带：镜像后绕序会反 ⇒ 双面渲染 ✓（只影响观感，不改颜色 ✓）
    state.root.traverse(function (o) {
      if (!o.isMesh) return;
      var mats = Array.isArray(o.material) ? o.material : [o.material];
      mats.forEach(function (m) {
        if (!m) return;
        m.metalnessMap = null;
        m.roughnessMap = null;
        m.aoMap = null;
        m.metalness = MAT.metal;
        m.roughness = MAT.rough;
        m.side = THREE.DoubleSide;
        m.needsUpdate = true;
      });
    });

    grid.visible = opts.grid;

    // ⭐ 「握把归零」只在选「枪」时露出（物品/近战/装备不适用 —— 那是枪的猜测规则）
    var gripRow = document.getElementById("gripRow");
    if (gripRow) gripRow.hidden = opts.kind !== "weapon";

    rebuildSlots();

    // 面板读数
    var cur = box.getSize(new THREE.Vector3());
    document.getElementById("curSize").textContent =
      cur.x.toFixed(3) + " × " + cur.y.toFixed(3) + " × " + cur.z.toFixed(3) + " m";
    var verts = 0, tris = 0;
    state.model.traverse(function (o) {
      if (!o.isMesh) return;
      var g = o.geometry;
      verts += g.attributes.position ? g.attributes.position.count : 0;
      tris += g.index ? g.index.count / 3 : (g.attributes.position ? g.attributes.position.count / 3 : 0);
    });
    // 右下角最下面只留顶点/三角面（其它信息在「尺寸」那一段已经有了 ✓ 不再重复 ✓）
    document.getElementById("diag").textContent = "顶点 " + verts + " · 三角面 " + Math.round(tris);
  }

  /** 取模型世界坐标下的顶点样本（用于朝向/握把判定） */
  function samplePoints(obj) {
    var out = [];
    obj.updateMatrixWorld(true);
    var v = new THREE.Vector3();
    obj.traverse(function (o) {
      if (!o.isMesh || !o.geometry || !o.geometry.attributes.position) return;
      var pos = o.geometry.attributes.position;
      var step = Math.max(1, Math.floor(pos.count / 4000));   // 上限 4000 个采样点
      for (var i = 0; i < pos.count; i += step) {
        v.fromBufferAttribute(pos, i).applyMatrix4(o.matrixWorld);
        out.push([v.x, v.y, v.z]);
      }
    });
    return out;
  }

  // ── 挂点：模型自身坐标 → 世界 ───────────────────────────────────────────────
  // “模型自身包围盒” = 游戏那边的 `mesh.bounds` ⇒ glb 里各几何体的包围盒并集（不带节点变换）
  function firstMesh() {
    var found = null;
    state.model.traverse(function (o) { if (!found && o.isMesh && o.geometry) found = o; });
    return found;
  }

  function assetBox(mesh) {
    var b = new THREE.Box3();
    var any = false;
    state.model.traverse(function (o) {
      if (!o.isMesh || !o.geometry) return;
      if (!o.geometry.boundingBox) o.geometry.computeBoundingBox();
      var gb = o.geometry.boundingBox.clone();
      gb.applyMatrix4(o.matrix);          // 到 `state.model` 的局部空间
      b.union(gb);
      any = true;
    });
    return any ? b : new THREE.Box3(new THREE.Vector3(-0.5, -0.5, -0.5), new THREE.Vector3(0.5, 0.5, 0.5));
  }

  /** 比例 [L,H,D] → `state.model` 局部坐标 */
  function ratioToLocal(box, lhd) {
    return new THREE.Vector3(
      box.min.x + Math.max(0, Math.min(1, lhd[2])) * (box.max.x - box.min.x),
      box.min.y + Math.max(0, Math.min(1, lhd[1])) * (box.max.y - box.min.y),
      box.min.z + Math.max(0, Math.min(1, lhd[0])) * (box.max.z - box.min.z));
  }

  /** `state.model` 局部坐标 → 比例 [L,H,D] */
  function localToRatio(box, v) {
    var sx = box.max.x - box.min.x, sy = box.max.y - box.min.y, sz = box.max.z - box.min.z;
    var c = function (a, lo, s) { return s < 1e-6 ? 0.5 : Math.max(0, Math.min(1, (a - lo) / s)); };
    return [c(v.z, box.min.z, sz), c(v.y, box.min.y, sy), c(v.x, box.min.x, sx)];
  }

  function rebuildSlots() {
    for (var i = 0; i < slotHandles.length; i++) {
      if (slotHandles[i].parent) slotHandles[i].parent.remove(slotHandles[i]);
      slotHandles[i].geometry.dispose();
      slotHandles[i].material.dispose();
    }
    slotHandles = [];
    if (!state.model) return;
    var box = assetBox();
    Object.keys(opts.slots).forEach(function (key) {
      var mat = new THREE.MeshBasicMaterial({
        color: key === "pivot" ? HANDLE_MAT.pivot : HANDLE_MAT.other,
        depthTest: false, transparent: true, opacity: 0.95,
      });
      var h = new THREE.Mesh(new THREE.SphereGeometry(HANDLE_R, 16, 12), mat);
      h.name = "slot:" + key;
      h.renderOrder = 999;
      h.userData.key = key;
      h.position.copy(ratioToLocal(box, opts.slots[key]));
      state.model.add(h);            // 挂在模型下 ⇒ 跟着镜像/缩放一起走
      slotHandles.push(h);
    });
  }

  function renderSlotRows() {
    var tb = document.getElementById("slots");
    if (!tb) return;
    tb.textContent = "";
    var fmt = function (v) { return (Math.round(v * 1000) / 1000).toString(); };
    Object.keys(opts.slots).forEach(function (key) {
      var lhd = opts.slots[key];
      var tr = document.createElement("tr");
      tr.innerHTML = '<td><input class="k" type="text" list="slotkeys"></td>' +
        '<td><input class="l" type="number" step="0.01" min="0" max="1"></td>' +
        '<td><input class="h" type="number" step="0.01" min="0" max="1"></td>' +
        '<td><input class="d" type="number" step="0.01" min="0" max="1"></td>' +
        '<td class="btn"><a title="删除">×</a></td>';
      var ins = tr.querySelectorAll("input");
      ins[0].value = key;
      ins[1].value = fmt(lhd[0]); ins[2].value = fmt(lhd[1]); ins[3].value = fmt(lhd[2]);
      ins[0].addEventListener("change", function () {
        var want = ins[0].value.trim();
        if (!want || want === key) { ins[0].value = key; return; }
        opts.slots[want] = opts.slots[key]; delete opts.slots[key];
        renderSlotRows(); rebuildSlots();
      });
      [1, 2, 3].forEach(function (idx) {
        ins[idx].addEventListener("input", function () {
          opts.slots[key][idx - 1] = Math.max(0, Math.min(1, Number(ins[idx].value) || 0));
          rebuildSlots();
        });
      });
      tr.querySelector("td.btn a").addEventListener("click", function () {
        delete opts.slots[key]; renderSlotRows(); rebuildSlots();
      });
      tb.appendChild(tr);
    });
  }

  function pointerNdc(ev) {
    var r = renderer.domElement.getBoundingClientRect();
    pointer.set(((ev.clientX - r.left) / r.width) * 2 - 1, -((ev.clientY - r.top) / r.height) * 2 + 1);
  }

  function attachSlotDrag() {
    var el = renderer.domElement;
    el.addEventListener("pointerdown", function (ev) {
      if (!slotHandles.length) return;
      pointerNdc(ev);
      raycaster.setFromCamera(pointer, camera);
      var hits = raycaster.intersectObjects(slotHandles, false);
      if (!hits.length) return;
      drag = hits[0].object;
      controls.enabled = false;                       // 抓球时别转镜头
      var nrm = camera.getWorldDirection(new THREE.Vector3());
      dragPlane.setFromNormalAndCoplanarPoint(nrm, drag.getWorldPosition(new THREE.Vector3()));
      if (el.setPointerCapture) { try { el.setPointerCapture(ev.pointerId); } catch (e) {} }
    });
    el.addEventListener("pointermove", function (ev) {
      if (!drag) return;
      pointerNdc(ev);
      raycaster.setFromCamera(pointer, camera);
      var hit = new THREE.Vector3();
      if (!raycaster.ray.intersectPlane(dragPlane, hit)) return;
      var local = drag.parent.worldToLocal(hit);      // → `state.model` 局部
      var box = assetBox();
      opts.slots[drag.userData.key] = localToRatio(box, local);
      drag.position.copy(ratioToLocal(box, opts.slots[drag.userData.key]));
      renderSlotRows();
    });
    var end = function () { if (drag) { drag = null; controls.enabled = true; } };
    el.addEventListener("pointerup", end);
    el.addEventListener("pointercancel", end);
  }

  // ── 启动 ────────────────────────────────────────────────────────────────────
  function start() {
    initScene();
    // ⚠️ **必须在任何 glTF 解析之前**删掉 `createImageBitmap` ✗
    //    它走 fetch(blob:) ⇒ 被 CSP `connect-src 'none'` 挡掉 ⇒ **贴图全丢** ⇒ 看着像「模型没皮」✗
    //    删掉 ⇒ three 退回 `TextureLoader`（`<img src=blob:>` ✓ 走 `img-src` ✓ 允许 ✓）
    //    参照鸭子是**先加载**的 ⇒ 晚删就轮到它中招（曾经就是：鸭子白身橙嘴全丢）
    try { delete window.createImageBitmap; } catch (e) {}
    document.getElementById("views").addEventListener("click", function (ev) {
      var a = ev.target.closest("a[data-dir]");
      if (!a) return;
      setView(a.dataset.dir.split(",").map(Number));
    });
    // 挂点：加/复制 + 拖球
    renderSlotRows();
    attachSlotDrag();
    document.getElementById("addslot").addEventListener("click", function () {
      var k = "slot";
      for (var i = 1; opts.slots[k] !== undefined; i++) k = "slot" + i;
      opts.slots[k] = [0.5, 0.5, 0.5];
      renderSlotRows(); rebuildSlots();
    });
    document.getElementById("copyslots").addEventListener("click", function () {
      var out = {};
      Object.keys(opts.slots).forEach(function (k) {
        out[k] = opts.slots[k].map(function (v) { return Math.round(v * 1000) / 1000; });
      });
      var text = JSON.stringify(out);
      var hint = document.getElementById("copyHint");
      var done = function (ok) { hint.textContent = ok ? "已复制 ✓ 贴进 config.json" : "复制失败，请手动选"; setTimeout(function () { hint.textContent = ""; }, 2500); };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(function () { done(true); }, function () { done(false); });
      } else {
        try {
          var ta = document.createElement("textarea");
          ta.value = text; ta.style.position = "fixed"; ta.style.opacity = "0";
          document.body.appendChild(ta); ta.select();
          var ok = document.execCommand("copy");
          document.body.removeChild(ta);
          done(ok);
        } catch (e) { done(false); }
      }
    });

    document.getElementById("kind").addEventListener("change", function (e) { opts.kind = e.target.value; build(); });
    document.getElementById("grip").addEventListener("change", function (e) { opts.grip = e.target.checked; build(); });
    loadRefDuck();
    document.getElementById("size").addEventListener("change", function (e) {
      opts.size = parseFloat(e.target.value) || 0; build();
    });

    readFile().then(function (res) {
      if (!res.data) throw new Error("文件为空");
      // 宿主必须把二进制按 base64 发（`editor:fileContent` 的 `encoding: "base64"`）。
      // 万一发的是 utf8（把 .glb 当文本读了 ⇒ 乱码 + 256KB 截断），给一句人话 ✗ 别把 atob 的异常抛给玩家。
      if (res.encoding && res.encoding !== "base64") {
        throw new Error(
          "宿主把这个模型当文本发过来了（encoding=" + res.encoding +
          "）—— 需要产品把二进制文件按 base64 交给编辑器（见 docs/mod-repo-guide.md §10）"
        );
      }
      var bin = base64ToBytes(res.data);
      var buf = bin.buffer.slice(bin.byteOffset, bin.byteOffset + bin.byteLength);
      // ⚠️ `createImageBitmap` 已在 `start()` 开头删掉（那时才轮到参照鸭子之后的所有解析 ✓）
      return new Promise(function (resolve, reject) {
        new GLTFLoader().parse(buf, "", resolve, reject);
      });
    }).then(function (gltf) {
      state.model = gltf.scene;

      // 层级：root(缩放) → mirror(X 取反) → grip(归零) → flip(枪口 180°) → orient(front 旋转) → stage(原模型)
      state.root = new THREE.Group();  state.root.name = "scale";   // ← 缩放层就是它（build 里用 state.root）
      state.mirror = new THREE.Group(); state.mirror.name = "mirror";
      state.grip = new THREE.Group();  state.grip.name = "grip";
      state.flip = new THREE.Group();  state.flip.name = "flip";
      state.orient = new THREE.Group(); state.orient.name = "orient";
      state.stage = new THREE.Group(); state.stage.name = "stage";
      state.root.add(state.mirror); state.mirror.add(state.grip); state.grip.add(state.flip);
      state.flip.add(state.orient); state.orient.add(state.stage);
      scene.add(state.root);

      build();
      var cur = state.box.getSize(new THREE.Vector3());
      document.getElementById("size").value = Math.max(cur.x, Math.max(cur.y, cur.z)).toFixed(3);
      setView([-1.2, 0.6, 1.3]);
      window.__ready = true;
    }).catch(function (e) {
      showError("模型加载失败：" + (e && e.message ? e.message : e));
    });
  }

  function base64ToBytes(b64) {
    var s = String(b64).replace(/^data:[^;]+;base64,/, "");
    var raw = atob(s);
    var out = new Uint8Array(raw.length);
    for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
    return out;
  }

  post({ type: "editor:ready" });
})();
