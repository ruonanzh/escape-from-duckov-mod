// ItemHooks.cs —— 用 Harmony 挂钩子，代替轮询扫描。
//
// 为什么：`FindObjectsOfType` 与 `Resources.FindObjectsOfTypeAll` 都是整场景枚举。
// 物品模板原来每 0.5 秒各做一次（物品扫描 + 图形克隆扫描）。这类 mod 有 3 个，就是每秒 6 次。
//
// 两个挂钩点（都是反编译实证）：
//   · `ItemGraphicInfo.CreateAGraphic` 的最后一步必然是 `itemGraphicInfo.Setup(item)`。
//     四个创建者都走它：`InteractablePickup.CreateGraphic`（掉落）、`Duckov.LotteryBox.Get`、
//     `Duckov.Buildings.Showcase`、`Duckov.Aquariums.Aquarium.GetOrCreateGraphic`。
//   · 手持那条分两半：
//     - 枪：`ItemExtensions.CreateHandheldAgent` 在两条分支之后都调 `Setup(itemInstance)`。
//     - 非枪：`CreateAgent` → `BindNewAgent` → `ItemAgent.Initialize(item, agentType)`，**没有** `Setup`。
//     所以挂 `Setup` 只能覆盖枪，非枪必须再挂 `Initialize`。
//
// 两条**不覆盖**的路径（靠别的手段兜，见文件末尾注释）：
//   · `useSpriteForPickup` 为真时，`InteractablePickup` 直接 Instantiate 精灵预制体，不调 `Setup`。
//   · 图形已经造好的（热重载之后）。那时创建事件早就过去了。

using System;
using HarmonyLib;
using UnityEngine;

namespace ModelKit
{
    public static class ItemHooks
    {
        /// <summary>世界图形造好时调用。参数是物品，与它刚得到的图形。</summary>
        public static Action<ItemStatsSystem.Item, ItemGraphicInfo> OnGraphicReady;

        /// <summary>手持实体建好时调用。参数是物品，与它的 agent。</summary>
        public static Action<ItemStatsSystem.Item, ItemStatsSystem.ItemAgent> OnAgentReady;

        static bool _installed;
        static string _report = "not installed";

        /// <summary>钩子装上了吗、装的结果如何（诊断用）。</summary>
        public static string Report => _report;

        /// <summary>装钩子。幂等：多调只有第一次生效。
        ///
        /// 同一台机器上多个 mod 会各装一次，Harmony 会把 postfix 叠加起来（它支持）。
        /// **不得**让失败抛出去：钩子装不上时 mod 要能继续跑（退回低频扫描）。</summary>
        public static bool Install()
        {
            if (_installed) return true;
            try
            {
                var h = new Harmony("gamer-agent-pi.mod-kit.item-hooks");

                var setup = AccessTools.Method(typeof(ItemGraphicInfo), "Setup", new[] { typeof(ItemStatsSystem.Item) });
                if (setup != null)
                {
                    h.Patch(setup, postfix: new HarmonyMethod(AccessTools.Method(typeof(ItemHooks), nameof(PostSetup))));
                }

                var init = AccessTools.Method(
                    typeof(ItemStatsSystem.ItemAgent), "Initialize",
                    new[] { typeof(ItemStatsSystem.Item), typeof(ItemStatsSystem.ItemAgent.AgentTypes) });
                if (init != null)
                {
                    h.Patch(init, postfix: new HarmonyMethod(AccessTools.Method(typeof(ItemHooks), nameof(PostInitialize))));
                }

                _installed = setup != null && init != null;
                _report = $"setup patched={setup != null}, initialize patched={init != null}";
                if (!_installed) Debug.LogWarning("[ModelKit] hooks: " + _report + " (falling back to the low-frequency sweep)");
                return _installed;
            }
            catch (Exception e)
            {
                _report = "install failed: " + e.Message;
                Debug.LogWarning("[ModelKit] hooks: " + _report + " (falling back to the low-frequency sweep)");
                return false;
            }
        }

        /// <summary>`ItemGraphicInfo.Setup(Item)` 的 postfix。世界图形与枪的手持都走这里。</summary>
        static void PostSetup(ItemGraphicInfo __instance, ItemStatsSystem.Item __0)
        {
            try
            {
                var cb = OnGraphicReady;
                if (cb != null && __0 != null && __instance != null) cb(__0, __instance);
            }
            catch (Exception e) { Debug.LogWarning("[ModelKit] OnGraphicReady threw: " + e.Message); }
        }

        /// <summary>`ItemAgent.Initialize(Item, AgentTypes)` 的 postfix。所有手持实体走这里。</summary>
        static void PostInitialize(ItemStatsSystem.ItemAgent __instance, ItemStatsSystem.Item __0)
        {
            try
            {
                var cb = OnAgentReady;
                if (cb != null && __0 != null && __instance != null) cb(__0, __instance);
            }
            catch (Exception e) { Debug.LogWarning("[ModelKit] OnAgentReady threw: " + e.Message); }
        }
    }
}
