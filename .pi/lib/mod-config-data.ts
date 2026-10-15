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

export const KNOWN_ACTIONS = new Set([
  "replace_weapon",
  "replace_item",
  "new_weapon",
  "new_item",
  "replace_character",
]);
/** 换**角色**的规则。角色不是物品，没有 `targets` 也没有 `typeIDs`，
 * 所以**不得**走下面那条「没有 targets 就静默无效果」的检查。 */
export const CHARACTER_ACTIONS = new Set(["replace_character"]);
/** **要做新增类字段校验**的 action。
 * 只有这两个用途：① 后面那些 cloneFromItemId / displayName / stats 的检查；
 * ② `mod-lint.ts` 据此决定要不要读游戏 typeID 清单。
 * **不得**拿它当「未实现的 action」判据 —— `replace_*` 不在里面，但它好好的。 */
export const NEW_ACTIONS = new Set(["new_weapon", "new_item"]);
/** 还没实现的 action。**现在是空的** —— `new_item` 已在 2026-10-14 接上（物品模板开始认 action）。
 * 保留这个集合，是因为它回答一个**独立的问题**：名字存在（`KNOWN_ACTIONS`）不等于已经能用。
 * 上次把两者合并，收紧时把 `replace_weapon` / `replace_item` 误报成「未实现」（回归 R1）。 */
export const NOT_IMPLEMENTED_ACTIONS = new Set<string>([]);
export const DISPLAY_QUALITIES = new Set(["None", "White", "Green", "Blue", "Purple", "Orange", "Red", "Q7", "Q8"]);

/** 游戏里现有的名字（tag / 商店档位）。
 *
 * **不得写死在代码里**（旧的 `KNOWN_MERCHANTS` 只有 3 个，而实际有 12 个商店档位加一套黑市，
 * 而且改一次版本就可能多一个）。与 `gameIds` 一样从游戏数据传入；为 null 时跳过那两项。 */
export type GameNames = {
  /** 游戏里现有的 tag 名。**null = 没拿到**，那几项检查就跳过（不要拿空集合当「游戏里没有」）。 */
  tags: Set<string> | null;
  /** 游戏里现有的商店档位。**null = 没拿到**，同理。 */
  merchants: Set<string> | null;
};

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

/** 只查 config 的结构（纯函数，好测）。`gameIds` / `gameNames` 为 null 时跳过“占号”与“名字存在”那几项。 */
export function validateDataLayer(
  cfg: unknown,
  gameIds: Set<number> | null,
  gameNames: GameNames | null = null,
): { errors: string[]; warnings: string[] } {
  const errors: string[] = [];
  const warnings: string[] = [];
  const root = (cfg ?? {}) as Record<string, unknown>;
  const entries = root.entries;
  if (!Array.isArray(entries) || entries.length === 0) {
    // 扁平写法：模板只在 targets / typeIDs / model 三者之一存在时才用它。
    // 既没有 targets 也没有 typeIDs 的扁平规则永远匹配不到，于是静默无效果。
    const flatUsed = "targets" in root || "typeIDs" in root || "model" in root;
    const hasTargets =
      (Array.isArray(root.targets) && root.targets.length > 0) ||
      (Array.isArray(root.typeIDs) && root.typeIDs.length > 0);
    if (flatUsed && !hasTargets) {
      errors.push(
        "config.json: the flat rule has neither targets nor typeIDs, so it can never match and nothing would happen." +
          " Add typeIDs (or targets), or write the entries form.",
      );
    }
    return { errors, warnings };
  }

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
    if (CHARACTER_ACTIONS.has(action)) {
      // 角色规则的字段与物品不同：模型文件名、额外倍数、手臂是否绑骨。
      // 最常见的静默无效果是**忘写 model**：模板会退回默认 model.glb，文件不存在就什么也不发生。
      if (typeof e.model !== "string" || !e.model.trim()) {
        errors.push(`${at} (${action}): model is required (the GLB file name next to the DLL)`);
      }
      if ("scale" in e && typeof e.scale !== "number") errors.push(`${at}: scale must be a number`);
      if ("bindArms" in e && typeof e.bindArms !== "boolean") errors.push(`${at}: bindArms must be true or false`);
      return;
    }
    if (NOT_IMPLEMENTED_ACTIONS.has(action)) {
      // `new_item`（新增物品）是**下一步**。数据层（`ItemFactory`）对物品也适用，
      // 但物品模板 `reference/item_model/ModBehaviour.cs` 还没接 `action`。
      // 写进 config 后，模板把它当成「没有 targets 的替换规则」，匹配永远为假，
      // 于是**静默无效果** —— 装上游戏却什么也不发生，最难查。
      // 所以这里直接报错，不得让它悄悄过。
      errors.push(
        `${at}: ${action} is not implemented yet - the item template has no action support, so this rule would silently do nothing. Use new_weapon, or one of the replace actions.`,
      );
      return;
    }
    // 剩下的 `replace_*`：不做新增类校验，但**要查它认不认得出目标**。
    // 一条既没有 targets 也没有 typeIDs 的替换规则，模板里 `Matches` 恒假，
    // 于是**静默无效果**（装上游戏什么也不发生）。最常见的原因是忘写 `action`：
    // 条目写的是 newTypeId / cloneFromItemId 那套，而 action 缺省成了 replace_weapon。
    if (!NEW_ACTIONS.has(action)) {
      const hasTargets =
        (Array.isArray(e.targets) && e.targets.length > 0) || (Array.isArray(e.typeIDs) && e.typeIDs.length > 0);
      if (!hasTargets) {
        errors.push(
          `${at} (${action}): this rule has neither targets nor typeIDs, so it can never match and nothing would happen.` +
            ` If you meant to add a new weapon, write action: "new_weapon".`,
        );
      }
      return;
    }

    const zh = `${at} (${action})`;
    const what = action === "new_weapon" ? "gun" : "item";
    if (!isPosInt(e.cloneFromItemId))
      errors.push(`${zh}: cloneFromItemId is required and must be a positive typeID (the ${what} to clone)`);
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

    // 上架到哪个商店：**物品这条路必须显式写**（除 onSale: false）。游戏里有 12 个商店档位加一套独立黑市，
    // 默认上到某一个店是猜的、猜错玩家在正确的店里看不到东西。
    // **武器那条不改**：`PutOnSale` 对武器仍缺省 `Merchant_Weapon`（老约定），所以不写也不报错。
    const onSale = e.onSale !== false;
    const merchant = typeof e.merchantID === "string" ? e.merchantID.trim() : "";
    if (onSale && !merchant && action === "new_item")
      errors.push(
        `${zh}: merchantID is required when the item is on sale - there are 12 shop profiles plus a black market,` +
          ` so do not default to one; ask the player which shop (or write "onSale": false)`,
      );
    if (merchant && gameNames && gameNames.merchants && !gameNames.merchants.has(merchant))
      errors.push(
        `${zh}: merchantID "${merchant}" is not a shop profile in the game - list them with` +
          ` inspect_game_data (class=StockShop, field=merchantID)`,
      );

    if (e.maxStock != null && !isPosInt(e.maxStock)) errors.push(`${zh}: maxStock must be a positive integer`);
    if (e.priceFactor != null && !(typeof e.priceFactor === "number" && e.priceFactor > 0)) errors.push(`${zh}: priceFactor must be a number > 0`);

    // 额外标记（Additional Tag）。**追加不是替换**：品类 tag 从原型继承，这里加的是在它之上的标记。
    // tag 同时决定功能（可修、可卖、插槽准入）与掉落池，所以名字写错不是小事。
    if (e.tags != null) {
      if (!Array.isArray(e.tags)) errors.push(`${zh}: tags must be an array of tag names such as ["ShowCase"]`);
      else
        for (const t of e.tags as unknown[]) {
          if (typeof t !== "string" || !t.trim()) errors.push(`${zh}: every tags entry must be a non-empty tag name`);
          else if (gameNames && gameNames.tags && !gameNames.tags.has(t.trim()))
            errors.push(
              `${zh}: tag "${t}" does not exist in the game - list them with inspect_game_data (class=Tag, field=m_Name).` +
                ` Note that tags are ADDED on top of the prototype's own tags`,
            );
        }
    }

    // 物品专有的数字（0 / 负数 = 不改，沿用原型）
    if (e.maxStackCount != null && !isPosInt(e.maxStackCount))
      errors.push(`${zh}: maxStackCount must be a positive integer, or leave it out`);
    if (e.value != null && !(Number.isInteger(e.value) && (e.value as number) >= 0))
      errors.push(`${zh}: value must be an integer >= 0, or leave it out`);
    if (e.weight != null && !(typeof e.weight === "number" && e.weight >= 0))
      errors.push(`${zh}: weight must be a number >= 0, or leave it out`);

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
        errors.push(`${zh}: cloneFromItemId ${e.cloneFromItemId} does not exist in the game - pick a real ${what} (inspect_game_data)`);
      if (isPosInt(e.newTypeId) && gameIds.has(e.newTypeId))
        errors.push(
          `${zh}: newTypeId ${e.newTypeId} is already taken by the game - pick a free id, or leave newTypeId out and let the game assign one`,
        );
    }
  });
  return { errors, warnings };
}
