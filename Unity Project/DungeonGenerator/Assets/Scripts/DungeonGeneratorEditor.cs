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
    GenerationType myGenType = GenerationType.DepthFirst;
    DGenerate_InData myInData;
    DungeonGenerator dungeonGenerator;

    private void OnGUI()
    {
        GUILayout.Label("Dungeon Generation Data", EditorStyles.boldLabel);
        myFileName = EditorGUILayout.TextField(new GUIContent("Dungeon Name", "This is the filename used to save the dungeon."), myFileName);
        myDepth = EditorGUILayout.IntField(new GUIContent("Dungeon Depth", "The depth decides how far the dungeon generates."), myDepth);
        mySeed = EditorGUILayout.IntField(new GUIContent("Dungeon Seed", "If the seed is 0 the dungeon is randomly generated. Any other value locks the generation so that the same dungeon will be generated over and over again."), mySeed);
        myAllowHeightDifference = EditorGUILayout.Toggle(new GUIContent("Allow Height Difference", "Allows the generator to generate on the y axis aswell."), myAllowHeightDifference);
        myGenType = (GenerationType)EditorGUILayout.EnumPopup(new GUIContent("Generation Type", "Depth first generates the dungeon one room chain at a time, Breadth first generates it radiating out from the root."), myGenType);
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
            myInData.genType = myGenType;
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
