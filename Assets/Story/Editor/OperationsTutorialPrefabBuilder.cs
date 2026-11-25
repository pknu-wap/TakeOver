using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Story.Editor
{
    public static class OperationsTutorialPrefabBuilder
    {
        public const string Path = "Assets/Resources/Story/OperationsTutorial.prefab";

        [MenuItem("TakeOver/Story/Rebuild Operations Tutorial Copy")]
        public static void Rebuild()
        {
            var holder = new GameObject("Inactive tutorial authoring");
            holder.SetActive(false);
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Operations/Prefabs/OperationsScreen.prefab");
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(source, holder.transform);
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var rect = (RectTransform)copy.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(1280, 720);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                // Bake the authored canvas layers into sibling order in one design coordinate system.
                // Keep their world transforms while removing the independent desktop renderers.
                var layers = copy.GetComponentsInChildren<Canvas>(true)
                    .Where(c => c.gameObject != copy).OrderBy(c => c.sortingOrder).ToArray();
                foreach (var layer in layers)
                {
                    layer.transform.SetParent(rect, true);
                    layer.transform.SetAsLastSibling();
                }
                foreach (var raycaster in copy.GetComponentsInChildren<GraphicRaycaster>(true)) Object.DestroyImmediate(raycaster);
                foreach (var scaler in copy.GetComponentsInChildren<CanvasScaler>(true)) Object.DestroyImmediate(scaler);
                foreach (var canvas in copy.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas);
                copy.name = "OperationsTutorial";
                copy.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(copy, Path);
            }
            finally { Object.DestroyImmediate(holder); }
            AssetDatabase.SaveAssets();
        }
    }
}
