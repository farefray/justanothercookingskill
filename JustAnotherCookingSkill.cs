using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Pipakin.SkillInjectorMod;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.IO;

namespace JustAnotherCookingSkill
{
    [BepInPlugin(PluginGUID, ModName, ModVer)]
    [BepInDependency("com.pipakin.SkillInjectorMod")]
    [BepInDependency(ValheimLib.ValheimLib.ModGuid)]
    class JustAnotherCookingSkill : BaseUnityPlugin
    {
        public const string PluginGUID = "farefray.rpgmodpack.valheim.JustAnotherCookingSkill";
        public const string ModName = "Just Another Cooking Skill";
        private const string ModVer = "0.9.9";
        internal static JustAnotherCookingSkill Instance { get; private set; }

        const int COOKING_SKILL_TYPE = 8253696;

        private static Dictionary<string, Texture2D> cachedTextures = new Dictionary<string, Texture2D>();

        public static ConfigEntry<bool> modEnabled;

        public static ConfigEntry<float> cookingStaticExperienceGain;
        public static ConfigEntry<float> cauldronStaticExperienceGain;
        public static ConfigEntry<int> foodIncreasePerQuality;

        private static Texture2D LoadTexture(string filepath)
        {
            if (cachedTextures.ContainsKey(filepath))
            {
                return cachedTextures[filepath];
            }
            Texture2D texture2D = new Texture2D(0, 0);
            ImageConversion.LoadImage(texture2D, File.ReadAllBytes(filepath));
            return texture2D;
        }

        private static Sprite LoadCustomTexture()
        {
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filepath = Path.Combine(directoryName, "/JustAnotherCookingSkill/assets/just_another_cooking_skill.png");
            if (File.Exists(filepath))
            {
                Texture2D texture2D = LoadTexture(filepath);
                return Sprite.Create(texture2D, new Rect(0f, 0f, 50f, 50f), Vector2.zero);
            }
            else
            {
                Debug.LogError("Unable to load skill icon! Make sure you place the /assets/just_another_cooking_skill.png file in the plugins directory!");
                return null;
            }
        }

        // Init handlers
        private void Awake()
        {
            Instance = this;

            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable the mod.");
            if (!modEnabled.Value)
                return;

            cookingStaticExperienceGain = Config.Bind<float>("Values Config", "cookingStaticExperienceGain", 1f, "Cooking skill experience gained when using the Cooking Station.");
            cauldronStaticExperienceGain = Config.Bind<float>("Values Config", "cauldronStaticExperienceGain", 2f, "Cooking skill experience gained when using the Cauldron.");
            foodIncreasePerQuality = Config.Bind<int>("Values Config", "foodIncreasePerQuality", 15, "For which amount better/worse quality food will be better/worse than previous");

           
            Log.Init(Logger);

            Meals.Cooking.Init();

            Hooks.Cooking.Init();

            registerSkills();
        }

        private void registerSkills()
        {
            // Skills registration
            string description = "Improves chance to prepare better quality food.";
            string name = "Cooking";

            SkillInjector.RegisterNewSkill(COOKING_SKILL_TYPE, name, description, 1.0f, LoadCustomTexture());
        }

        /// <summary>
        /// Destroying the attached Behaviour will result in the game or Scene receiving OnDestroy.
        /// OnDestroy occurs when a Scene or game ends.
        /// It is also called when your mod is unloaded, this is where you do clean up of hooks, harmony patches,
        /// loose GameObjects and loose monobehaviours.
        /// Loose here refers to gameobjects not attached
        /// to the parent BepIn GameObject where your BaseUnityPlugin is attached
        /// </summary>
        private void OnDestroy()
        {
            Hooks.Cooking.Disable();
        }


        public static void raiseCookingSkill(Humanoid user, float amount)
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

            try
            {
                ((Player)user).RaiseSkill((Skills.SkillType)COOKING_SKILL_TYPE, amount);
            }
            catch (Exception e)
            {
                Debug.LogError("Error increasing fitness skill: " + e.ToString());
            }
        }

        public static string getQualityBasedOnSkill(Humanoid user)
        {
            // thats probably stupid way to make skill affect the result - could be optimized
            int skillLevel = (int)Math.Round(user.GetSkillFactor((Skills.SkillType)COOKING_SKILL_TYPE) * 100); // 0.01 = 1 lvl, 0.99 = 99 lvl
            
            // TODO: base this on food quality
            if (skillLevel > 3 && skillLevel <= 7)
            {
                return Meals.Cooking.qualityPrefixes[0];
            } else if (skillLevel > 7)
            {
                return Meals.Cooking.qualityPrefixes[0];
            }

            return "";
        }       

        
    }
}
