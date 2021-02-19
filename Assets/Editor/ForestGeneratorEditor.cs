using System.Collections;
using UnityEngine;
using UnityEditor;

[CustomEditor (typeof (ForestGenerator))]
public class ForestGeneratorEditor : Editor
{
    public override void OnInspectorGUI ()
    {
        ForestGenerator generator = (ForestGenerator)target;

        if (DrawDefaultInspector ())
        {
            if (generator.auto_update)
            {
                generator.DrawMapInEditor();
            }
        }

        if (GUILayout.Button("Generate"))
        {
            generator.DrawMapInEditor();
        }
    }
}

