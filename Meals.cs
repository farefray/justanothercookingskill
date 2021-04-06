using System.Collections.Generic;
using UnityEngine;
using ValheimLib;
using ValheimLib.ODB;
using System.Linq;
using System;

namespace JustAnotherCookingSkill.Meals
{
    internal static class Cooking
    {
        //  "tasty", "deliciously", "divine"
        public static string[] qualityPrefixes = { ""/** default quality **/, "well", "tasty", "deliciously", "divine" };

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

        internal static void Init()
        {
            ObjectDBHelper.OnAfterInit += registerPrefabs;
        }

        private static void registerPrefabs()
        {
            foreach (string foodName in affectedFood)
            {
                // we skip default value(0)
                for (int index = 1; index < qualityPrefixes.Length; index++)
                {
                    // check for base prefab existance
                    ItemDrop basePrefab = Prefab.Cache.GetPrefab<ItemDrop>(foodName);
                    if (basePrefab == null)
                    {
                        Log.LogInfo($"No prefab registered for meal: {foodName}");
                        continue;
                    }

                    string qualityPrefix = qualityPrefixes[index];
                
                    // skipping default quality mostly, cuz those items are already exists,
                    // but modyfing to match the rest
                    string qualifiedPrefabName = foodName + "_" + qualityPrefix;
                    Log.LogInfo($"trying to make clone named: {qualifiedPrefabName}");

                    // create new quality items
                    GameObject clonedBasePrefabObject = basePrefab.gameObject.InstantiateClone(qualifiedPrefabName);
                    CustomItem newMealItem = new CustomItem(clonedBasePrefabObject, fixReference: true);

                    newMealItem.ItemDrop.m_itemData.m_shared.m_name = $"${qualifiedPrefabName}";
                    Log.LogInfo($"newMealItem named: {newMealItem.ItemDrop.m_itemData.m_shared.m_name}");

                    // setting quality representation for food
                    newMealItem.ItemDrop.m_itemData.m_quality = index;
                    newMealItem.ItemDrop.m_itemData.m_shared.m_maxQuality = qualityPrefixes.Length;

                    // increase/decrease stats
                    float baseFoodHealth = newMealItem.ItemDrop.m_itemData.m_shared.m_food;
                    newMealItem.ItemDrop.m_itemData.m_shared.m_food =
                        (int)Math.Round(baseFoodHealth * ((100f + (index * JustAnotherCookingSkill.foodIncreasePerQuality.Value) - 20) / 100f));

                    float baseFoodStamina = newMealItem.ItemDrop.m_itemData.m_shared.m_foodStamina;
                    newMealItem.ItemDrop.m_itemData.m_shared.m_foodStamina =
                        (int)Math.Round(baseFoodStamina * ((100f + (index * JustAnotherCookingSkill.foodIncreasePerQuality.Value) - 20) / 100f));

                    // increase regen for better quality items
                    if (index > 2)
                    {
                        newMealItem.ItemDrop.m_itemData.m_shared.m_foodRegen += index - 2;
                    }

                    ObjectDBHelper.Add(newMealItem);
                }
            }
        }

    }
}