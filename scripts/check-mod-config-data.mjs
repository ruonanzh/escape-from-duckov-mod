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
const { DISPLAY_QUALITIES, KNOWN_MERCHANTS, stripJsonComments, validateDataLayer } = await import(
  join(here, "..", ".pi", "lib", "mod-config-data.ts")
);

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
const errs = (cfg, ids = null) => validateDataLayer(cfg, ids).errors;
const warns = (cfg, ids = null) => validateDataLayer(cfg, ids).warnings;

/** 游戏里已有的号（模拟 data-probe 读到的集合） */
const GAME = new Set([36, 655, 662, 1629]);

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
check("merchantID 不认识", 1, errs(withEntry({ merchantID: "Merchant_Nope" })).length);
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
check("new_item 还没实现，必须报错拦下（不得静默无效果）", 1, errs({ entries: [{ action: "new_item", cloneFromItemId: 36, displayName: "x" }] }).length);
check(
  "new_item 的报错要说清是没实现",
  true,
  errs({ entries: [{ action: "new_item", cloneFromItemId: 36, displayName: "x" }] }).every((e) => e.includes("not implemented")),
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
check("商人是那三个", ["Merchant_Normal", "Merchant_Ming", "Merchant_Weapon"], [...KNOWN_MERCHANTS]);
check("品质枚举是九个", 9, DISPLAY_QUALITIES.size);

console.log(failed ? `\n  ${failed} 条不符合预期` : "\n  全部符合预期");
process.exit(failed ? 1 : 0);
