using BepInEx;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using static AbilityApi.Api;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AchievementHandler;

namespace AbilityApi.Internal
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        GameObject BowObject;
        public static BoplBody Arrow;
        public struct CircleEntry
        {
            public Sprite sprite;
            public Vector4 center;
        }
        public static bool gotDefaultAbilityCount = false;
        public static int defaultAbilityCount = 30;
        public static Vector4 defaultExtents = new(0.04882813f, 0.04882813f, 0.08300781f, 0.9287109f);
        //public static GunAbility testAbilityPrefab;
        public static Texture2D testAbilityTex;
        public static Sprite testSprite;
        public static List<Texture2D> BackroundSprites = new();
        // For some reason the prefix for dropping abilities on death is called twice
        // so this will prevent an ability from being dropped twice.
        public static Dictionary<string, bool> playersDied = new();
        private void Awake()
        {
            Logger.LogInfo("Plugin AbilityApi is loaded!");
            Harmony harmony = new Harmony("com.Ricksters.AbilityApi");
            Logger.LogInfo("harmony created");
            harmony.PatchAll();
            Logger.LogInfo("AbilityApi Patch Complete!");

            string namespaceName = Assembly.GetExecutingAssembly().GetName().Name;
            string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            // Load backgrounds
            BackroundSprites.Add(LoadImageFromResources(namespaceName, "assets.BlueTeam.png"));
            BackroundSprites.Add(LoadImageFromResources(namespaceName, "assets.OrangeTeam.png"));
            BackroundSprites.Add(LoadImageFromResources(namespaceName, "assets.GreenTeam.png"));
            BackroundSprites.Add(LoadImageFromResources(namespaceName, "assets.PinkTeam.png"));

            // Example of creating a custom ability
            InstantTestAbility instantTestAbilityPrefab = Api.ConstructInstantAbility<InstantTestAbility>("A Ability");
            testAbilityTex = LoadImageFromResources(namespaceName, "assets.BlinkTest.png");
            testSprite = Sprite.Create(testAbilityTex, new Rect(0, 0, testAbilityTex.width, testAbilityTex.height), new Vector2(.5f, .5f));
            NamedSprite testNamedSprite = new NamedSprite("A Custom Ability", testSprite, instantTestAbilityPrefab.gameObject, true);
            Api.RegisterNamedSprites(testNamedSprite);

            GameObject[] array = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];
            GameObject[] array2 = array;

            foreach (GameObject val2 in array2)
            {
                if (((UnityEngine.Object)val2).name == "Bow")
                {
                    BowObject = val2;
                    break;
                }
            }

            if (BowObject == null)
            {
                Debug.Log("Failed to get bow");
                return;
            }
            Component component = BowObject.GetComponent(typeof(BowTransform));
            BowTransform obj = (BowTransform)(object)((component is BowTransform) ? component : null);
            Arrow = (BoplBody)AccessTools.Field(typeof(BowTransform), "Arrow").GetValue(obj);
        }

        public static void AddNamedSprites(NamedSpriteList list)
        {
            if (!gotDefaultAbilityCount)
            {
                defaultAbilityCount = list.sprites.Count;
                gotDefaultAbilityCount = true;
            }
            // Recreate any abilities that could've been destroyed
            for (int i = 0; i < Sprites.Count; i++)
            {
                if (Sprites[i].associatedGameObject != null) continue;
                var obj = ReCreateGameObjects[i]();
                NamedSprite namedSprite = new NamedSprite(Sprites[i].name, Sprites[i].sprite, obj.gameObject, Sprites[i].isOffensiveAbility);
                Sprites[i] = namedSprite;
                NamedSpritesDict[namedSprite.name] = namedSprite;
            }
            if (list.sprites.Count == defaultAbilityCount)
            {
                list.sprites.AddRange(Sprites);
            }
        }

        public static Sprite CreateSpriteWithBackground(Sprite abilitySprite, int playerTeam)
        {
            var TextureWithBackround = Api.OverlayBackround(abilitySprite.texture, BackroundSprites[playerTeam]);
            var SpriteWithBackround = Sprite.Create(TextureWithBackround, new Rect(0f, 0f, TextureWithBackround.width, TextureWithBackround.height), new Vector2(0.5f, 0.5f));
            SpriteWithBackround.name = abilitySprite.name;
            return SpriteWithBackround;
        }

        [HarmonyPatch(typeof(AbilityReadyIndicator), nameof(AbilityReadyIndicator.SetSprite))]
        public static class SpriteCirclePatch
        {
            public static void Postfix(AbilityReadyIndicator __instance, ref Sprite sprite, ref SpriteRenderer ___spriteRen)
            {
                if (CustomAbilityTexstures.Contains(sprite.texture))
                {
                    //basicly set the backround up
                    //AbilityTextureMetaData metadata = Api.CustomAbilityTexstures[sprite.texture];
                    //Vector2 CircleCenter = new Vector2(metadata.BackroundTopLeftCourner.x + (metadata.BackroundSize.x/2), metadata.BackroundTopLeftCourner.y + (metadata.BackroundSize.y / 2));
                    //Vector2 CircleCenterZeroToOne = CircleCenter / metadata.TotalSize;
                    //__instance.spriteRen.material.SetVector("_CircleExtents", new Vector4(1/ metadata.BackroundSize.x, 1 / metadata.BackroundSize.y, CircleCenterZeroToOne.x, CircleCenterZeroToOne.y));
                    __instance.GetSpriteRen().material.SetVector("_CircleExtents", new Vector4(0, 0, 0, 0));
                }

                else
                {
                    __instance.GetSpriteRen().material.SetVector("_CircleExtents", defaultExtents);
                }
            }
        }
        [HarmonyPatch(typeof(AbilityGrid), "Awake")]
        public static class AbilityGridPatch
        {
            public static void Prefix(AbilityGrid __instance)
            {
                abilityGrid = __instance;
                AddNamedSprites(__instance.abilityIcons);
            }
        }
        [HarmonyPatch(typeof(AchievementHandler), "Awake")]
        public static class AchievementHandlerPatch
        {
            public static void Postfix(AchievementHandler __instance)
            {
                AddNamedSprites(__instance.abilityIcons);
            }
        }
        // These AchievementHandler patches are due to achievements searching for ability icons with a sprite
        // which for modded abilities will always return an index of -1 which will cause an error.
        [HarmonyPatch(typeof(AchievementHandler), "onStartedAGame")]
        public static class AchievementHandlerOnStartedAGamePatch
        {
            public static bool Prefix(AchievementHandler __instance)
            {
                PlayerHandler playerHandler = PlayerHandler.Get();
                List<Player> list = playerHandler.PlayerList();
                if (playerHandler.NumberOfTeams() <= 1)
                {
                    return false;
                }
                if (!IsAchieved(AchievementEnum.PlayAGameWhereEveryoneOnlyPickedBlinkGuns))
                {
                    bool flag = true;
                    for (int i = 0; i < list.Count; i++)
                    {
                        List<Sprite> list2 = list[i].AbilityIcons;
                        if (list2.Count != 3)
                        {
                            flag = false;
                        }
                        for (int j = 0; j < list2.Count; j++)
                        {
                            int abilityIndex = __instance.abilityIcons.IndexOf(list2[j]);
                            if (list2[j] == null || abilityIndex < 0 || !(__instance.abilityIcons.sprites[abilityIndex].name == "Blink gun"))
                            {
                                flag = false;
                                break;
                            }
                        }
                    }
                    if (flag)
                    {
                        TryAwardAchievement(AchievementEnum.PlayAGameWhereEveryoneOnlyPickedBlinkGuns);
                    }
                }
                if (IsAchieved(AchievementEnum.PlayAGameWhereEveryPlayerSelectedOnlyRANDOM))
                {
                    return false;
                }
                bool flag2 = true;
                for (int k = 0; k < list.Count; k++)
                {
                    List<Sprite> list3 = list[k].AbilityIcons;
                    _ = list[k].Abilities;
                    if (list3.Count != 3)
                    {
                        flag2 = false;
                    }
                    for (int l = 0; l < list3.Count; l++)
                    {
                        int abilityIndex = __instance.abilityIcons.IndexOf(list3[l]);
                        if (list3[l] == null || abilityIndex < 0 || !(__instance.abilityIcons.sprites[abilityIndex].name == "Random"))
                        {
                            flag2 = false;
                            break;
                        }
                    }
                }
                if (flag2)
                {
                    TryAwardAchievement(AchievementEnum.PlayAGameWhereEveryPlayerSelectedOnlyRANDOM);
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(AchievementHandler), "OnWinRound")]
        public static class AchievementHandlerOnWinRoundPatch
        {
            public delegate int NumberOfPlayersInTeamDelegate(int team);
            public static int numberOfPlayersInTeam(int team)
            {
                List<Player> list = PlayerHandler.Get().PlayerList();
                int num = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Team == team)
                    {
                        num++;
                    }
                }
                return num;
            }
            public static bool Prefix(AchievementHandler __instance, bool[] ___playerIsAfk)
            {
                PlayerHandler playerHandler = PlayerHandler.Get();
                List<Player> list = playerHandler.PlayerList();
                int num = playerHandler.NumberOfTeams();
                Player player = null;
                if (num == 1)
                {
                    return false;
                }
                bool flag = false;
                for (int i = 0; i < list.Count; i++)
                {
                    Player player2 = list[i];
                    if (player2.WonThisRound && player2.IsLocalPlayer)
                    {
                        flag = true;
                        player = player2;
                    }
                }
                bool flag2 = false;
                int num2 = -1;
                for (int j = 0; j < list.Count; j++)
                {
                    Player player3 = list[j];
                    if (player3.WonThisRound)
                    {
                        if (num2 != player3.Team && num2 != -1)
                        {
                            flag2 = true;
                        }
                        num2 = player3.Team;
                    }
                }
                if (!flag)
                {
                    return false;
                }
                if (!IsAchieved(AchievementEnum.Reach100Wins))
                {
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (list[k].WonThisRound && list[k].IsLocalPlayer && list[k].GamesWon >= 100)
                        {
                            TryAwardAchievement(AchievementEnum.Reach100Wins);
                        }
                    }
                }
                if (flag2)
                {
                    return false;
                }
                if (!IsAchieved(AchievementEnum.WinA1v3) && num == 2 && playerHandler.NumberOfPlayers() == 4 && numberOfPlayersInTeam(player.Team) == 1)
                {
                    TryAwardAchievement(AchievementEnum.WinA1v3);
                }
                if (!IsAchieved(AchievementEnum.WinAGameWithoutDoingAnything))
                {
                    for (int l = 0; l < ___playerIsAfk.Length; l++)
                    {
                        Player player4 = list[l];
                        if (___playerIsAfk[l] && player4.WonThisRound && numberOfPlayersInTeam(player4.Team) == 1)
                        {
                            TryAwardAchievement(AchievementEnum.WinAGameWithoutDoingAnything);
                            break;
                        }
                    }
                }
                if (!IsAchieved(AchievementEnum.WinAGameWith3Dashes))
                {
                    List<Player> list2 = playerHandler.PlayerList();
                    bool flag3 = true;
                    for (int m = 0; m < list2.Count; m++)
                    {
                        if (!list2[m].WonThisRound)
                        {
                            continue;
                        }
                        List<Sprite> list3 = list2[m].AbilityIcons;
                        if (list3.Count != 3)
                        {
                            flag3 = false;
                        }
                        for (int n = 0; n < list3.Count; n++)
                        {
                            int abilityIndex = __instance.abilityIcons.IndexOf(list3[n]);
                            if (list3[n] == null || abilityIndex < 0 || !(__instance.abilityIcons.sprites[abilityIndex].name == "Dash"))
                            {
                                flag3 = false;
                                break;
                            }
                        }
                        if (flag3)
                        {
                            TryAwardAchievement(AchievementEnum.WinAGameWith3Dashes);
                            break;
                        }
                    }
                }
                if (!IsAchieved(AchievementEnum.WinAGameAgainstAPlayerWith3Dashes))
                {
                    List<Player> list4 = playerHandler.PlayerList();
                    for (int num3 = 0; num3 < list4.Count; num3++)
                    {
                        if (list4[num3].IsAlive)
                        {
                            continue;
                        }
                        List<Sprite> list5 = list4[num3].AbilityIcons;
                        bool flag4 = list5.Count == 3;
                        for (int num4 = 0; num4 < list5.Count; num4++)
                        {
                            int abilityIndex = __instance.abilityIcons.IndexOf(list5[num4]);
                            if (list5[num4] == null || abilityIndex < 0 || !(__instance.abilityIcons.sprites[abilityIndex].name == "Dash"))
                            {
                                flag4 = false;
                            }
                        }
                        if (flag4)
                        {
                            TryAwardAchievement(AchievementEnum.WinAGameAgainstAPlayerWith3Dashes);
                        }
                    }
                }
                if (IsAchieved(AchievementEnum.WinAGameWithNoOffensiveAbilities))
                {
                    return false;
                }
                List<Player> list6 = playerHandler.PlayerList();
                bool flag5 = true;
                int num5 = 0;
                for (int num6 = 0; num6 < list6.Count; num6++)
                {
                    if (list6[num6].IsAlive)
                    {
                        num5++;
                    }
                }
                for (int num7 = 0; num7 < list6.Count; num7++)
                {
                    if (!list6[num7].WonThisRound || num5 != 1)
                    {
                        continue;
                    }
                    List<Sprite> list7 = list6[num7].AbilityIcons;
                    for (int num8 = 0; num8 < list7.Count; num8++)
                    {
                        int abilityIndex = __instance.abilityIcons.IndexOf(list7[num8]);
                        if (list7[num8] == null || abilityIndex < 0 || __instance.abilityIcons.sprites[abilityIndex].isOffensiveAbility)
                        {
                            flag5 = false;
                            break;
                        }
                    }
                    if (flag5)
                    {
                        TryAwardAchievement(AchievementEnum.WinAGameWithNoOffensiveAbilities);
                        break;
                    }
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(CharacterStatsList), "TryStartNextLevel_online")]
        public static class CharacterStatsListPatch
        {
            public static void Prefix(CharacterStatsList __instance)
            {
                AddNamedSprites(__instance.abilityIcons);

            }
        }
        [HarmonyPatch(typeof(DynamicAbilityPickup), "Awake")]
        public static class DynamicAbilityPickupPatch
        {
            public static void Postfix(DynamicAbilityPickup __instance)
            {
                AddNamedSprites(__instance.abilityIcons);
            }
        }
        [HarmonyPatch(typeof(MidGameAbilitySelect), "Awake")]
        public static class MidGameAbilitySelectPatch
        {
            public static void Postfix(MidGameAbilitySelect __instance, ref NamedSpriteList ___localAbilityIcons)
            {
                AddNamedSprites(__instance.AbilityIcons);
            }
        }
        // Delete this patch when api is finished
        [HarmonyPatch(typeof(MidGameAbilitySelect), "SetPlayer")]
        public static class MidGameAbilitySetPlayerPatch
        {
            public static bool Prefix(MidGameAbilitySelect __instance, int playerId)
            {
                return true;
                int YPos = 0;
                Player player = PlayerHandler.Get().GetPlayer(playerId);
                Debug.Log($"Received player from id: {playerId}");
                Image[] CircleImages = new Image[Settings.Get().NumberOfAbilities];
                RectTransform rectTrans = __instance.GetComponent<RectTransform>();
                rectTrans.anchoredPosition = Vector2.zero;
                int[] XPos = new int[Settings.Get().NumberOfAbilities];
                Debug.Log($"Setting up circle images...");
                for (int i = 0; i < __instance.Circles.Length; i++)
                {
                    if (i >= Settings.Get().NumberOfAbilities)
                    {
                        __instance.Circles[i].gameObject.SetActive(value: false);
                        rectTrans.anchoredPosition += Vector2.down * 71.7001f;
                    }
                    else
                    {
                        CircleImages[i] = __instance.Circles[i].transform.GetChild(0).GetComponent<Image>();
                    }
                }
                Debug.Log($"Setting up ability icons inside circles...");
                NamedSpriteList localAbilityIcons = (SteamManager.instance.dlc.HasDLC() ? __instance.AbilityIcons : __instance.AbilityIcons_demo);
                for (int j = 0; j < CircleImages.Length; j++)
                {
                    int index = j;
                    if (XPos.Length == 3)
                    {
                        switch (j)
                        {
                            case 2:
                                index = 1;
                                break;
                            case 1:
                                index = 2;
                                break;
                        }
                    }
                    Debug.Log($"Getting ability name: {player.Abilities[index]}");
                    // Error is here
                    XPos[j] = localAbilityIcons.IndexOf(player.Abilities[index].name);
                    Debug.Log($"Received ability name for ability: {player.Abilities[index]}");
                    if (XPos[j] < 0)
                    {
                        XPos[j] = 0;
                    }
                    CircleImages[j].sprite = localAbilityIcons.GetSprite(XPos[j]);
                }
                __instance.ArrowR.anchoredPosition = __instance.Circles[YPos].anchoredPosition;
                __instance.ArrowL.anchoredPosition = __instance.Circles[YPos].anchoredPosition;
                return true;
            }
        }
        [HarmonyPatch(typeof(MidGameAbilitySelect), "UpdatePlayerAbilityChoices")]
        public static class MidGameAbilitySelectUpdatePlayerAbilityChoicesPatch
        {
            public static bool Prefix(MidGameAbilitySelect __instance, Player ___player, NamedSpriteList ___localAbilityIcons, int ___YPos, int[] ___XPos)
            {
                NamedSprite abilityIcon = ___localAbilityIcons.sprites[___XPos[___YPos]];
                Sprite iconSprite = abilityIcon.sprite;
                if (NamedSpritesDict.ContainsKey(abilityIcon.name))
                {
                    iconSprite = CreateSpriteWithBackground(abilityIcon.sprite, ___player.Team);
                }
                if (Settings.Get().NumberOfAbilities == 3)
                {
                    int index = ___YPos;
                    if (___YPos == 2)
                    {
                        index = 1;
                    }
                    if (___YPos == 1)
                    {
                        index = 2;
                    }
                    __instance.CircleImages[___YPos].sprite = ___localAbilityIcons.GetSprite(___XPos[___YPos]);
                    ___player.Abilities[index] = ___localAbilityIcons.sprites[___XPos[___YPos]].associatedGameObject;
                    ___player.AbilityIcons[index] = iconSprite;
                }
                else
                {
                    __instance.CircleImages[___YPos].sprite = ___localAbilityIcons.GetSprite(___XPos[___YPos]);
                    ___player.Abilities[___YPos] = ___localAbilityIcons.sprites[___XPos[___YPos]].associatedGameObject;
                    ___player.AbilityIcons[___YPos] = iconSprite;
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(RandomAbility), "Awake")]
        public static class RandomAbilityPatch
        {
            public static void Postfix(RandomAbility __instance)
            {
                AddNamedSprites(__instance.abilityIcons);
            }
        }
        [HarmonyPatch(typeof(SelectAbility), "Awake")]
        public static class SelectAbilityPatch
        {
            public static void Postfix(SelectAbility __instance)
            {
                AddNamedSprites(__instance.abilityIcons);
            }
        }
        [HarmonyPatch(typeof(SlimeController), "Awake")]
        public static class SlimeControllerAwakePatch
        {
            public static void Postfix(SlimeController __instance)
            {
                AddNamedSprites(__instance.abilityIconsFull);
            }
        }
        [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.AddAdditionalAbility))]
        public class AddAdditionalAbilityPatch
        {
            public static void Prefix(SlimeController __instance, Fix[] ___abilityCooldownTimers, AbilityMonoBehaviour ability, PlayerCollision ___playerCollision, ref Sprite indicatorSprite, GameObject abilityPrefab)
            {
                if (NamedSpritesDict.ContainsKey(indicatorSprite.name))
                {
                    Debug.Log("Replacing indicator sprite with background");
                    Player p = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
                    indicatorSprite = CreateSpriteWithBackground(indicatorSprite, p.Team);
                }
            }
        }
        [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.DropAbilities))]
        public static class SlimeControllerDropAbilitiesPatch
        {
            public static bool Prefix(SlimeController __instance, ref DynamicAbilityPickup ___abilityPickupPrefab)
            {
                if (!GameSession.IsInitialized() || GameSessionHandler.HasGameEnded() || __instance.abilities.Count <= 0)
                {
                    return false;
                }
                if (!playersDied.ContainsKey(__instance.playerNumber.ToString()))
                {
                    playersDied.Add(__instance.playerNumber.ToString(), true);
                } else
                {
                    playersDied.Remove(__instance.playerNumber.ToString());
                    return false;
                }
                NamedSpriteList abilityIcons = SteamManager.instance.abilityIcons;
                PlayerHandler.Get().GetPlayer(__instance.playerNumber);
                for (int i = 0; i < __instance.AbilityReadyIndicators.Length; i++)
                {
                    if (__instance.AbilityReadyIndicators[i] != null)
                    {
                        __instance.AbilityReadyIndicators[i].InstantlySyncTransform();
                    }
                }
                int num = Settings.Get().NumberOfAbilities - 1;
                while (num >= 0 && (num >= __instance.AbilityReadyIndicators.Length || __instance.AbilityReadyIndicators[num] == null))
                {
                    num--;
                }
                if (num < 0)
                {
                    return false;
                }

                Vec2 launchDirection = Vec2.NormalizedSafe(Vec2.up + new Vec2(Updater.RandomFix((Fix)(-0.3f), (Fix)0.3f), (Fix)0L));
                DynamicAbilityPickup dynamicAbilityPickup = FixTransform.InstantiateFixed<DynamicAbilityPickup>(___abilityPickupPrefab, __instance.body.position);
                Sprite primarySprite = __instance.AbilityReadyIndicators[num].GetPrimarySprite();
                NamedSprite namedSprite = new();
                if (__instance.abilityIconsFull.IndexOf(primarySprite) != -1)
                {
                    namedSprite = __instance.abilityIconsFull.sprites[__instance.abilityIconsFull.IndexOf(primarySprite)];
                    Debug.Log("droping normal ability");
                }
                else 
                {
                    namedSprite = NamedSpritesDict[primarySprite.name];
                    Debug.Log($"Dropping custom ability {namedSprite.name}");
                    primarySprite = namedSprite.sprite;
                    primarySprite.name = namedSprite.name;
                }
                if (namedSprite.associatedGameObject == null)
                {
                    namedSprite = __instance.abilityIconsDemo.sprites[__instance.abilityIconsDemo.IndexOf(primarySprite)];
                }
                dynamicAbilityPickup.InitPickup(namedSprite.associatedGameObject, primarySprite, launchDirection);

                return false;
            }
        }
        [HarmonyPatch(typeof(PlayerCollision), nameof(PlayerCollision.SpawnClone))]
        public static class PlayerCollisionPatch
        {
            public static bool Prefix()
            {
                return false;
            }
            public static void Postfix(PlayerCollision __instance, Player player, SlimeController slimeContToRevive, Vec2 targetPosition, ref SlimeController __result)
            {

                if (player.playersAndClonesStillAlive < Constants.MaxClones + 1)
                {
                    player.playersAndClonesStillAlive++;
                    SlimeController slimeController = FixTransform.InstantiateFixed<SlimeController>(__instance.reviveEffectPrefab.emptyPlayerPrefab, targetPosition);
                    slimeController.playerNumber = player.Id;
                    slimeController.GetPlayerSprite().sprite = null;
                    slimeController.GetPlayerSprite().material = player.Color;
                    List<AbilityMonoBehaviour> list = new();
                    for (int i = 0; i < slimeContToRevive.abilities.Count; i++)
                    {
                        int index = slimeContToRevive.abilityIcons.IndexOf(slimeContToRevive.AbilityReadyIndicators[i].GetPrimarySprite());
                        GameObject gameObject;
                        if (index != -1)
                        {
                            gameObject = FixTransform.InstantiateFixed(slimeContToRevive.abilityIcons.sprites[index].associatedGameObject, Vec2.zero);
                        }
                        else
                        {
                            string spriteName = slimeContToRevive.AbilityReadyIndicators[i].GetPrimarySprite().name;
                            gameObject = FixTransform.InstantiateFixed(NamedSpritesDict[spriteName].associatedGameObject, Vec2.zero);
                        }
                        gameObject.gameObject.SetActive(false);
                        list.Add(gameObject.GetComponent<AbilityMonoBehaviour>());
                    }
                    slimeController.abilities = list;
                    AbilityReadyIndicator[] array = new AbilityReadyIndicator[3];
                    for (int j = 0; j < slimeContToRevive.AbilityReadyIndicators.Length; j++)
                    {
                        if (!(slimeContToRevive.AbilityReadyIndicators[j] == null))
                        {
                            array[j] = Instantiate(__instance.reviveEffectPrefab.AbilityReadyIndicators[j]).GetComponent<AbilityReadyIndicator>();
                            array[j].SetSprite(slimeContToRevive.AbilityReadyIndicators[j].GetPrimarySprite(), true);
                            array[j].Init();
                            array[j].SetColor(__instance.reviveEffectPrefab.teamColors.teamColors[player.Team].fill);
                            array[j].GetComponent<FollowTransform>().Leader = slimeController.transform;
                            array[j].gameObject.SetActive(false);
                        }
                    }
                    slimeController.AbilityReadyIndicators = array;
                    slimeController.PrepareToRevive(targetPosition);
                    __result = slimeController;
                    return;
                }
                __result = null;
            }
        }
        [HarmonyPatch(typeof(GameSessionHandler), "SpawnPlayers")]
        public static class SpawnPlayerPatch
        {
            public static void Prefix()
            {
                playersDied.Clear();
            }
        }
        [HarmonyPatch(typeof(SteamManager), "Awake")]
        public static class SteamManagerPatch
        {
            public static void Postfix(SteamManager __instance, ref NamedSpriteList ___abilityIconsFull)
            {
                AddNamedSprites(___abilityIconsFull);
            }
        }
        [HarmonyPatch(typeof(CharacterSelectHandler_online), "InitPlayer")]
        public static class CharacterSelectHandler_onlinePatch
        {
            public static Player playerToReturn;
            public static bool Prefix(int id, byte color, byte team, byte ability1, byte ability2, byte ability3, int nrOfAbilities, PlayerColors playerColors)
            {
                NamedSpriteList abilityIcons = SteamManager.instance.abilityIcons;
                Player player = new Player
                {
                    Id = id,
                    Color = playerColors.playerColors[(int)color].playerMaterial,
                    Team = (int)team
                };

                Debug.Log($"Team is {team}");
                if (nrOfAbilities > 0)
                {
                    if (NamedSpritesDict.ContainsKey(abilityIcons.sprites[ability1].name))
                    {
                        player.Abilities.Add(abilityIcons.sprites[(int)ability1].associatedGameObject);
                        player.AbilityIcons.Add(CreateSpriteWithBackground(abilityIcons.sprites[ability1].sprite, team));
                    }
                    else
                    {
                        player.Abilities.Add(abilityIcons.sprites[(int)ability1].associatedGameObject);
                        player.AbilityIcons.Add(abilityIcons.sprites[ability1].sprite);
                    }

                }
                if (nrOfAbilities > 1)
                {
                    if (NamedSpritesDict.ContainsKey(abilityIcons.sprites[ability2].name))
                    {
                        player.Abilities.Add(abilityIcons.sprites[(int)ability2].associatedGameObject);
                        player.AbilityIcons.Add(CreateSpriteWithBackground(abilityIcons.sprites[ability2].sprite, team));
                    }
                    else
                    {
                        player.Abilities.Add(abilityIcons.sprites[(int)ability2].associatedGameObject);
                        player.AbilityIcons.Add(abilityIcons.sprites[(int)ability2].sprite);
                    }
                }
                if (nrOfAbilities > 2)
                {
                    if (NamedSpritesDict.ContainsKey(abilityIcons.sprites[ability3].name))
                    {
                        player.Abilities.Add(abilityIcons.sprites[(int)ability3].associatedGameObject);
                        player.AbilityIcons.Add(CreateSpriteWithBackground(abilityIcons.sprites[ability3].sprite, team));
                    }
                    else
                    {
                        player.Abilities.Add(abilityIcons.sprites[(int)ability3].associatedGameObject);
                        player.AbilityIcons.Add(abilityIcons.sprites[(int)ability3].sprite);
                    }
                }
                player.IsLocalPlayer = false;
                playerToReturn = player;
                return false;
            }
            public static void Postfix(ref Player __result)
            {
                __result = playerToReturn;
            }
        }
        [HarmonyPatch(typeof(Arrow), "OnCollide")]
        public static class Arrow_OnCollide_Patch
        {
            public static bool Prefix(Arrow __instance)
            {
                if (__instance.gameObject.name == "bullet-api")
                {
                    Updater.DestroyFix(__instance.gameObject);
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(CharacterSelectHandler), "TryStartGame_inner")]
        public static class CharacterSelectHandlerPatch
        {
            public static bool Prefix(CharacterSelectHandler __instance, ref bool ___allReadyForMoreThanOneFrame, ref PlayerColors ___playerColors)
            {
                if (CharacterSelectHandler.startButtonAvailable && ___allReadyForMoreThanOneFrame)
                {
                    AudioManager audioManager = AudioManager.Get();
                    if (audioManager != null)
                    {
                        audioManager.Play("startGame");
                    }

                    // Clear the player list
                    CharacterSelectHandler.startButtonAvailable = false;

                    // Create the player list
                    List<Player> list = PlayerHandler.Get().PlayerList();
                    list.Clear();

                    int num = 1;
                    NamedSpriteList abilityIcons = SteamManager.instance.abilityIcons;
                    for (int i = 0; i < __instance.characterSelectBoxes.Length; i++)
                    {
                        if (__instance.characterSelectBoxes[i].menuState == CharSelectMenu.ready)
                        {
                            PlayerInit playerInit = __instance.characterSelectBoxes[i].playerInit;
                            Player player = new(num, playerInit.team)
                            {
                                Color = ___playerColors[playerInit.color].playerMaterial,
                                UsesKeyboardAndMouse = playerInit.usesKeyboardMouse,
                                CanUseAbilities = true,
                                inputDevice = playerInit.inputDevice,
                                Abilities = new List<GameObject>(3),
                                AbilityIcons = new List<Sprite>(3)
                            };

                            player.Abilities.Add(abilityIcons.sprites[playerInit.ability0].associatedGameObject);

                            // If its a custom ability then use the one that has the backround
                            if (Sprites.Contains(abilityIcons.sprites[playerInit.ability0]))
                            {
                                var iconSprite = abilityIcons.sprites[playerInit.ability0].sprite;
                                player.AbilityIcons.Add(CreateSpriteWithBackground(iconSprite, player.Team));
                            }
                            else
                            {
                                // If its not a custom ability do it normaly
                                player.AbilityIcons.Add(abilityIcons.sprites[playerInit.ability0].sprite);
                            }

                            Settings settings = Settings.Get();
                            if (settings != null && settings.NumberOfAbilities > 1)
                            {
                                player.Abilities.Add(abilityIcons.sprites[playerInit.ability1].associatedGameObject);
                                // If its a custom ability then use the one that has the backround
                                if (Sprites.Contains(abilityIcons.sprites[playerInit.ability1]))
                                {
                                    var iconSprite = abilityIcons.sprites[playerInit.ability1].sprite;
                                    player.AbilityIcons.Add(CreateSpriteWithBackground(iconSprite, player.Team));
                                }
                                else
                                {
                                    // If its not a custom ability do it normaly
                                    player.AbilityIcons.Add(abilityIcons.sprites[playerInit.ability1].sprite);
                                }
                            }
                            Settings settings2 = Settings.Get();
                            if (settings2 != null && settings2.NumberOfAbilities > 2)
                            {
                                player.Abilities.Add(abilityIcons.sprites[playerInit.ability2].associatedGameObject);
                                // If its a custom ability then use the one that has the backround
                                if (Sprites.Contains(abilityIcons.sprites[playerInit.ability2]))
                                {
                                    var iconSprite = abilityIcons.sprites[playerInit.ability2].sprite;
                                    player.AbilityIcons.Add(CreateSpriteWithBackground(iconSprite, player.Team));
                                }
                                else
                                {
                                    // If its not a custom ability do it normaly
                                    player.AbilityIcons.Add(abilityIcons.sprites[playerInit.ability2].sprite);
                                }
                            }
                            player.CustomKeyBinding = playerInit.keybindOverride;
                            num++;
                            list.Add(player);
                        }
                    }
                    GameSession.Init();
                    SceneManager.LoadScene("Level1");
                    if (!WinnerTriangleCanvas.HasBeenSpawned)
                    {
                        SceneManager.LoadScene("winnerTriangle", LoadSceneMode.Additive);
                    }
                }
                return false;
            }
        }
    }
}
