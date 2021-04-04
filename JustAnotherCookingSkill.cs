﻿using System;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using JotunnLib.Entities;
using JotunnLib.Managers;
using JotunnLib.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace JustAnotherCookingSkill
{
    [BepInPlugin("com.bepinex.plugins.farefray.justanothercookingskill", "Just Another Cooking Skill", "0.0.3")]
    [BepInDependency("com.bepinex.plugins.jotunnlib")]
    class JustAnotherCookingSkill : BaseUnityPlugin
    {
        public const string PluginGUID = "farefray.rpgmodpack.valheim.JustAnotherCookingSkill";
        public static string ModName = "Just Another Cooking Skill";

        private static Harmony harmony;

        public static ConfigEntry<bool> modEnabled;

        private static ConfigEntry<float> cookingStaticExperienceGain;
        private static ConfigEntry<float> cauldronStaticExperienceGain;

        private static Skills.SkillType COOKING_SKILL_TYPE = 0;
        private static ConfigEntry<int> foodIncreasePerQuality;
        private static List<string> affectedFood = new List<string> {
            // cooking station
            "CookedMeat", "CookedLoxMeat", "SerpentMeatCooked", "FishCooked", "NeckTailGrilled",
            // cauldron
            "QueensJam", "BloodPudding", "Bread", "FishWraps", "LoxPie", "Sausages", "CarrotSoup", "TurnipStew", "SerpentStew",
            // Jams by RandyKnapp
            "RaspberryJam", "HoneyRaspberryJam", "BlueberryJam", "HoneyBlueberryJam", "CloudberryJam", "HoneyCloudberryJam", "KingsJam", "NordicJam",
            // TEST
            "NonExistingItemToTestItsOkay"
        };
        private static string[] qualityPrefixes = { "awful", "poorly", "" /** Thats normal food variant **/, "well", "deliciously" };

        private static string[] qualitiesChanceBucket = { }; // thats, most likely horrible but funny way to generate random
        private static System.Random random = new System.Random();

        // Init handlers
        private void Awake()
        {
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable the mod.");
            if (!modEnabled.Value)
                return;

            foodIncreasePerQuality = Config.Bind<int>("Values Config", "foodIncreasePerQuality", 15, "For which amount better/worse quality food will be better/worse than previous");
            cookingStaticExperienceGain = Config.Bind<float>("Values Config", "cookingStaticExperienceGain", 1f, "Cooking skill experience gained when using the Cooking Station.");
            cauldronStaticExperienceGain = Config.Bind<float>("Values Config", "cauldronStaticExperienceGain", 2f, "Cooking skill experience gained when using the Cauldron.");

            // chances to qualify variants for meals
            int[] qualityChancesArray = { 75, 40, 35, 20, 10 }; // should match Length of qualityPrefixes
            for (int qualityIndex = 0; qualityIndex < qualityChancesArray.Length; qualityIndex++)
            {
                string[] repeatedQualities = Enumerable.Repeat(qualityPrefixes[qualityIndex], qualityChancesArray[qualityIndex]).ToArray();
                qualitiesChanceBucket = qualitiesChanceBucket.Concat(repeatedQualities).ToArray();
            }

            // maybe we should register those same as skills? cuz it gives warning on startScreen about prefabs missing
            ObjectManager.Instance.ObjectRegister += registerObjects;
            PrefabManager.Instance.PrefabRegister += registerPrefabs;
            registerSkills();

            harmony = new Harmony(PluginGUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        private static void Log(object msg)
        {
            Debug.Log($"[{ModName}] {msg.ToString()}");
        }

        // Register new prefabs
        private void registerPrefabs(object sender, EventArgs e)
        {
            foreach (string foodName in affectedFood)
            {
                for (int index = 0; index < qualityPrefixes.Length; index++)
                {
                    // check for base prefab existance
                    if (ObjectDB.instance.GetItemPrefab(foodName) == null)
                    {
                        Log($"No prefab registered for meal: {foodName}");
                        continue;
                    }

                    ItemDrop item = ObjectManager.Instance.GetItemDrop(foodName);
                    if (item == null)
                    {
                        // when no base meal prefab exists, we just skip that part
                        continue;
                    }

                    GameObject prefab;

                    string qualityPrefix = qualityPrefixes[index];
                    bool isNormalQuality = qualityPrefix == "";
                    if (!isNormalQuality)
                    {
                        // skipping default quality mostly, cuz those items are already exists,
                        // but modyfing to match the rest
                        string qualifiedPrefabName = qualityPrefix + foodName;

                        // create new quality items
                        prefab = PrefabManager.Instance.CreatePrefab(qualifiedPrefabName, foodName);
                        // prefab.gameObject.name = qualifiedPrefabName;

                        item = prefab.GetComponent<ItemDrop>();
                        item.m_itemData.m_dropPrefab = prefab;
                    }


                    // setting quality representation for food
                    item.m_itemData.m_quality = index;
                    item.m_itemData.m_shared.m_maxQuality = qualityPrefixes.Length;
                   
                    if (isNormalQuality)
                    {
                        // for items which already exist, we're done here
                        continue;
                    }

                    string new_m_name = qualityPrefix + item.m_itemData.m_shared.m_name.Substring(1);
                    // TODO: this should be moved into Awake
                    LocalizationManager.Instance.RegisterTranslation(new_m_name,
                        Localization.instance.Localize(item.m_itemData.m_shared.m_name)); // same localization name as base

                    item.m_itemData.m_shared.m_name = "$" + new_m_name;
                    item.m_itemData.m_shared.m_description += " (" + qualityPrefix + " cooked)";
                   
                    // increase/decrease stats
                    float baseFoodHealth = item.m_itemData.m_shared.m_food;
                    item.m_itemData.m_shared.m_food =
                        (int)Math.Round(baseFoodHealth * ((100f + (index * foodIncreasePerQuality.Value) - 20) / 100f));

                    float baseFoodStamina = item.m_itemData.m_shared.m_foodStamina;
                    item.m_itemData.m_shared.m_foodStamina = 
                        (int)Math.Round(baseFoodStamina * ((100f + (index * foodIncreasePerQuality.Value) - 20) / 100f));

                    // increase regen for better quality items
                    if (index > 2)
                    {
                        item.m_itemData.m_shared.m_foodRegen += index - 2;
                    }
                }
            }
        }

        // Register new items and recipes
        private void registerObjects(object sender, EventArgs e)
        {
            foreach (string foodName in affectedFood)
            {
                for (int index = 0; index < qualityPrefixes.Length; index++)
                {
                    if (index == 2 || ObjectDB.instance.GetItemPrefab(foodName) == null)
                    {
                        // skipping empty quality, cuz those items are already exists by default
                        continue;
                    }

                    ObjectManager.Instance.RegisterItem(qualityPrefixes[index] + foodName);
                    Log($"Registering {qualityPrefixes[index] + foodName}");
                }
            }
        }


        // Register new skills
        void registerSkills()
        {
            string spritePath = "JustAnotherCookingSkill/assets/just_another_cooking_skill.png";
            Texture2D cookingSkillTex = AssetUtils.LoadTexture(spritePath);
            if (!cookingSkillTex)
            {
                Debug.LogError($"Cannot find sprite for just another cooking skill - {spritePath}");
                return;
            }

            Sprite CookingSkillSprite = Sprite.Create(cookingSkillTex, new Rect(0f, 0f, cookingSkillTex.width, cookingSkillTex.height), Vector2.zero);
            string description = "Improves chance to prepare better quality food.";
            string name = "Cooking";

            // skill id: 8253696
            COOKING_SKILL_TYPE = SkillManager.Instance.RegisterSkill(new SkillConfig()
            {
                Identifier = PluginGUID,
                Name = name,
                Description = description,
                IncreaseStep = 1f,
                Icon = CookingSkillSprite
            });


            LocalizationManager.Instance.RegisterTranslation("skill_" + COOKING_SKILL_TYPE, name);
            LocalizationManager.Instance.RegisterTranslation("skill_" + COOKING_SKILL_TYPE + "_description", description);
        }

        static void raiseCookingSkill(Humanoid user, float amount)
        {
           /**
           * Skill xp to compare:
           * lvl 1 = 1.91
           * lvl 5 = 7.84
           * lvl 10 = 18.74
           * lvl 15 = 32.5
           * lvl 25 = 66.78
           * lvl 50 = 182.6
           * lvl 75 = 331.77
           * lvl 99 = 500
           */

            ((Player)user).RaiseSkill(COOKING_SKILL_TYPE, amount);
        }

        static string getRandomQualityBasedOnSkill(Humanoid user)
        {
            // thats probably stupid way to make skill affect the result - could be optimized
            int skillLevel = (int)Math.Round(user.GetSkillFactor(COOKING_SKILL_TYPE) * 100); // 0.01 = 1 lvl, 0.99 = 99 lvl

            int maximumValue = qualitiesChanceBucket.Length - 1;
            int randomSeed = random.Next(0, maximumValue);

            randomSeed += random.Next(0, skillLevel); // increasing randomed value by skill

            return randomSeed >= maximumValue ? qualitiesChanceBucket[maximumValue] : qualitiesChanceBucket[randomSeed];
        }       

        // ==================================================================== //
        //              FOOD PATCHES                                       //
        // ==================================================================== //

        #region Food eating Patches

        static string getBaseFoodMName(ItemDrop.ItemData item)
        {
            string foodPrefix = qualityPrefixes[item.m_quality];
            return foodPrefix != "" ? item.m_shared.m_name.Replace(foodPrefix, "") : item.m_shared.m_name; // contains base food name of item we are trying to eat. F.e. CookedMeat if we are trying to eat wellCookedMeat
        }

        // Do not let eat multiple food qualities
        [HarmonyPatch(typeof(Player), "CanEat")]
        internal class Patch_Player_CanEat
        {   
            static bool Prefix(ref bool __result, ref Player __instance, ref List<Player.Food> ___m_foods, ref ItemDrop.ItemData item, bool showMessages)
            {
                bool isOurFoodUsed = item.m_shared.m_maxQuality == qualityPrefixes.Length; // quite tricky way to figure out if food droploots were edited by our mod. Could be improved.

                if (!isOurFoodUsed) {
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
                    raiseCookingSkill(user, cookingStaticExperienceGain.Value * 0.5f);
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
                        raiseCookingSkill(user, cookingStaticExperienceGain.Value * 0.5f);

                        string qualityPrefix = getRandomQualityBasedOnSkill(user);

                        // check if such object exist
                        string qualifyMealName = qualityPrefix + itemName;
                        if (!ObjectManager.Instance.GetItemDrop(qualifyMealName))
                        {
                            Log($"No object registered for qualify meal: {qualifyMealName}");
                            return true;
                        }

                        // instead of processing normal food, we spawn qualified variant
                        ReflectionUtils.InvokePrivate(__instance, "SpawnItem", new object[] { qualifyMealName });
                     

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
                if (___m_craftRecipe.m_item.m_itemData.m_shared.m_maxQuality == qualityPrefixes.Length)
                {
                    raiseCookingSkill(player, cauldronStaticExperienceGain.Value);

                    string itemName = ___m_craftRecipe.m_item.gameObject.name;
                    string qualityPrefix = getRandomQualityBasedOnSkill(player);

                    // check if such object exist
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
                }

                return true;
            }
        }

        #endregion
    }
}
