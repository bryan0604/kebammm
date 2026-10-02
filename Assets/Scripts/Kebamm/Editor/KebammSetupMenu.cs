#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kebamm.Editor
{
    public static class KebammSetupMenu
    {
        [MenuItem("Kebamm/Add Prototype Bootstrap To Open Scene")]
        public static void AddBootstrapToOpenScene()
        {
            var existing = Object.FindFirstObjectByType<KebammPrototypeBootstrap>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("Kebamm", "Bootstrap already present in the open scene.", "OK");
                return;
            }

            var go = new GameObject("KebammPrototype");
            go.AddComponent<KebammPrototypeBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Add Kebamm Prototype Bootstrap");
            Selection.activeGameObject = go;

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[Kebamm] Added KebammPrototypeBootstrap to open scene. Press Play to run.");
        }
    }
}
#endif
