/**
 * mod-config-data.ts —— `config.json` 数据层的判据（新增武器 / 新增物品）。
 *
 * 为什么单独一份：扩展（`.pi/extensions/mod-lint.ts`）与自检脚本
 * （`scripts/check-mod-config-data.mjs`）导入**同一份**，于是判据不会漂。
 * 与 `libs/mod-kit/ItemFactory.cs` 和 `.pi/skills/add-weapon/SKILL.md` 对齐。
 *
 * 这里只放**离线**能判的。要读游戏数据的占号检查在扩展里（`data-probe`），
 * 它把 `gameIds` 作为参数传进来；为 null 时跳过那两项。
 */

// ── 数据层校验（新增武器 / 新增物品的 config）─────────────────────────────
// 判据与 `libs/mod-kit/ItemFactory.cs` 和 `.pi/skills/add-weapon/SKILL.md` 对齐。
// 离线能做的在这里做；「typeID 是否被占号」要读游戏数据（data-probe），拿不到就只给 WARN。

export const KNOWN_ACTIONS = new Set(["replace_weapon", "replace_item", "new_weapon", "new_item"]);
export const NEW_ACTIONS = new Set(["new_weapon", "new_item"]);
export const DISPLAY_QUALITIES = new Set(["None", "White", "Green", "Blue", "Purple", "Orange", "Red", "Q7", "Q8"]);
export const KNOWN_MERCHANTS = new Set(["Merchant_Normal", "Merchant_Ming", "Merchant_Weapon"]);

/** 去掉 `//` 行注释（字符串内的不碰）—— config 允许写注释，校验不得因此报错 */
export function stripJsonComments(text: string): string {
  let out = "";
  let inStr = false;
  let esc = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inStr) {
      out += c;
      if (esc) esc = false;
      else if (c === "\\") esc = true;
      else if (c === '"') inStr = false;
      continue;
    }
    if (c === '"') { inStr = true; out += c; continue; }
    if (c === "/" && text[i + 1] === "/") {
      while (i < text.length && text[i] !== "\n") i++;
      out += "\n";
      continue;
    }
    out += c;
  }
  return out;
}

const isPosInt = (v: unknown): v is number => typeof v === "number" && Number.isInteger(v) && v > 0;

/** 只查 config 的结构（纯函数，好测）。`gameIds` 为 null 时跳过“占号”那两项。 */
export function validateDataLayer(cfg: unknown, gameIds: Set<number> | null): { errors: string[]; warnings: string[] } {
  const errors: string[] = [];
  const warnings: string[] = [];
  const root = (cfg ?? {}) as Record<string, unknown>;
  const entries = root.entries;
  if (!Array.isArray(entries) || entries.length === 0) return { errors, warnings };

  const flat = ["typeIDs", "targets", "model"].filter((k) => k in root);
  if (flat.length) {
    warnings.push(
      `config.json has both "entries" and the flat fields (${flat.join(", ")}) - the template reads ONLY "entries", so the flat ones are ignored`,
    );
  }

  const usedNewIds = new Map<number, number>();
  entries.forEach((raw, i) => {
    const at = `config.json entries[${i}]`;
    if (!raw || typeof raw !== "object") { errors.push(`${at}: not an object`); return; }
    const e = raw as Record<string, unknown>;
    const action = typeof e.action === "string" ? e.action : "replace_weapon";
    if (!KNOWN_ACTIONS.has(action)) {
      errors.push(`${at}: unknown action "${action}" (use one of ${[...KNOWN_ACTIONS].join(", ")})`);
      return;
    }
    if (!NEW_ACTIONS.has(action)) return;

    const zh = `${at} (${action})`;
    if (!isPosInt(e.cloneFromItemId)) errors.push(`${zh}: cloneFromItemId is required and must be a positive typeID`);
    if (typeof e.displayName !== "string" || !e.displayName.trim()) errors.push(`${zh}: displayName is required`);
    if (e.newTypeId != null && !isPosInt(e.newTypeId)) errors.push(`${zh}: newTypeId must be a positive integer, or leave it out`);
    if (isPosInt(e.newTypeId)) {
      const first = usedNewIds.get(e.newTypeId);
      if (first != null) errors.push(`${zh}: newTypeId ${e.newTypeId} is already used by entries[${first}]`);
      else usedNewIds.set(e.newTypeId, i);
    }
    if (e.quality != null && !(typeof e.quality === "number" && Number.isInteger(e.quality) && e.quality >= 0))
      errors.push(`${zh}: quality must be an integer >= 0, or leave it out`);
    if (e.displayQuality != null && String(e.displayQuality) !== "" && !DISPLAY_QUALITIES.has(String(e.displayQuality)))
      errors.push(`${zh}: displayQuality "${String(e.displayQuality)}" is not one of ${[...DISPLAY_QUALITIES].join(", ")}`);
    if (e.size != null && !(typeof e.size === "number" && e.size > 0)) errors.push(`${zh}: size must be a number > 0, or leave it out`);
    if (e.merchantID != null && String(e.merchantID) !== "" && !KNOWN_MERCHANTS.has(String(e.merchantID)))
      errors.push(`${zh}: merchantID "${String(e.merchantID)}" is unknown (known: ${[...KNOWN_MERCHANTS].join(", ")})`);
    if (e.maxStock != null && !isPosInt(e.maxStock)) errors.push(`${zh}: maxStock must be a positive integer`);
    if (e.priceFactor != null && !(typeof e.priceFactor === "number" && e.priceFactor > 0)) errors.push(`${zh}: priceFactor must be a number > 0`);

    if (e.stats != null) {
      if (typeof e.stats !== "object" || Array.isArray(e.stats))
        errors.push(`${zh}: stats must be an object such as { "Damage": 13.0 }`);
      else
        for (const [k, v] of Object.entries(e.stats as Record<string, unknown>))
          if (typeof v !== "number" || !Number.isFinite(v))
            errors.push(`${zh}: stats.${k} must be a number (an absolute value, not a multiplier)`);
    }

    const craft = e.crafting;
    if (craft != null) {
      if (typeof craft !== "object" || Array.isArray(craft)) errors.push(`${zh}: crafting must be an object`);
      else {
        const c = craft as Record<string, unknown>;
        if (c.money != null && !(typeof c.money === "number" && c.money >= 0)) errors.push(`${zh}: crafting.money must be a number >= 0`);
        const mats = c.materials;
        if (mats != null) {
          if (typeof mats !== "object" || Array.isArray(mats))
            errors.push(`${zh}: crafting.materials must be an object such as { "662": 5 }`);
          else
            for (const [k, v] of Object.entries(mats as Record<string, unknown>)) {
              if (!/^\d+$/.test(k)) errors.push(`${zh}: crafting.materials key "${k}" must be an item typeID`);
              else if (!isPosInt(v)) errors.push(`${zh}: crafting.materials["${k}"] must be a positive amount`);
              else if (gameIds && !gameIds.has(Number(k)))
                errors.push(`${zh}: crafting material typeID ${k} does not exist in the game`);
            }
        }
      }
    }

    // 占号检查（要有游戏数据）
    if (gameIds) {
      if (isPosInt(e.cloneFromItemId) && !gameIds.has(e.cloneFromItemId))
        errors.push(`${zh}: cloneFromItemId ${e.cloneFromItemId} does not exist in the game - pick a real gun (inspect_game_data)`);
      if (isPosInt(e.newTypeId) && gameIds.has(e.newTypeId))
        errors.push(
          `${zh}: newTypeId ${e.newTypeId} is already taken by the game - pick a free id, or leave newTypeId out and let the game assign one`,
        );
    }
  });
  return { errors, warnings };
}
