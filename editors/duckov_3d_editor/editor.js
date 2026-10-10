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

  /** `GltfLoader.Thickness`：某一段（沿 z）里的最大横向厚度 */
  function thickness(points, lo, hi) {
    var x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity, any = false;
    for (var i = 0; i < points.length; i++) {
      var p = points[i];
      if (p[2] < lo || p[2] > hi) continue;
      any = true;
      if (p[0] < x0) x0 = p[0];
      if (p[0] > x1) x1 = p[0];
      if (p[1] < y0) y0 = p[1];
      if (p[1] > y1) y1 = p[1];
    }
    return any ? Math.max(x1 - x0, y1 - y0) : Infinity;
  }

  /** `GltfLoader.MuzzleAtPositiveZ`：两端各看 6% / 15% 两片，**细**的那端是枪管 */
  function muzzleAtPositiveZ(points) {
    var b = bounds(points);
    var sp = b.span[2];
    if (sp <= 1e-6) return true;
    var tMin = Math.max(thickness(points, b.mn[2], b.mn[2] + sp * 0.06), thickness(points, b.mn[2], b.mn[2] + sp * 0.15));
    var tMax = Math.max(thickness(points, b.mx[2] - sp * 0.06, b.mx[2]), thickness(points, b.mx[2] - sp * 0.15, b.mx[2]));
    return tMax < tMin;
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

  function initScene() {
    var host = document.getElementById("view");
    renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio || 1);
    renderer.setSize(window.innerWidth, window.innerHeight);
    renderer.setClearColor(0xeef0f2, 1);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    host.appendChild(renderer.domElement);

    scene = new THREE.Scene();
    // ⭐ 环境贴图必给 ✗ —— 否则 PBR 的金属部分是**全黑**（会造成"模型很糟"的假象）
    var pmrem = new THREE.PMREMGenerator(renderer);
    scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    scene.environmentIntensity = 1.0;
    scene.add(new THREE.AmbientLight(0xffffff, 0.75));
    var key = new THREE.DirectionalLight(0xffffff, 2.2); key.position.set(3, 5, 6); scene.add(key);
    var fill = new THREE.DirectionalLight(0xffffff, 0.8); fill.position.set(-4, 1, -3); scene.add(fill);

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

    // ⭐ 原点标记 = **游戏里模型原点的位置**（握把 / 手抓点 / 挂点基准）—— 白十字
    var o = new THREE.Vector3();
    var cs = [new THREE.Vector3(1, 0, 0), new THREE.Vector3(0, 1, 0), new THREE.Vector3(0, 0, 1)];
    var pts = [];
    cs.forEach(function (c) { pts.push(o.clone().addScaledVector(c, -0.02), o.clone().addScaledVector(c, 0.02)); });
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
    var center = state.box ? state.box.getCenter(new THREE.Vector3()) : new THREE.Vector3();
    var dist = (state.radius || 1) * 3.0;
    camera.position.copy(center).add(d.multiplyScalar(dist));
    controls.target.copy(center);
    controls.update();
  }

  // ── 组装（顺序与游戏一致） ─────────────────────────────────────────────────
  var opts = {
    kind: "gun",        // gun（长轴→+Z） | up（长轴→+Y） | raw
    muzzle: "auto",     // auto | +z | -z
    grip: true,         // 握把归零
    mirror: true,       // ⭐ 固定开：Unity 镜像（X 取反）= 游戏里看到的样子 —— 不做 ⇒ 预览与游戏左右相反 ✗
    grid: true,         // ⭐ 固定开：网格（每格 10cm）
    size: 0            // 0 = 不改（用模型自己的尺寸）
  };

  function build() {
    if (!state.model) return;
    // ⚠️ 只清 stage ✗（root 里装的是**层级**：mirror/grip/flip/orient/stage —— 清它等于把树拆了 ✗）
    while (state.stage.children.length) state.stage.remove(state.stage.children[0]);

    // ① 原始模型（three 与 glTF 同规 ⇒ 原样加进来）
    state.stage.add(state.model);

    // ② 长轴 → 目标轴（枪：+Z · 立起来：+Y）—— 与游戏 `OrientToUnity` / `ApplyFrontDeclaration` 等价：
    //    `OrientToUnity` ax==0 顶点映射 (z,y,−x) ⇒ 这里 = 绕 Y **+90°**；ax==1 映射 (x,z,−y) ⇒ 绕 X **−90°**
    //    ⚠️ 量长轴**必须先清零**所有变换 ✗ —— 否则读到的是"上一次摆好之后"的包围盒，
    //       长轴就会在每次重算时来回翻（实测：头盔 0.713×0.998×0.861 → 0.713×0.861×0.998 ✗）
    state.orient.rotation.set(0, 0, 0);
    state.flip.rotation.set(0, 0, 0);
    state.grip.position.set(0, 0, 0);
    state.mirror.scale.set(1, 1, 1);
    state.root.scale.setScalar(1);
    state.root.updateMatrixWorld(true);
    var b = bounds(samplePoints(state.model));
    var ax = longAxis(b.span);
    if (opts.kind === "gun") {
      if (ax === 0) state.orient.rotation.y = Math.PI / 2;
      else if (ax === 1) state.orient.rotation.x = -Math.PI / 2;
    } else if (opts.kind === "up") {
      if (ax === 0) state.orient.rotation.z = Math.PI / 2;
      else if (ax === 2) state.orient.rotation.x = -Math.PI / 2;
    }

    // ③ 枪口方向：判据（`MuzzleAtPositiveZ`：两端各看 6%/15%，**细**的那端是枪管）
    //    ⚠️ 必须对 **orient 之后**的坐标采样 ✗（模型自带的 matrixWorld 里没有这一步 ✗）
    state.flip.rotation.set(0, 0, 0);
    if (opts.kind === "gun") {
      state.orient.updateMatrixWorld(true);
      var want = opts.muzzle === "auto" ? (muzzleAtPositiveZ(samplePoints(state.orient)) ? "+z" : "-z") : opts.muzzle;
      if (want === "-z") state.flip.rotation.y = Math.PI;
    }

    // ④ 握把归零（顶点级：把握把点移到原点）
    state.grip.position.set(0, 0, 0);
    if (opts.grip && opts.kind === "gun") {
      state.flip.updateMatrixWorld(true);   // 枪托在 −Z ⇒ 必须在**朝向定好之后**采样 ✗
      var g = guessGrip(samplePoints(state.flip));
      state.grip.position.set(-g[0], -g[1], -g[2]);
    }

    // ⑤ Unity 镜像（= 游戏里 GltfLoader 的 X 取反）
    state.mirror.scale.set(opts.mirror ? -1 : 1, 1, 1);

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

    // 包围盒线框
    if (hull) { scene.remove(hull); hull.geometry.dispose(); }
    hull = new THREE.Box3Helper(box, 0x2b6cb0);
    scene.add(hull);

    // 材质：镜像后绕序会反 ⇒ 双面渲染（只影响观感，不改颜色）
    state.root.traverse(function (o) {
      if (!o.isMesh) return;
      var mats = Array.isArray(o.material) ? o.material : [o.material];
      mats.forEach(function (m) { if (m) { m.side = THREE.DoubleSide; } });
    });

    grid.visible = opts.grid;

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
    document.getElementById("diag").innerHTML =
      "顶点 " + verts + " · 三角面 " + Math.round(tris) + "<br>" +
      "最长边 " + Math.max(cur.x, Math.max(cur.y, cur.z)).toFixed(3) + " m" +
      (s !== 1 ? "（缩放 ×" + s.toFixed(4) + "）" : "") + "<br>" +
      "镜像 " + (opts.mirror ? "开（= 游戏里）" : "关（= glb 原始）") + "<br>" +
      "握把归零 " + (opts.grip && opts.kind === "gun" ? "已做" : "未做");
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

  // ── 启动 ────────────────────────────────────────────────────────────────────
  function start() {
    initScene();
    document.getElementById("views").addEventListener("click", function (ev) {
      var a = ev.target.closest("a[data-dir]");
      if (!a) return;
      setView(a.dataset.dir.split(",").map(Number));
    });
    document.getElementById("kind").addEventListener("change", function (e) { opts.kind = e.target.value; build(); });
    document.getElementById("muzzle").addEventListener("change", function (e) { opts.muzzle = e.target.value; build(); });
    document.getElementById("grip").addEventListener("change", function (e) { opts.grip = e.target.checked; build(); });
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
      // ⚠️ CSP 是 `connect-src 'none'`（编辑器不给外传通道）⇒ 内嵌纹理**不能**走
      //    `ImageBitmapLoader`（它用 fetch 读 blob: ✗ 会被挡 ⇒ 贴图全丢 ⇒ 看着像"模型没材质"）。
      //    删掉 createImageBitmap ⇒ three 退回 `TextureLoader`（`<img src=blob:>` ✓ 走 `img-src` ✓ 允许）。
      try { delete window.createImageBitmap; } catch (e) {}
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
