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

// ── 条目模式（config 里有 entries 时）──────────────────────────────────────
// 一个 mod 可以**混装多种能力**，于是 entries 里每条自带一个 `action`，字段表按 action 换。
// 与 `libs/mod-kit` 与模板 `reference/weapon_model/ModBehaviour.cs` 对齐。
const ACTION_LABEL = {
  replace_weapon: "换武器外观（replace_weapon）",
  replace_item: "换物品外观（replace_item）",
  new_weapon: "新增武器（new_weapon）",
  new_item: "新增物品（new_item）",
};
const ACTION_FIELDS = {
  replace_weapon: ["typeIDs", "targets", "model", "front", "size", "icon", "world", "handheld", "debug"],
  replace_item: ["typeIDs", "targets", "model", "front", "size", "icon", "world", "handheld", "debug"],
  new_weapon: [
    "newTypeId", "cloneFromItemId", "displayName", "description", "model", "icon", "size",
    "merchantID", "maxStock", "priceFactor", "onSale", "quality", "displayQuality", "stats", "crafting",
  ],
  // 物品比武器多四样：tags（额外标记）、maxStackCount、value、weight。
  // 不列 slots：`slots` 是**武器模型挂点**用的，物品那条路不读它（列出来会让人以为有效）。
  new_item: [
    "newTypeId", "cloneFromItemId", "displayName", "description", "model", "icon", "size",
    "merchantID", "maxStock", "priceFactor", "onSale",
    "tags", "maxStackCount", "value", "weight",
    "quality", "displayQuality", "stats", "crafting",
  ],
};
const FIELD = {
  typeIDs: { kind: "nums", hint: "按 typeID（推荐）。逗号分隔。用 inspect_game_data 查。" },
  targets: { kind: "list", hint: "或按对象名**全等**（不区分大小写）。逗号分隔。" },
  model: { kind: "text", hint: "本 mod 目录下的 glb（相对名或绝对路径）。新增那条可以留空：沿用原型外形。" },
  front: { kind: "select", options: ["auto", "+z", "-z", "+x", "-x", "up"], def: "auto" },
  size: { kind: "number", hint: "目标**最长边**（米）。不填 = 不缩放 / 按枪种档位。" },
  icon: { kind: "text", def: "icon.png" },
  world: { kind: "bool", def: true },
  handheld: { kind: "bool", def: false },
  debug: { kind: "bool", def: false },
  newTypeId: { kind: "number", hint: "不填 = 游戏自己的下一个空闲号（ItemAssetsCollection.NextTypeID）。" },
  cloneFromItemId: { kind: "number", hint: "拿哪件现有物品/枪当**原型**（数值、插槽、品质、分类 Tag 全继承它）。" },
  displayName: { kind: "text" },
  description: { kind: "text" },
  merchantID: {
    kind: "text", def: "Merchant_Weapon",
    hint: "上架到哪个商店。**武器**不写 = Merchant_Weapon（老约定）；**物品必填**（游戏有 12 个商店档位："
      + "售货机 Merchant_Normal / 老吴 Merchant_Weapon / 橘子 Merchant_Equipment / 小明 Merchant_Ming …）。",
  },
  tags: {
    kind: "strings",
    hint: "**额外标记**，逗号分隔。追在原型分类之上，不是替换。"
      + "ShowCase = 可摆上展示台（展示台的 12 个槽都要它）；Collection / Luxury / Daily 只是归类标记。",
  },
  maxStackCount: { kind: "number", hint: "堆叠上限。不填 = 沿用原型。" },
  value: { kind: "number", hint: "价值（商人的价格基础）。不填 = 沿用原型。" },
  weight: { kind: "number", hint: "重量（千克）。不填 = 沿用原型；背包上限看它。" },
  maxStock: { kind: "number", def: "1" },
  priceFactor: { kind: "number", def: "1" },
  onSale: { kind: "bool", def: true },
  quality: { kind: "number", hint: "整数。不填 = 沿用原型。" },
  displayQuality: { kind: "select", options: ["", "None", "White", "Green", "Blue", "Purple", "Orange", "Red", "Q7", "Q8"] },
  stats: { kind: "lines", hint: "覆盖数值，**每行一个** `Damage=13.0`。键用游戏自己的名字；值用绝对值。" },
  crafting: { kind: "craft", hint: "制作台配方。材料每行一个 `662=5`。" },
};

let entriesMode = false;
let entries = [];          // 每条：{ action, … }（原对象）

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
  if (entriesMode) return { ...model, entries: readEntries() };
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
  if (entriesMode) return validateEntries(value.entries || []);
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
  // 有 entries 就走条目模式；否则沿用老表单（老 config 照原样渲染，行为不变）
  entriesMode = Array.isArray(model.entries) && model.entries.length > 0;
  if (entriesMode) {
    fillEntries(model.entries);
    applyMode();
    dirty = false;
    showError("");
    paint();
    return;
  }
  applyMode();
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

// ── 条目模式：渲染 / 读回 ──────────────────────────────────────────────

function numOrText(v) { return v == null ? "" : String(v); }

/** 把 `Key=Value` 多行文本读成对象；空行与 `#` 开头的行跳过 */
function readLines(text, numericKeys) {
  const out = {};
  for (const raw of String(text || "").split("\n")) {
    const line = raw.trim();
    if (!line || line.startsWith("#")) continue;
    const i = line.indexOf("=");
    if (i < 0) continue;
    const k = line.slice(0, i).trim();
    const v = line.slice(i + 1).trim();
    if (!k) continue;
    out[k] = numericKeys ? Number(v) : v;
  }
  return out;
}

function linesToText(obj) {
  if (!obj || typeof obj !== "object") return "";
  return Object.entries(obj).map(([k, v]) => `${k}=${v}`).join("\n");
}

/** 一个字段行。值从 `entry[key]` 取；写回由 `readEntryCard` 统一做 */
function fieldRow(key, spec, value) {
  const wide = spec.kind === "lines" || spec.kind === "craft";
  const row = document.createElement(wide ? "div" : "label");
  row.className = "row";
  row.dataset.field = key;
  const name = document.createElement("span");
  name.className = "k";
  name.textContent = key;
  row.appendChild(name);

  let el;
  if (spec.kind === "bool") {
    el = document.createElement("input");
    el.type = "checkbox";
    el.checked = value == null ? !!spec.def : !!value;
  } else if (spec.kind === "select") {
    el = document.createElement("select");
    for (const o of spec.options) {
      const op = document.createElement("option");
      op.value = o;
      op.textContent = o === "" ? "（不改）" : o;
      el.appendChild(op);
    }
    el.value = value == null ? spec.def ?? "" : String(value);
  } else if (spec.kind === "lines") {
    el = document.createElement("textarea");
    el.rows = 4;
    el.value = linesToText(value);
  } else if (spec.kind === "craft") {
    const money = document.createElement("input");
    money.type = "number";
    money.dataset.role = "money";
    money.value = numOrText((value && value.money) ?? 0);
    money.placeholder = "money";
    const tags = document.createElement("input");
    tags.type = "text";
    tags.dataset.role = "tags";
    tags.value = Array.isArray(value && value.tags) ? value.tags.join(", ") : "";
    tags.placeholder = "tags（逗号分隔）";
    row.appendChild(money);
    row.appendChild(tags);
    el = document.createElement("textarea");
    el.rows = 4;
    el.dataset.role = "materials";
    el.value = linesToText((value && value.materials) || {});
  } else if (spec.kind === "nums" || spec.kind === "strings" || spec.kind === "number") {
    el = document.createElement("input");
    el.type = spec.kind === "number" ? "number" : "text";
    el.step = "any";
    el.value = spec.kind === "number" ? numOrText(value) : listToText(value);
  } else {
    el = document.createElement("input");
    el.type = "text";
    el.value = numOrText(value);
  }
  el.classList.add("v");
  el.addEventListener("input", () => setDirty(true));
  el.addEventListener("change", () => setDirty(true));
  row.appendChild(el);
  if (spec.hint) {
    const hint = document.createElement("div");
    hint.className = "hint";
    hint.textContent = spec.hint;
    row.appendChild(hint);
  }
  return row;
}

/** 挂点表的一行（条目模式用；老表单那套 `addSlotRow` 写的是固定容器，不动它）*/
function slotRow(key, value) {
  const tr = document.createElement("tr");
  const arr = Array.isArray(value) ? value : [0, 0, 0];
  const idx = { x: 0, y: 1, z: 2 };
  for (const cls of ["k", "x", "y", "z"]) {
    const td = document.createElement("td");
    const inp = document.createElement("input");
    inp.className = cls;
    if (cls === "k") {
      inp.type = "text";
      inp.value = key || "";
    } else {
      inp.type = "number";
      inp.step = "0.01";
      inp.dataset.axis = cls;
      inp.value = arr[idx[cls]] ?? 0;
    }
    inp.addEventListener("input", () => setDirty(true));
    td.appendChild(inp);
    tr.appendChild(td);
  }
  const td = document.createElement("td");
  td.className = "del";
  const del = document.createElement("button");
  del.type = "button";
  del.textContent = "×";
  del.addEventListener("click", () => { tr.remove(); setDirty(true); });
  td.appendChild(del);
  tr.appendChild(td);
  return tr;
}

function slotTable(value) {
  const box = document.createElement("div");
  const table = document.createElement("table");
  table.className = "slots";
  table.innerHTML = '<thead><tr><th>键</th><th>x</th><th>y</th><th>z</th><th></th></tr></thead>';
  const body = document.createElement("tbody");
  table.appendChild(body);
  box.appendChild(table);
  const add = document.createElement("button");
  add.type = "button";
  add.textContent = "加一行挂点";
  add.addEventListener("click", () => { body.appendChild(slotRow("muzzle", [0, 0, 0])); setDirty(true); });
  box.appendChild(add);
  for (const [k, v] of Object.entries(value && typeof value === "object" ? value : {})) body.appendChild(slotRow(k, v));
  return box;
}

function addEntryCard(entry) {
  const src = entry && typeof entry === "object" ? entry : {};
  const raw = typeof src.action === "string" && src.action ? src.action : "replace_weapon";
  const known = !!ACTION_LABEL[raw];

  const card = document.createElement("section");
  card.className = "card";
  card.__src = src;
  // 不认识的 action：**不得**悄悄改成 replace_weapon（那会把玩家的配置改掉）。
  // 原值存在 `data-raw-action`，保存时原样写回，只给一条提示。
  card.dataset.unknown = known ? "" : "1";
  card.dataset.rawAction = raw;
  card.dataset.action = known ? raw : "";

  const head = document.createElement("div");
  head.className = "cardhead";
  const pick = document.createElement("select");
  pick.className = "action";
  for (const [k, label] of Object.entries(ACTION_LABEL)) {
    const op = document.createElement("option");
    op.value = k;
    op.textContent = label;
    pick.appendChild(op);
  }
  if (!known) {
    const op = document.createElement("option");
    op.value = "";
    op.textContent = `（不认识的 action：${raw}，原样保留）`;
    pick.appendChild(op);
  }
  pick.value = known ? raw : "";
  pick.addEventListener("change", () => {
    // 先把当前 DOM 按**旧** action 快照下来，再换字段表 —— 否则切走再切回会丢值（实测踩到）
    card.__src = readEntryCard(card);
    const next = pick.value || "replace_weapon";
    card.dataset.action = next;
    card.dataset.rawAction = next;
    card.dataset.unknown = "";
    renderCardFields(card);
    setDirty(true);
  });
  head.appendChild(pick);
  const del = document.createElement("button");
  del.type = "button";
  del.textContent = "删除这条";
  del.addEventListener("click", () => { card.remove(); setDirty(true); });
  head.appendChild(del);
  card.appendChild(head);

  const fields = document.createElement("div");
  fields.className = "fields";
  card.appendChild(fields);
  renderCardFields(card);
  $("entries").appendChild(card);
}

/** 按 `card.dataset.action` 重建这张卡的字段区。
 * 数据来源：DOM 的字段表与当前 action 对得上就读 DOM；对不上（首次渲染 / 刚切完 action）就拿 `card.__src`。*/
function renderCardFields(card) {
  const fields = card.querySelector(".fields");
  const action = card.dataset.action;
  const unknown = card.dataset.unknown === "1";
  const matches = fields.childElementCount > 0 && card.dataset.rendered === action;
  const cur = unknown || !matches ? { ...(card.__src || {}) } : readEntryCard(card);
  fields.textContent = "";

  if (unknown) {
    const note = document.createElement("div");
    note.className = "hint";
    note.style.marginLeft = "0";
    note.textContent = `这个 action（${card.dataset.rawAction}）编辑器还不认识。原样保留，不改动。要改就手改 JSON。`;
    fields.appendChild(note);
    const pre = document.createElement("pre");
    pre.textContent = JSON.stringify(cur, null, 2);
    fields.appendChild(pre);
    card.__src = cur;
    card.dataset.rendered = action;
    return;
  }

  for (const key of ACTION_FIELDS[action] || ACTION_FIELDS.replace_weapon) {
    fields.appendChild(fieldRow(key, FIELD[key] || { kind: "text" }, cur[key]));
  }
  const slotsTitle = document.createElement("div");
  slotsTitle.className = "hint";
  slotsTitle.style.marginLeft = "0";
  slotsTitle.textContent = "slots（米，我们模型的局部空间；键名大小写不敏感）";
  fields.appendChild(slotsTitle);
  fields.appendChild(slotTable(cur.slots));
  card.__src = cur;                 // 下一次重建的兵底值
  card.dataset.rendered = action;   // 记住这一屏是给哪个 action 渲染的
}

/** 读一张卡 → 一个 entry 对象（未知键从 `card.__src` 原样带回）*/
function readEntryCard(card) {
  // 不认识的 action：整条原样返回，不改成别的（改了就是悄悄换掉玩家的配置）
  if (card.dataset.unknown === "1") return { ...(card.__src || {}) };

  const action = card.dataset.action || "replace_weapon";
  const out = { action };
  const src = card.__src || {};
  const known = new Set([...(ACTION_FIELDS[action] || []), "action", "slots"]);
  for (const [k, v] of Object.entries(src)) if (!known.has(k)) out[k] = v;

  for (const row of card.querySelectorAll(".fields .row")) {
    const key = row.dataset.field;
    const spec = FIELD[key] || { kind: "text" };
    const el = row.querySelector(".v");
    if (!el) continue;
    if (spec.kind === "bool") {
      if (el.checked !== !!spec.def) out[key] = el.checked;
    } else if (spec.kind === "lines") {
      const o = readLines(el.value, true);      // 值必须是**数字**（mod 那边按 float 读）
      if (Object.keys(o).length) out[key] = o;
    } else if (spec.kind === "craft") {
      const craft = {};
      const money = Number((row.querySelector("[data-role=money]") || {}).value) || 0;
      const mats = readLines((row.querySelector("[data-role=materials]") || {}).value, true);
      const tags = textToList((row.querySelector("[data-role=tags]") || {}).value, false);
      const hasMats = Object.keys(mats).length > 0;
      const hasTags = !!(tags && tags.length);
      // **只有真写了内容才写这个键** —— 写一个空的 `crafting: {}` 会让 mod 以为要加配方
      if (hasMats || hasTags || money) {
        craft.money = money;
        if (hasMats) craft.materials = mats;
        if (hasTags) craft.tags = tags;
        out[key] = craft;
      }
    } else if (spec.kind === "select") {
      if (el.value !== "" && el.value !== (spec.def ?? "")) out[key] = el.value;
    } else if (spec.kind === "nums" || spec.kind === "strings") {
      const list = textToList(el.value, spec.kind === "nums");
      if (list && list.length) out[key] = list;
    } else if (spec.kind === "number") {
      const raw = el.value.trim();
      if (raw !== "") out[key] = Number(raw);
    } else {
      // 文本字段：**只看是否为空**，不得因为等于 `def` 就丢掉
      // （否则玩家显式写的 merchantID 会被默默删掉）
      const raw = el.value.trim();
      if (raw !== "") out[key] = raw;
    }
  }

  const slots = {};
  for (const tr of card.querySelectorAll(".fields table.slots tr")) {
    const kEl = tr.querySelector("input.k");
    if (!kEl) continue;
    const k = kEl.value.trim();
    if (!k) continue;
    const get = (a) => Number((tr.querySelector(`input[data-axis=${a}]`) || {}).value) || 0;
    slots[k] = [get("x"), get("y"), get("z")];
  }
  if (Object.keys(slots).length) out.slots = slots;
  return out;
}

function readEntries() {
  const out = [];
  for (const card of $("entries").querySelectorAll(".card")) out.push(readEntryCard(card));
  return out;
}

function validateEntries(list) {
  const errors = [];
  list.forEach((e, i) => {
    const at = `entries[${i}]（${e.action}）`;
    // 不认识的 action 不在这里报 —— 编辑器不得因为自己看不懂就拦住保存
    // （原样保留，交给 validate_mod 报）。
    if (!ACTION_LABEL[e.action]) return;
    if (e.action === "replace_weapon" || e.action === "replace_item") {
      if (e.typeIDs && e.typeIDs.some((n) => !Number.isFinite(n))) errors.push(`${at}：typeIDs 必须是数字`);
      if (!e.typeIDs && !e.targets) errors.push(`${at}：至少要给 typeIDs 或 targets 之一`);
      if (!String(e.model || "").trim()) errors.push(`${at}：model 不能为空（要指向一个 glb）`);
    } else {
      if (!(Number(e.cloneFromItemId) > 0)) errors.push(`${at}：cloneFromItemId 必填（原型枪的 typeID）`);
      if (e.newTypeId != null && !(Number(e.newTypeId) > 0)) errors.push(`${at}：newTypeId 要么不填，要么大于 0`);
      if (e.stats && Object.values(e.stats).some((v) => !Number.isFinite(Number(v)))) errors.push(`${at}：stats 的值必须是数字`);
    }
    if (e.size != null && !(Number(e.size) > 0)) errors.push(`${at}：size 要么留空，要么大于 0`);
    for (const [k, v] of Object.entries(e.slots || {})) {
      if (!Array.isArray(v) || v.length < 3) errors.push(`${at}：slots.${k} 必须是三个数`);
      else if (v.some((n) => !Number.isFinite(n))) errors.push(`${at}：slots.${k} 的三个数必须是数字`);
    }
  });
  return errors;
}

function fillEntries(list) {
  entries = Array.isArray(list) ? list : [];
  $("entries").textContent = "";
  for (const e of entries) addEntryCard(e);
}

/** 切换两种模式：条目模式只看 `#entriesSection`，扁平模式只看老那几节 */
function applyMode() {
  $("entriesSection").style.display = entriesMode ? "" : "none";
  for (const id of ["secPick", "secModel", "secLayer", "secSlots"]) $(id).style.display = entriesMode ? "none" : "";
  $("mkentries").style.display = entriesMode ? "none" : "";
}

function start() {
  $("addentry").addEventListener("click", () => { addEntryCard({ action: "replace_weapon" }); setDirty(true); });
  $("mkentries").addEventListener("click", () => {
    // 把当前扁平表单包成一条 entries（于是 一份 config 可以慢慢长出多条）
    const one = readForm();
    entriesMode = true;
    model = { ...one };
    for (const f of SIMPLE) delete model[f];
    delete model.slots;
    fillEntries([{ action: "replace_weapon", ...one }]);
    applyMode();
    setDirty(true);
  });
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
