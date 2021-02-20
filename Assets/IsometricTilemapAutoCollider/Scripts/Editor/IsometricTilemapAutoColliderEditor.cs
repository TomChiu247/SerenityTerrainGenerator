using UnityEditor;
using UnityEngine;

namespace GoblinsInteractive.IsometricTilemapAutoCollider
{
    [CustomEditor(typeof(IsometricTilemapAutoCollider))]
    public class IsometricTilemapAutoColliderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Build Colliders"))
            {
                ((IsometricTilemapAutoCollider)target).BuildColliders();
            }
        }
    }
}
