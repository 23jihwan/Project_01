using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 프로젝트를 처음 열면 자동으로 실행되는 설정 스크립트.
///  1) Player Settings : 480 x 854, 세로(Portrait) 고정, 패키지명
///  2) Assets/Scenes/Main.unity 씬 생성 + Build Settings 등록 + 씬 열기
///  3) Game 뷰 해상도에 "Lion Run Portrait (480x854)" 추가 & 선택
/// 메뉴 [Lion Run] 에서 언제든 다시 실행할 수 있습니다.
/// </summary>
[InitializeOnLoad]
public static class LionRunSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    static string OpenedKey => "LionRun_SceneOpened_" + Application.dataPath;

    static LionRunSetup()
    {
        EditorApplication.delayCall += AutoSetup;
    }

    static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoSetup;
            return;
        }

        ApplyPlayerSettings();
        bool created = EnsureScene();
        EnsureBuildSettings();

        if (created || !EditorPrefs.GetBool(OpenedKey, false))
        {
            OpenMainScene();
            EditorPrefs.SetBool(OpenedKey, true);
        }

        if (!SessionState.GetBool("LionRun_GameView", false))
        {
            SessionState.SetBool("LionRun_GameView", true);
            GameViewSizeHelper.SelectPortrait480x854();
        }
    }

    [MenuItem("Lion Run/1. 프로젝트 설정 다시 적용")]
    static void MenuSetup()
    {
        ApplyPlayerSettings();
        EnsureScene();
        EnsureBuildSettings();
        OpenMainScene();
        GameViewSizeHelper.SelectPortrait480x854();
        Debug.Log("[LionRun] 설정 완료 : 480x854 / Portrait / Main 씬");
    }

    [MenuItem("Lion Run/2. Game 뷰 480x854로 맞추기")]
    static void MenuGameView() => GameViewSizeHelper.SelectPortrait480x854();

    [MenuItem("Lion Run/3. 최고 기록 초기화")]
    static void MenuResetBest()
    {
        PlayerPrefs.DeleteKey("LionRun_Best");
        PlayerPrefs.Save();
        Debug.Log("[LionRun] 최고 기록 초기화됨");
    }

    static void ApplyPlayerSettings()
    {
        if (PlayerSettings.defaultScreenWidth != 480) PlayerSettings.defaultScreenWidth = 480;
        if (PlayerSettings.defaultScreenHeight != 854) PlayerSettings.defaultScreenHeight = 854;
        if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed) PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        if (PlayerSettings.resizableWindow) PlayerSettings.resizableWindow = false;
        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        if (PlayerSettings.productName != "LionRun") PlayerSettings.productName = "LionRun";

        try
        {
            SetIdIfDefault(NamedBuildTarget.Android);
            SetIdIfDefault(NamedBuildTarget.iOS);
        }
        catch (Exception e)
        {
            Debug.Log("[LionRun] 패키지명 자동 설정 건너뜀 : " + e.Message);
        }
    }

    static void SetIdIfDefault(NamedBuildTarget target)
    {
        string id = PlayerSettings.GetApplicationIdentifier(target);
        if (string.IsNullOrEmpty(id) || id.StartsWith("com.DefaultCompany", StringComparison.OrdinalIgnoreCase))
            PlayerSettings.SetApplicationIdentifier(target, "com.lionrun.game");
    }

    static bool EnsureScene()
    {
        if (File.Exists(ScenePath)) return false;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4.27f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0xA8, 0xD0, 0x8D, 0xFF);
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        new GameObject("LionGame").AddComponent<LionGame>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[LionRun] Main 씬 생성 : " + ScenePath);
        return true;
    }

    static void EnsureBuildSettings()
    {
        if (!File.Exists(ScenePath)) return;
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
            if (s.path == ScenePath) return;

        var list = new EditorBuildSettingsScene[scenes.Length + 1];
        list[0] = new EditorBuildSettingsScene(ScenePath, true);
        Array.Copy(scenes, 0, list, 1, scenes.Length);
        EditorBuildSettings.scenes = list;
    }

    static void OpenMainScene()
    {
        if (!File.Exists(ScenePath)) return;
        if (EditorSceneManager.GetActiveScene().path == ScenePath) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}

/// <summary>
/// Game 뷰에 480x854 고정 해상도를 추가하고 선택 (Unity 내부 API를 리플렉션으로 사용, 실패해도 무해)
/// </summary>
static class GameViewSizeHelper
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static void SelectPortrait480x854()
    {
        try
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance", All).GetValue(null, null);

            var groupTypeProp = sizesType.GetProperty("currentGroupType", All);
            object groupType = groupTypeProp.GetValue(groupTypeProp.GetGetMethod(true).IsStatic ? null : instance, null);
            var group = sizesType.GetMethod("GetGroup", All).Invoke(instance, new[] { groupType });
            var groupT = group.GetType();

            int idx = FindIndex(group, groupT);
            if (idx < 0)
            {
                var sizeT = asm.GetType("UnityEditor.GameViewSize");
                var sizeKindT = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeT.GetConstructor(new[] { sizeKindT, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.Parse(sizeKindT, "FixedResolution"), 480, 854, "Lion Run Portrait" });
                groupT.GetMethod("AddCustomSize", All).Invoke(group, new[] { size });
                sizesType.GetMethod("SaveToHDD", All)?.Invoke(instance, null);
                idx = FindIndex(group, groupT);
            }
            if (idx < 0) return;

            var gameViewT = asm.GetType("UnityEditor.GameView");
            foreach (var w in Resources.FindObjectsOfTypeAll(gameViewT))
            {
                var prop = gameViewT.GetProperty("selectedSizeIndex", All);
                if (prop != null && prop.CanWrite) prop.SetValue(w, idx, null);
                else gameViewT.GetMethod("SizeSelectionCallback", All)?.Invoke(w, new object[] { idx, null });
                ((EditorWindow)w).Repaint();
            }
        }
        catch (Exception e)
        {
            Debug.Log("[LionRun] Game 뷰 해상도 자동 설정을 건너뜀 (수동으로 480x854 추가 가능) : " + e.Message);
        }
    }

    static int FindIndex(object group, Type groupT)
    {
        var texts = (string[])groupT.GetMethod("GetDisplayTexts", All).Invoke(group, null);
        for (int i = 0; i < texts.Length; i++)
            if (texts[i].Contains("480x854")) return i;
        return -1;
    }
}
