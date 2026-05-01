using System;
using System.Collections.Generic;
using DragonRescue.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRescue.EditorTools
{
    public static class MissingScriptScanner
    {
        [MenuItem("Dragon Rescue/Debug/Scan Missing Scripts")]
        private static void ScanMissingScripts()
        {
            int checkedObjects = 0;
            int checkedPrefabs = 0;
            int missingScriptCount = 0;
            List<string> findings = new List<string>();

            ScanOpenScenes(findings, ref checkedObjects, ref missingScriptCount);
            ScanPrefabs(findings, ref checkedObjects, ref checkedPrefabs, ref missingScriptCount);

            if (missingScriptCount == 0)
            {
                DebugSystem.AlwaysLog(
                    DebugCategory.Data,
                    $"Missing script scan complete. Checked {checkedObjects} objects across {SceneManager.sceneCount} open scene(s) and {checkedPrefabs} prefab(s). No missing scripts found.");
                return;
            }

            DebugSystem.AlwaysError(
                DebugCategory.Data,
                $"Missing script scan complete. Found {missingScriptCount} missing script component(s):\n{string.Join("\n", findings)}");
        }

        private static void ScanOpenScenes(List<string> findings, ref int checkedObjects, ref int missingScriptCount)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int j = 0; j < roots.Length; j++)
                {
                    ScanHierarchy(
                        roots[j],
                        $"Scene '{scene.name}'",
                        findings,
                        ref checkedObjects,
                        ref missingScriptCount);
                }
            }
        }

        private static void ScanPrefabs(
            List<string> findings,
            ref int checkedObjects,
            ref int checkedPrefabs,
            ref int missingScriptCount)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = null;

                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    checkedPrefabs++;

                    ScanHierarchy(
                        prefabRoot,
                        $"Prefab '{path}'",
                        findings,
                        ref checkedObjects,
                        ref missingScriptCount);
                }
                catch (Exception ex)
                {
                    DebugSystem.AlwaysError(DebugCategory.Data, $"Failed to scan prefab '{path}': {ex.Message}");
                }
                finally
                {
                    if (prefabRoot != null)
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void ScanHierarchy(
            GameObject root,
            string scope,
            List<string> findings,
            ref int checkedObjects,
            ref int missingScriptCount)
        {
            if (root == null) return;

            Stack<Transform> stack = new Stack<Transform>();
            stack.Push(root.transform);

            while (stack.Count > 0)
            {
                Transform current = stack.Pop();
                GameObject currentObject = current.gameObject;
                checkedObjects++;

                int missingOnObject = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(currentObject);
                if (missingOnObject > 0)
                {
                    missingScriptCount += missingOnObject;
                    findings.Add($"{scope} > {GetHierarchyPath(current)} missing={missingOnObject}");
                }

                for (int i = current.childCount - 1; i >= 0; i--)
                {
                    stack.Push(current.GetChild(i));
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }
    }
}
