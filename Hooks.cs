using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using ValheimLib;

namespace JustAnotherCookingSkill.Hooks
{
    internal static class Cooking
    {
        internal static Harmony HarmonyInstance;

        /// <summary>
        /// Show that you can use either monomod hooks, or harmony patches for hooking game methods.
        /// </summary>
        internal static void Init()
        {
            EnableMonoModHooks();

            EnableHarmonyPatches();
        }

        /// <summary>
        /// Disable the enabled hooks.
        /// </summary>
        internal static void Disable()
        {
            DisableMonoModHooks();

            DisableHarmonyPatches();
        }

        private static void EnableMonoModHooks()
        {
            On.FejdStartup.Awake += OnFejdStartupAwakeMonoModHookShowcase;
        }

        internal static void DisableMonoModHooks()
        {
            On.FejdStartup.Awake -= OnFejdStartupAwakeMonoModHookShowcase;
        }

        private static void EnableHarmonyPatches()
        {
            HarmonyInstance = new Harmony(JustAnotherCookingSkill.PluginGUID);
            HarmonyInstance.PatchAll();
        }

        private static void DisableHarmonyPatches()
        {
            HarmonyInstance.UnpatchSelf();
        }

        private static void OnFejdStartupAwakeMonoModHookShowcase(On.FejdStartup.orig_Awake orig, FejdStartup self)
        {

            // calling the original method
            orig(self);

            Log.LogInfo("Hello from a monomod hook, this method is fired after the original method is called : " + self.m_betaText);
        }

        // ==================================================================== //
        //              FOOD PATCHES                                       //
        // ==================================================================== //

        #region Food eating Patches

        static string getBaseFoodMName(ItemDrop.ItemData item)
        {
            string foodPrefix = Meals.Cooking.qualityPrefixes[item.m_quality];
            return foodPrefix != "" ? item.m_shared.m_name.Replace(foodPrefix, "") : item.m_shared.m_name; // contains base food name of item we are trying to eat. F.e. CookedMeat if we are trying to eat wellCookedMeat
        }

        // Do not let eat multiple food qualities
        [HarmonyPatch(typeof(Player), "CanEat")]
        internal class Patch_Player_CanEat
        {
            static bool Prefix(ref bool __result, ref Player __instance, ref List<Player.Food> ___m_foods, ref ItemDrop.ItemData item, bool showMessages)
            {
                bool isOurFoodUsed = item.m_shared.m_maxQuality == Meals.Cooking.qualityPrefixes.Length; // quite tricky way to figure out if food droploots were edited by our mod. Could be improved.

                if (!isOurFoodUsed)
                {
                    return true;
                }

                string ourFoodBase = getBaseFoodMName(item);

                // checking if we have already active food of same base (dont let eat Well Cooked Meat when Poorly Cooked Meat still active)
                foreach (Player.Food food in ___m_foods)
                {
                    string existingFoodBase = getBaseFoodMName(food.m_item);

                    if (ourFoodBase == existingFoodBase)
                    {
                        if (food.CanEatAgain())
                        {
                            food.m_health = 0f; // dirty way to finish effect of base food, so our food may replace it
                            return true;
                        }

                        __instance.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_nomore", new string[]
                        {
                            item.m_shared.m_name
                        }), 0, null);

                        __result = false;
                        return false;
                    }
                }

                return true;
            }
        }

        #endregion

        // ==================================================================== //
        //              COOKING STATION PATCHES                                 //
        // ==================================================================== //

        #region Cooking Station Patches

        // increase cooking skill when placing an item on the cooking station
        [HarmonyPatch(typeof(CookingStation), "UseItem")]
        internal class Patch_CookingStation_UseItem
        {
            static void Postfix(ref bool __result, Humanoid user)
            {
                if (__result)
                {
                   JustAnotherCookingSkill.raiseCookingSkill(user, JustAnotherCookingSkill.cookingStaticExperienceGain.Value * 0.5f);
                }
            }
        }

        // increase cooking skill + cook different-qualify items based on skill
        [HarmonyPatch(typeof(CookingStation), "Interact")]
        internal class Patch_CookingStation_Interact
        {
            static bool Prefix(ref CookingStation __instance, ref ZNetView ___m_nview, Humanoid user, bool hold)
            {
                if (hold)
                    return false;

                Traverse t_cookingStation = Traverse.Create(__instance);
                ZDO zdo = ___m_nview.GetZDO();

                for (int slot = 0; slot < __instance.m_slots.Length; ++slot)
                {
                    string itemName = zdo.GetString(nameof(slot) + slot);
                    bool isItemDone = t_cookingStation.Method("IsItemDone", itemName).GetValue<bool>();

                    if (itemName != "" && itemName != __instance.m_overCookedItem.name && isItemDone)
                    {
                        JustAnotherCookingSkill.raiseCookingSkill(user, JustAnotherCookingSkill.cookingStaticExperienceGain.Value * 0.5f);

                        string qualityPrefix = JustAnotherCookingSkill.getQualityBasedOnSkill(user);

                        // check if such object exist
                        string qualifyMealName = qualityPrefix + itemName;
                        if (Prefab.Cache.GetPrefab<ItemDrop>(qualifyMealName) == null)
                        {
                            Log.LogError($"No object registered for qualify meal: {qualifyMealName}");
                            return true;
                        }

                        // instead of processing normal food, we spawn qualified variant
                        MethodInfo method = __instance.GetType().GetMethod("SpawnItem");
                        if (method == null)
                        {
                            Log.LogError("Method SpawnItem does not exist on type CookingStation");
                            return true;
                        }

                        method.Invoke(__instance, new object[] { qualifyMealName });

                        zdo.Set("slot" + slot, "");

                        ___m_nview.InvokeRPC(ZNetView.Everybody, "SetSlotVisual", new object[]
                        {
                            slot,
                            ""
                        });

                        // Unfortunately we replace this hook totally and extra modifications will conflict on those. I gonna find better way
                        return false;
                    }
                }

                return true;
            }
        }

        #endregion

        // ==================================================================== //
        //              CAULDRON PATCHES                                        //
        // ==================================================================== //

        #region Cauldron Patches

        // increase cooking skill when making food in the cauldron
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        public static class InventoryGui_DoCrafting_Patch
        {
            public static bool Prefix(InventoryGui __instance,
                ref Recipe ___m_craftRecipe,
                ref ItemDrop.ItemData ___m_craftUpgradeItem,
                Player player)
            {
                if (___m_craftRecipe == null)
                {
                    return true;
                }

                bool isCauldronRecipe = ___m_craftRecipe.m_craftingStation?.m_name == "$piece_cauldron";
                bool haveRequirements = player.HaveRequirements(___m_craftRecipe, false, 1) || player.NoCostCheat();

                if (!isCauldronRecipe ||
                    !haveRequirements ||
                    ___m_craftUpgradeItem != null)
                {
                    // thats not our case
                    return true;
                }

                if (!player.GetInventory().HaveEmptySlot())
                {
                    return false; // weird way, but as temp solution
                }

                // isCauldronRecipe + food which has m_maxQuality is our way to figure out that this food is affected by our module
                if (___m_craftRecipe.m_item.m_itemData.m_shared.m_maxQuality == Meals.Cooking.qualityPrefixes.Length)
                {
                    JustAnotherCookingSkill.raiseCookingSkill(player, JustAnotherCookingSkill.cauldronStaticExperienceGain.Value * 0.5f);

                    string itemName = ___m_craftRecipe.m_item.gameObject.name;
                    string qualityPrefix = JustAnotherCookingSkill.getQualityBasedOnSkill(player);

                    // check if such object exist
                    // TODO;
/**
 string qualityMealName = qualityPrefix + itemName;
GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(qualityMealName);
if (itemPrefab == null)
{
    Log($"No prefab registered for qualify meal: {qualityMealName}");
    return true;
}

ItemDrop qualityItemDrop = ObjectManager.Instance.GetItemDrop(qualityMealName);

if (player.GetInventory().AddItem(qualityMealName, ___m_craftRecipe.m_amount, qualityItemDrop.m_itemData.m_quality, 0, player.GetPlayerID(), player.GetPlayerName()) != null)
{
    if (!player.NoCostCheat())
    {
        player.ConsumeResources(___m_craftRecipe.m_resources, 1);
    }

    ReflectionUtils.InvokePrivate(__instance, "UpdateCraftingPanel", new object[] { false });
}

CraftingStation currentCraftingStation = Player.m_localPlayer.GetCurrentCraftingStation();
if (currentCraftingStation)
{
    currentCraftingStation.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity, null, 1f);
}

Game.instance.GetPlayerProfile().m_playerStats.m_crafts++;
Gogan.LogEvent("Game", "Crafted", itemName, (long)1);

return false; // we have already crafted our recipe, skip origin method
**/
}

return true;
}
}

#endregion
}

}
