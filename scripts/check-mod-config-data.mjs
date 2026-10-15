#!/usr/bin/env node
/**
 * check-mod-config-data.mjs —— 校验 `config.json` 数据层的判据（新增武器 / 新增物品）。
 *
 * 为什么要有这个脚本：
 *   这个判据决定 `validate_mod` 报不报错。判错了两边都疼：
 *   漏报 ⇒ agent 把一个坏的 config 装进游戏，玩家看到一把没有名字或占号的枪；
 *   误报 ⇒ agent 白改一通（实测踩过一次：分页读游戏号时把 662 误判成不存在）。
 *   所以把它变成机器检查。
 *
 * 它调用**同一个文件**（`.pi/lib/mod-config-data.ts`）。扩展也导入那份。判据只有一份，不会漂。
 *
 * 用法：node --experimental-strip-types scripts/check-mod-config-data.mjs
 * 退出码：0 = 全部符合预期；1 = 有不符合
 */

import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const { CHARACTER_ACTIONS, DISPLAY_QUALITIES, KNOWN_ACTIONS, NEW_ACTIONS, NOT_IMPLEMENTED_ACTIONS, stripJsonComments, validateDataLayer } =
  await import(join(here, "..", ".pi", "lib", "mod-config-data.ts"));

let failed = 0;
const check = (name, expected, got) => {
  const ok = JSON.stringify(expected) === JSON.stringify(got);
  if (!ok) failed += 1;
  console.log(`  ${ok ? "PASS" : "FAIL"}  ${name}`);
  if (!ok) console.log(`        期望 ${JSON.stringify(expected)}，得到 ${JSON.stringify(got)}`);
};

/** 一份好的新增武器 config（各用例在它上面改一处） */
const good = {
  entries: [
    {
      action: "new_weapon",
      cloneFromItemId: 1629,
      displayName: "逻辑与理性",
      newTypeId: 900001,
      merchantID: "Merchant_Weapon",
      maxStock: 1,
      priceFactor: 1,
      quality: 9,
      displayQuality: "None",
      stats: { Damage: 13, Capacity: 25 },
      crafting: { money: 0, materials: { 662: 5 }, tags: ["Special"] },
    },
  ],
};
const withEntry = (patch) => ({ entries: [{ ...good.entries[0], ...patch }] });
const errs = (cfg, ids = null, names = null) => validateDataLayer(cfg, ids, names).errors;
const warns = (cfg, ids = null, names = null) => validateDataLayer(cfg, ids, names).warnings;

/** 游戏里已有的号（模拟 data-probe 读到的集合） */
const GAME = new Set([36, 655, 662, 743, 1629]);   // 743 = Item_fiber（材料，new_item 用例的原型）

/** 游戏里已有的 tag 名与商店档位（模拟 data-probe 现查的结果；**12 个档位，不是 3 个**） */
const NAMES = {
  tags: new Set(["ShowCase", "Collection", "Luxury", "Daily", "Material", "Food", "Armor", "Backpack", "Helmat", "NotSellable"]),
  merchants: new Set([
    "Merchant_Normal",
    "Merchant_Weapon",
    "Merchant_Equipment",
    "Merchant_Ming",
    "Merchant_Mud",
    "Merchant_Fo",
    "Merchant_Myst",
    "Merchant_Myst0",
    "Merchant_MystIsland",
    "Merchant_Island",
    "Merchant_IslandMobile",
  ]),
};

console.log("一份好的 config：");
check("没有错误", [], errs(good, GAME));
check("没有警告", [], warns(good, GAME));

console.log("结构（离线就能判，不需要游戏数据）：");
check("未知的 action", 1, errs({ entries: [{ action: "new_gun", cloneFromItemId: 1629, displayName: "x" }] }).length);
check("缺 cloneFromItemId", 1, errs(withEntry({ cloneFromItemId: undefined })).length);
check("cloneFromItemId 是 0", 1, errs(withEntry({ cloneFromItemId: 0 })).length);
check("缺 displayName（名字键缺失）", 1, errs(withEntry({ displayName: "  " })).length);
check("newTypeId 不是正数", 1, errs(withEntry({ newTypeId: -1 })).length);
check("同一份 config 里 newTypeId 重复", 1, errs({ entries: [good.entries[0], { ...good.entries[0] }] }).length);
check("quality 是负数", 1, errs(withEntry({ quality: -3 })).length);
check("displayQuality 不是枚举里的值", 1, errs(withEntry({ displayQuality: "PurpleRain" })).length);
check("stats 的值是字符串", 1, errs(withEntry({ stats: { Damage: "13" } })).length);
check("stats 不是对象", 1, errs(withEntry({ stats: [13] })).length);
check("merchantID 不认识（要游戏数据）", 1, errs(withEntry({ merchantID: "Merchant_Nope" }), null, NAMES).length);
check("maxStock 是 0", 1, errs(withEntry({ maxStock: 0 })).length);
check("priceFactor 是负数", 1, errs(withEntry({ priceFactor: -1 })).length);
check("size 是 0", 1, errs(withEntry({ size: 0 })).length);
check("crafting.materials 的键不是 typeID", 1, errs(withEntry({ crafting: { materials: { abc: 5 } } })).length);
check("crafting.materials 的数量不是正数", 1, errs(withEntry({ crafting: { materials: { 662: 0 } } })).length);
check("crafting.money 是负数", 1, errs(withEntry({ crafting: { money: -5 } })).length);

console.log("占号（要有游戏数据）：");
check("newTypeId 已经是游戏的号", 1, errs(withEntry({ newTypeId: 655 }), GAME).length);
check("cloneFromItemId 游戏里不存在", 1, errs(withEntry({ cloneFromItemId: 999999 }), GAME).length);
check("材料 typeID 游戏里不存在", 1, errs(withEntry({ crafting: { materials: { 999999: 1 } } }), GAME).length);
check("拿不到游戏数据时这三项不判（不误报）", [], errs(withEntry({ newTypeId: 655, cloneFromItemId: 999999 }), null));
check("拿不到游戏数据时仍然报结构错", 1, errs(withEntry({ displayName: "" }), null).length);

console.log("别的写法：");
check("扁平旧写法不查（由别的检查管）", [], errs({ typeIDs: [655], model: "mp5.glb" }));
check("没有 entries 也不查", [], errs({}));
// H6：扁平写法也要拦「没有目标」的死规则
check("扁平写法有 model 但没有 typeIDs 与 targets 必须报错", 1, errs({ model: "mp5.glb" }).length);
check("扁平写法有 targets 就有效", [], errs({ targets: ["Item_SMG_MP5_Normal"], model: "mp5.glb" }));
check(
  "扁平写法的报错要说清永远不会命中",
  true,
  errs({ model: "mp5.glb" }).some((e) => e.includes("can never match")),
);

// ↓ 这几条是回归守卫：把 NEW_ACTIONS 收窄成只含 new_weapon 时，
//   replace_* 的 entries 曾经被误报成「未实现」。所以每条 replace 写法都得有断言。
check(
  "entries 里的 replace_weapon 不得报错",
  [],
  errs({ entries: [{ action: "replace_weapon", typeIDs: [655], model: "a.glb" }] }, GAME),
);
check(
  "entries 里的 replace_item 不得报错",
  [],
  errs({ entries: [{ action: "replace_item", typeIDs: [36], model: "b.glb" }] }, GAME),
);
check(
  "entries 里不写 action（默认 replace_weapon）不得报错",
  [],
  errs({ entries: [{ typeIDs: [655], model: "a.glb" }] }, GAME),
);
check(
  "混装 replace_weapon 与 new_weapon 不得报错",
  [],
  errs(
    {
      entries: [
        { action: "replace_weapon", typeIDs: [655], model: "a.glb" },
        { action: "new_weapon", newTypeId: 900001, cloneFromItemId: 1629, displayName: "新枪" },
      ],
    },
    GAME,
  ),
);
check(
  "换外观的 entries 不得因为尺寸/插槽被误报",
  [],
  errs(
    { entries: [{ action: "replace_weapon", typeIDs: [655], model: "a.glb", size: 1.0, slots: { muzzle: [0, 0, 0.5] } }] },
    GAME,
  ),
);
check(
  "只写 targets 的替换规则也算能命中",
  [],
  errs({ entries: [{ action: "replace_weapon", targets: ["Item_SMG_MP5_Normal"], model: "a.glb" }] }, GAME),
);
// ↓ 这一条堵的是「忘写 action」：条目写的是新增武器那套字段，但 action 缺省成 replace_weapon，
//   于是没有任何目标，Matches 恒假，静默无效果。
check(
  "替换规则没有 targets 也没有 typeIDs（忘写 action 的典型）必须报错",
  1,
  errs({ entries: [{ newTypeId: 900001, cloneFromItemId: 1629, displayName: "忘写 action", model: "x.glb" }] }, GAME).length,
);
check(
  "那条报错要提示写 action: new_weapon",
  true,
  errs({ entries: [{ cloneFromItemId: 1629, model: "x.glb" }] }, GAME).some((e) => e.includes('new_weapon')),
);
// 2026-10-14：`new_item` 接上了。这条以前断言「必须报错拦下」，现在反过来 ——
// 它必须**被接受**（物品模板已经认 action）。把它放回未实现集合会让这两条红。
check(
  "new_item 必须被接受（不得再报未实现）",
  false,
  errs({ entries: [{ action: "new_item", cloneFromItemId: 36, displayName: "x" }] }).some((e) => e.includes("not implemented")),
);
check(
  "new_item 的合法 config 没有错误",
  [],
  errs(
    {
      entries: [
        {
          action: "new_item",
          cloneFromItemId: 743,
          displayName: "超大纤维束",
          merchantID: "Merchant_Normal",
          tags: ["ShowCase", "Luxury"],
          maxStackCount: 99,
          value: 500,
          weight: 0.35,
        },
      ],
    },
    GAME,
    NAMES,
  ),
);

console.log("新增物品（new_item）专有的判据：");
const item = (patch) => ({
  entries: [{ action: "new_item", cloneFromItemId: 743, displayName: "x", merchantID: "Merchant_Normal", ...patch }],
});
check("缺 merchantID（onSale 缺省为真）必须报错", 1, errs(item({ merchantID: undefined })).length);
check(
  "缺 merchantID 的报错要说清「不得默认商店」",
  true,
  errs(item({ merchantID: undefined })).some((e) => e.includes("do not default to one")),
);
check("写了 onSale: false 就不必写 merchantID", [], errs(item({ merchantID: undefined, onSale: false })));
check("merchantID 是游戏里没有的档位", 1, errs(item({ merchantID: "Merchant_Nope" }), null, NAMES).length);
check(
  "12 个商店档位都认得（不是只认三个）",
  [],
  errs(item({ merchantID: "Merchant_IslandMobile" }), null, NAMES),
);
check("tags 不是数组", 1, errs(item({ tags: "ShowCase" })).length);
check("tags 里有空名字", 1, errs(item({ tags: ["ShowCase", "  "] })).length);
check("tags 里有游戏里不存在的名字", 1, errs(item({ tags: ["NotATag"] }), null, NAMES).length);
check("tags 的名字都对时不报错", [], errs(item({ tags: ["ShowCase", "Collection"] }), null, NAMES));
check("maxStackCount 是 0", 1, errs(item({ maxStackCount: 0 })).length);
check("value 是负数", 1, errs(item({ value: -1 })).length);
check("weight 是负数", 1, errs(item({ weight: -0.5 })).length);
check("不带模型、不带 tags 也能过（全部沿用原型）", [], errs(item({})));
// C1 回归守卫：只拿到一半的游戏名字时，**只跳过那一半**，不得把空集合当成「游戏里没有」。
// （把 GameNames 的字段收回成 Set<string>（非空）时，这两条会红。）
check(
  "只拿到 tags 时，不判 merchantID（不得误报）",
  [],
  errs(item({ merchantID: "Merchant_Nope" }), null, { tags: new Set(["ShowCase"]), merchants: null }),
);
check(
  "只拿到 merchants 时，不判 tag 名（不得误报）",
  [],
  errs(item({ tags: ["NotATag"] }), null, { tags: null, merchants: NAMES.merchants }),
);
check(
  "两样都拿到时照常判（对照）",
  2,
  errs(item({ merchantID: "Merchant_Nope", tags: ["NotATag"] }), null, NAMES).length,
);
// 武器那条「不写 merchantID 也不报错」是**老约定**，不得被物品这条带偏
check(
  "回归守卫：new_weapon 不写 merchantID 仍不得报错（老约定）",
  [],
  errs({ entries: [{ action: "new_weapon", cloneFromItemId: 1629, displayName: "老枪" }] }, GAME),
);
check(
  "entries 与扁平字段同时存在时给一条警告",
  1,
  warns({ ...good, model: "mp5.glb" }, GAME).length,
);

console.log("注释与字面量：");
// 注释被换掉后 JSON 的**含义**必须不变（不去比空白）
check(
  "去掉 // 注释后 JSON 含义不变",
  JSON.parse('{ "a": 1, "b": [2] }'),
  JSON.parse(stripJsonComments('{ // 说明\n "a": 1, // 又一条\n "b": [2] }')),
);
check(
  "字符串里的 // 不得被当成注释",
  '{"model":"https://x/y.glb"}',
  stripJsonComments('{"model":"https://x/y.glb"}'),
);
check("中文注释也去掉", { a: 1 }, JSON.parse(stripJsonComments('{"a":1} // 中文说明')));

console.log("常量与 mod-kit 对齐（数值写错时这里会红）：");
check("品质枚举是九个", 9, DISPLAY_QUALITIES.size);
check(
  "五个 action 都认",
  ["replace_item", "replace_weapon", "new_item", "new_weapon", "replace_character"].sort(),
  [...KNOWN_ACTIONS].sort(),
);
// 角色规则没有 targets / typeIDs，必须走自己的分支，不得被「没有 targets 就静默无效果」拦下。
check("角色规则在自己的集合里", true, CHARACTER_ACTIONS.has("replace_character"));
check("done: 新增那两个都算「新增类」", true, NEW_ACTIONS.has("new_weapon") && NEW_ACTIONS.has("new_item"));
// ↓ 回归守卫：`new_item` 不得被放回「未实现」。放回去 = 校验会拦下它 = 这个功能又没了。
//   （上一次的教训是反过来的：把集合合并，导致 replace_* 被误报成未实现。）
check("回归守卫：new_item 不得在未实现集合里", false, NOT_IMPLEMENTED_ACTIONS.has("new_item"));
check("回归守卫：new_weapon 不得在未实现集合里", false, NOT_IMPLEMENTED_ACTIONS.has("new_weapon"));

console.log(failed ? `\n  ${failed} 条不符合预期` : "\n  全部符合预期");
process.exit(failed ? 1 : 0);
