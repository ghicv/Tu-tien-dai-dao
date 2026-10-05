using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThienDao.EditorTools
{
    public static class ThienDaoSceneSetup
    {
        const string ScenePath = "Assets/ThienDao/Scenes/World.unity";

        [MenuItem("Thiên Đạo/Tạo scene World demo")]
        public static void CreateWorldScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 135f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(30, 74, 160, 255);
            camGo.transform.position = new Vector3(512f, 512f, -10f);
            camGo.AddComponent<Player.CameraController>();

            var world = new GameObject("World");
            world.AddComponent<WorldBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[ThienDao] Created {ScenePath}. Press Play to generate the world.");
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == path) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
