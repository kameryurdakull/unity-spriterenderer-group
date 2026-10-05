using UnityEditor;
using UnityEngine;

namespace SpriteGroups.Editor
{
    [CustomEditor(typeof(SpriteRendererGroup)), CanEditMultipleObjects]
    public sealed class SpriteRendererGroupEditor : UnityEditor.Editor
    {
        private const string AlphaProperty = "alpha";
        private const string IgnoreParentsProperty = "ignoreParentGroups";
        private SerializedProperty alpha;
        private SerializedProperty ignoreParents;

        private void OnEnable()
        {
            alpha = serializedObject.FindProperty(AlphaProperty);
            ignoreParents = serializedObject.FindProperty(IgnoreParentsProperty);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(alpha);
            EditorGUILayout.PropertyField(ignoreParents);
            if (serializedObject.ApplyModifiedProperties()) RefreshTargets();

            if (targets.Length == 1)
            {
                var group = (SpriteRendererGroup)target;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField("Effective Alpha", group.EffectiveAlpha);
                    EditorGUILayout.IntField("Owned Sprites", group.RendererCount);
                }
            }

            EditorGUILayout.HelpBox("Child sprites keep their original alpha. Nested groups multiply alpha unless Ignore Parent Groups is enabled. Use Refresh after adding sprites to deep children.", MessageType.Info);
            if (GUILayout.Button("Refresh Sprite Hierarchy")) RefreshTargets();
        }

        private void RefreshTargets()
        {
            for (var index = 0; index < targets.Length; index++)
                ((SpriteRendererGroup)targets[index]).Refresh();
            SceneView.RepaintAll();
        }
    }
}
