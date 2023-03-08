using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System;

public class DungeonGeneratorEditor : EditorWindow
{
    [MenuItem("Dungeon Generation/ Generation data")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(DungeonGeneratorEditor));
    }

    string myFileName = "default";
    int myDepth = 1;
    int mySeed = 0;
    bool myAllowHeightDifference = true;
    DGenerate_InData myInData;
    DungeonGenerator dungeonGenerator;

    private void OnGUI()
    {
        GUILayout.Label("Dungeon Generation Data", EditorStyles.boldLabel);
       
        myFileName = EditorGUILayout.TextField("Dungeon name", myFileName);
        myDepth = EditorGUILayout.IntField("Dungeon Depth", myDepth);
        mySeed = EditorGUILayout.IntField("Dungeon Seed", mySeed);
        myAllowHeightDifference = EditorGUILayout.Toggle("Allow Height Differences", myAllowHeightDifference);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear Dungeon"))
        {
            GameObject dungeon = GameObject.Find("DungeonRoot");
            DestroyImmediate(dungeon);
        }

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Dungeon"))
        {
            GameObject dungeon = GameObject.Find("DungeonRoot");
            DestroyImmediate(dungeon);

            myInData = new DGenerate_InData();
            myInData.Depth = myDepth;
            myInData.Seed = mySeed;
            myInData.AllowHeightDifference = myAllowHeightDifference;

            dungeonGenerator = GameObject.FindGameObjectWithTag("DungeonGenerator").GetComponent<DungeonGenerator>();
            dungeonGenerator.GenerateDungeon(myInData);
        }
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Save Dungeon"))
        {

        }
    }
}
