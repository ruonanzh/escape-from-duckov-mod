/**
 * Duckov 配置编辑器 —— mod 的 `config.json`（表单，Ctrl+S 保存）。
 *
 * 只用契约里的 postMessage 桥（设计仓 desktop-gamer-agent-pi `docs/mod-repo-guide.md` §10）：
 * `editor:readFile` / `editor:save` 都**只对打开的那个文件**生效 —— 本编辑器读写的正好是它 ✓
 * 沙箱里没有网络（CSP `connect-src 'none'`），所以不 fetch 任何东西 ✓
 *
 * 字段与 `libs/mod-kit` 对齐（改字段前先看那边的 README）：
 *   item_model   targets / typeIDs / model / world / handheld / size / front / debug
 *   weapon_model 另加 icon / slots（含可选的 pivot）
 */

const SIMPLE = ["typeIDs", "targets", "model", "front", "size", "icon", "world", "handheld", "debug"];
const BOOL = new Set(["world", "handheld", "debug"]);
const SLOT_KEYS = ["muzzle", "stock", "grip", "scope", "tec", "pivot"];

let writable = false;
let revision = null;
let model = {};          // 解析后的原始对象（未知键原样带回）
let dirty = false;
let seq = 0;
const pending = new Map();

const $ = (id) => document.getElementById(id);
const post = (m) => window.parent.postMessage(m, "*");

function request(type, payload) {
  const requestId = `r${++seq}`;
  return new Promise((resolve) => {
    pending.set(requestId, resolve);
    post({ type, requestId, ...payload });
  });
}

function settle(requestId, value) {
  const resolve = pending.get(requestId);
  if (resolve) {
    pending.delete(requestId);
    resolve(value);
  }
}

window.addEventListener("message", (ev) => {
  const d = ev.data || {};
  if (d.type === "editor:init") {
    writable = d.mode === "edit";
    $("title").textContent = "Duckov 配置编辑器 · " + ((d.file && d.file.name) || "config.json");
    void load();
  } else if (d.type === "editor:fileContent" || d.type === "editor:saved" || d.type === "editor:error") {
    settle(d.requestId, d);
  }
});

// ── 列表字段 <-> 文本 ───────────────────────────────────────────────────────
function listToText(value) {
  if (!Array.isArray(value)) return value == null ? "" : String(value);
  return value.join(", ");
}
function textToList(text, numeric) {
  const parts = String(text)
    .split(/[,\n]/)
    .map((s) => s.trim())
    .filter((s) => s !== "");
  if (!parts.length) return undefined;
  return numeric ? parts.map((s) => Number(s)) : parts;
}

// ── 槽位表 ─────────────────────────────────────────────────────────────────
function addSlotRow(key, value) {
  const tr = document.createElement("tr");
  tr.innerHTML =
    '<td><input type="text" class="k"></td>' +
    '<td><input type="number" step="0.01" class="l"></td>' +
    '<td><input type="number" step="0.01" class="h"></td>' +
    '<td><input type="number" step="0.01" class="d"></td>' +
    '<td class="del"><button type="button" title="删除">×</button></td>';
  const inputs = tr.querySelectorAll("input");
  inputs[0].value = key || "";
  const v = Array.isArray(value) ? value : [0, 0, 0];
  inputs[1].value = v[0] ?? 0;   // x（沿 X）
  inputs[2].value = v[1] ?? 0;   // y（沿 Y）
  inputs[3].value = v[2] ?? 0;   // z（沿 Z，枪口 +Z）
  tr.querySelector(".del button").addEventListener("click", () => {
    tr.remove();
    setDirty(true);
  });
  inputs.forEach((el) => el.addEventListener("input", () => setDirty(true)));
  $("slots").appendChild(tr);
}

function readSlots() {
  const out = {};
  for (const tr of $("slots").querySelectorAll("tr")) {
    const inputs = tr.querySelectorAll("input");
    const key = inputs[0].value.trim();
    if (!key) continue;
    out[key] = [Number(inputs[1].value) || 0, Number(inputs[2].value) || 0, Number(inputs[3].value) || 0];
  }
  return out;
}

// ── 表单 <-> 对象 ──────────────────────────────────────────────────────────
function readForm() {
  const next = { ...model };
  for (const f of SIMPLE) {
    const el = $(`f-${f}`);
    if (BOOL.has(f)) {
      const want = f === "world" ? true : false;   // world 默认开，其余默认关
      if (el.checked === want) delete next[f];
      else next[f] = el.checked;
      continue;
    }
    if (f === "size") {
      const raw = el.value.trim();
      if (raw === "") delete next.size;
      else next.size = Number(raw);
      continue;
    }
    if (f === "front") {
      if (el.value === "auto") delete next.front;
      else next.front = el.value;
      continue;
    }
    if (f === "typeIDs" || f === "targets") {
      const list = textToList(el.value, f === "typeIDs");
      if (list === undefined) delete next[f];
      else next[f] = list;
      continue;
    }
    const raw = el.value.trim();
    if (raw === "") delete next[f];
    else next[f] = raw;
  }
  const slots = readSlots();
  if (Object.keys(slots).length) next.slots = slots;
  else delete next.slots;
  return next;
}

function validate(value) {
  const errors = [];
  if (value.typeIDs && value.typeIDs.some((n) => !Number.isFinite(n))) errors.push("typeIDs 必须是数字");
  if (!value.typeIDs && !value.targets) errors.push("至少要给 typeIDs 或 targets 之一");
  if (!String(value.model || "").trim()) errors.push("model 不能为空（要指向一个 glb）");
  if (value.size != null && !(Number(value.size) > 0)) errors.push("size 要么留空，要么大于 0");
  for (const [k, v] of Object.entries(value.slots || {})) {
    if (!Array.isArray(v) || v.length < 3) errors.push(`slots.${k} 必须是三个数`);
    else if (v.some((n) => !Number.isFinite(n))) errors.push(`slots.${k} 的三个数必须是数字（单位：米，可负）`);
  }
  return errors;
}

function paint() {
  const errors = validate(readForm());
  const st = $("status");
  st.textContent = errors.length ? `有错误：${errors[0]}` : dirty ? "未保存（Ctrl+S）" : "已保存";
  st.dataset.state = errors.length ? "error" : dirty ? "dirty" : "clean";
  $("save").disabled = !writable || errors.length > 0;
}

function setDirty(next) {
  dirty = next;
  paint();
}

function fill(value) {
  model = value && typeof value === "object" ? { ...value } : {};
  for (const f of SIMPLE) {
    const el = $(`f-${f}`);
    const cur = model[f];
    if (BOOL.has(f)) {
      const want = f === "world" ? true : false;
      el.checked = cur == null ? want : !!cur;
    } else if (f === "front") {
      el.value = cur == null ? "auto" : String(cur);
    } else if (f === "typeIDs" || f === "targets") {
      el.value = listToText(cur);
    } else {
      el.value = cur == null ? "" : String(cur);
    }
  }
  $("slots").textContent = "";
  const slots = model.slots && typeof model.slots === "object" ? model.slots : {};
  for (const [k, v] of Object.entries(slots)) addSlotRow(k, v);
  const rest = { ...model };
  for (const f of SIMPLE) delete rest[f];
  delete rest.slots;
  $("rest").textContent = Object.keys(rest).length ? JSON.stringify(rest, null, 2) : "（没有其他字段）";
  dirty = false;
  showError("");
  paint();
}

function showError(message) {
  const box = $("error");
  box.style.display = message ? "block" : "none";
  box.textContent = message || "";
}

async function load() {
  const res = await request("editor:readFile", {});
  if (res.code) {
    showError(res.message || "读取失败");
    return;
  }
  revision = res.revision ?? null;
  if (res.encoding && res.encoding !== "utf8") {
    showError(`宿主发来的不是 utf8（encoding=${res.encoding}）`);
    return;
  }
  try {
    fill(JSON.parse(res.data));
  } catch (e) {
    showError(`config.json 不是合法 JSON：${e.message}`);
  }
}

async function save() {
  if (!writable) return;
  const errors = validate(readForm());
  if (errors.length) {
    paint();
    return;
  }
  const next = readForm();
  const res = await request("editor:save", {
    data: `${JSON.stringify(next, null, 2)}\n`,
    baseRevision: revision ?? undefined,
  });
  if (res.code) {
    showError(
      res.code === "CONFLICT"
        ? "文件已被别处改动 —— 先「重新加载」，否则会覆盖别人的修改。"
        : res.message || "保存失败",
    );
    return;
  }
  revision = res.revision ?? revision;
  model = next;
  setDirty(false);
  showError("");
}

function start() {
  for (const f of SIMPLE) {
    const el = $(`f-${f}`);
    if (!el) continue;
    el.addEventListener(el.tagName === "SELECT" || el.type === "checkbox" ? "change" : "input", () => setDirty(true));
  }
  $("addslot").addEventListener("click", () => {
    addSlotRow($("slotkey").value, [0.5, 0.5, 0.5]);
    setDirty(true);
  });
  $("reload").addEventListener("click", () => void load());
  $("save").addEventListener("click", () => void save());
  window.addEventListener("keydown", (ev) => {
    if ((ev.ctrlKey || ev.metaKey) && ev.key.toLowerCase() === "s") {
      ev.preventDefault();
      void save();
    }
  });
  post({ type: "editor:ready" });
}

start();
