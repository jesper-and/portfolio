using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System;
using System.IO;
using Palmmedia.ReportGenerator.Core;
using UnityEngine.UI;
using System.Diagnostics;

public class DungeonGeneratorEditor : EditorWindow
{
    [MenuItem("Dungeon Generation/ Generation data")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(DungeonGeneratorEditor));
    }

    string myFileName = "default";
    Vector3Int gridSize = Vector3Int.one;
    int mySeed = 0;
    int myLargeRooms = 0;
    Stopwatch timer = new Stopwatch();
    bool createReadableJson = false;
    DGenerate_InData myInData;
    DungeonGenerator dungeonGenerator;
    List<GameObject> finishedDungeon = new List<GameObject>();
    private void OnGUI()
    {
        Variables();
        EditorGUILayout.BeginHorizontal();
        ClearButton();
        GenerateButton();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        SaveDungeon();
        EditorGUILayout.EndHorizontal();
        SaveVariables();
    }

    private void Variables()
    {
        GUILayout.Label("Dungeon Generation Data", EditorStyles.boldLabel);
        myFileName = EditorGUILayout.TextField(new GUIContent("Dungeon Name", "This is the filename used to save the dungeon."), myFileName);
        gridSize = EditorGUILayout.Vector3IntField(new GUIContent("Dungeon Depth", "The depth decides how far the dungeon generates."), gridSize);
        myLargeRooms = EditorGUILayout.IntField(new GUIContent("Amount of rooms", "Amount of large rooms in the dungeon."), myLargeRooms);
        mySeed = EditorGUILayout.IntField(new GUIContent("Dungeon Seed", "If the seed is 0 the dungeon is randomly generated. Any other value locks the generation so that the same dungeon will be generated over and over again."), mySeed);
    }

    private void ClearButton()
    {
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear Dungeon"))
        {
            GameObject dungeon = GameObject.Find("DungeonRoot");
            DestroyImmediate(dungeon);
            finishedDungeon.Clear();
        }
    }

    private void GenerateButton()
    {
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Dungeon"))
        {
            timer.Restart();
            timer.Start();
            GameObject dungeon = GameObject.Find("DungeonRoot");
            DestroyImmediate(dungeon);

            myInData = new DGenerate_InData();
            myInData.gridSize = gridSize;
            myInData.Seed = mySeed;
            myInData.myLargeRooms = myLargeRooms;
            dungeonGenerator = GameObject.FindGameObjectWithTag("DungeonGenerator").GetComponent<DungeonGenerator>();
            finishedDungeon = dungeonGenerator.GenerateDungeon(myInData);
            timer.Stop();

            UnityEngine.Debug.Log("Time to generate : " + timer.Elapsed.TotalSeconds.ToString() + " seconds");
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public Vector3 position;
        public Vector3 rotation;
        public String meshName;
        public String roomType;
    }

    private void SaveDungeon()
    {
        GUI.backgroundColor = Color.white;
        createReadableJson = EditorGUILayout.ToggleLeft(new GUIContent("Save as readable json", "Saves the file in a readable format, will take longer to produce."), createReadableJson);

        if (GUILayout.Button("Save Dungeon"))
        {
            if (finishedDungeon.Count < 1)
            {
                UnityEngine.Debug.LogWarning("Tried saving an empty Dungeon.");
                return;
            }
            timer.Restart();
            timer.Start();

            String fileName = myFileName;
            if (File.Exists("Assets/Generated Dungeons/" + fileName + ".json"))
            {
                fileName += "(copy)";
            }

            String output = new String("[");
            for (int i = 0; i < finishedDungeon.Count; i++)
            {
                SaveData data = new SaveData();
                data.position = dungeonGenerator.RoundToInt(finishedDungeon[i].transform.position);
                data.rotation = dungeonGenerator.RoundToInt(finishedDungeon[i].transform.rotation.eulerAngles);

                if (finishedDungeon[i].GetComponent<MeshFilter>())
                {
                    data.meshName = finishedDungeon[i].GetComponent<MeshFilter>().mesh.ToString();
                }
                data.roomType = finishedDungeon[i].GetComponent<Room>().myType.ToString();

                output += JsonUtility.ToJson(data, createReadableJson) + ",";
            }

            output = output.Substring(0, output.Length - 1);
            output += "]";
            File.WriteAllText("Assets/Generated Dungeons/" + fileName + ".json", output);
            UnityEngine.Debug.Log("Dungeon file saved at " + "Assets/Generated Dungeons/" + fileName + ".json");
            UnityEngine.Debug.Log("Time to generate save file: " + timer.Elapsed.TotalSeconds.ToString() + " seconds.");
        }
    }

    void SaveVariables()
    {
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Save preset variables"))
        {
            String fileName = myFileName + "_presets";
            if (File.Exists("Assets/presets/" + fileName + ".json"))
            {
                fileName += "(copy)";
            }
            String output = new String("");
            output = JsonUtility.ToJson(myInData, true);
            File.WriteAllText("Assets/presets/" + fileName + ".json", output);
            UnityEngine.Debug.Log("Dungeon file saved at " + "Assets/presets/" + fileName + ".json");
        }
    }
}
