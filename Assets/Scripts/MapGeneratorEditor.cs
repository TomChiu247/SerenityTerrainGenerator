using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(TileProceduralGenerator))]
public class TilePRNGMapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TileProceduralGenerator map = target as TileProceduralGenerator; //or = (TileProceduralGenerator)target;
        if (DrawDefaultInspector())
        {
            if (map.autoUpdate)
            {
                map.GenerateMap();
            }
        }

        if (GUILayout.Button("Generate Map"))
        {
            map.GenerateMap();
        }
    }
}