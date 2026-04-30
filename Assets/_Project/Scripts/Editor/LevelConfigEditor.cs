using UnityEditor;
using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.EditorScripts
{
    [CustomEditor(typeof(LevelConfig))]
    public class LevelConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Get the current selected movement type
            SerializedProperty movementTypeProp = serializedObject.FindProperty("dragonMovementType");
            DragonMovementType moveType = (DragonMovementType)movementTypeProp.enumValueIndex;

            // Iterate through all serialized properties automatically
            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true)) // Enter the object
            {
                do
                {
                    // Disable editing of the script reference
                    if (prop.name == "m_Script")
                    {
                        GUI.enabled = false;
                        EditorGUILayout.PropertyField(prop);
                        GUI.enabled = true;
                        continue;
                    }

                    // ── Dynamic Hiding Logic ──
                    if (moveType == DragonMovementType.Linear)
                    {
                        // Hide Waypoint fields if Linear is selected
                        if (prop.name == "dragonPathWaypointsViewport") continue;
                    }
                    else if (moveType == DragonMovementType.Waypoint)
                    {
                        // Hide Linear fields if Waypoint is selected
                        if (prop.name == "dragonStartViewport" || prop.name == "dragonEndViewport") continue;
                    }

                    // Draw the property normally (preserves headers and tooltips!)
                    EditorGUILayout.PropertyField(prop, true);

                } while (prop.NextVisible(false)); // Move to next sibling property
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
