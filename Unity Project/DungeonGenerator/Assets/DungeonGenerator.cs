using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class DGenerate_InData
{
    public int Depth = 1;
    public int Seed = 0;
}

class Block
{

}

enum BlockType
{
    Room,
    Corridor,
    Entry,
    Exit
}

enum Direction
{
    Left, Right, Forward, End
}

class Room : Block
{
    void Init(int aDepth)
    {
        myDepth = aDepth;
        myDoorways = 2;
    }

    int myDepth;
    int myDoorways;
    BlockType myType = BlockType.Room;
}

class Corridor : Block
{
    void Init(int aDepth)
    {
        myDepth = aDepth;
        myDoorways = 1;
    }

    int myDepth;
    int myDoorways;
    BlockType myType = BlockType.Corridor;
}

public class DungeonGenerator : MonoBehaviour
{

    List<GameObject> oneDoorRooms = new List<GameObject>();


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void GenerateDungeon(DGenerate_InData aData)
    {
        Debug.Log("Starting Dungeon Generation");
        int currentDepth = 0;
        Vector3 position = Vector3.zero;

        string[] guids = AssetDatabase.FindAssets("t: prefab", new string[] { "Assets/prefabs/BuildingBlocks/2DR" });



        foreach (string guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            oneDoorRooms.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        GameObject dungeonParent = new GameObject("DungeonRoot");

        GameObject currentParent = dungeonParent;

        GenerateBlock(dungeonParent, dungeonParent.transform.position, 0, aData.Depth);
    }

    void GenerateRoom(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    {
        GameObject room = GameObject.Instantiate(oneDoorRooms[0].gameObject, aParent.transform);

        Direction blockDirection = (Direction)Random.Range(0, (int)Direction.End);

        switch (blockDirection)
        {
            case Direction.Left:
                room.transform.position += Vector3.left;
                break;
            case Direction.Right:
            room.transform.position += Vector3.right;
                break;
            case Direction.Forward:
                room.transform.position += Vector3.forward;
                break;
        }

        GenerateBlock(room, room.transform.position, aCurrentDepth + 1, aMaxDepth);
    }

    void GenerateBlock(GameObject aParent, Vector3 aPosition, int aCurrentDepth, int aMaxDepth)
    {
        if (aCurrentDepth > aMaxDepth)
        {
            Debug.Log("Finished Generating Dungeon.");
            return;
        }

        BlockType type = (BlockType)(Random.Range(0, 2) % 2);

        if (type == BlockType.Room)
        {
            GenerateRoom(aParent, aCurrentDepth, aMaxDepth);
        }
        else if (type == BlockType.Corridor)
        {
            GenerateRoom(aParent, aCurrentDepth, aMaxDepth);
        }
        else
        {
            Debug.Log(type);
        }
    }

}
