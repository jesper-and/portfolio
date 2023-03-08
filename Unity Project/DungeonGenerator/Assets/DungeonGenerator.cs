using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class DGenerate_InData
{
    public int Depth = 1;
    public int Seed = 0;
    public bool AllowHeightDifference = true;
}

enum BlockType
{
    Room_1ER,
    Room_1EL,
    Room_1EF,
    Room_2ELR,
    Room_2EFR,
    Room_2EFL,
    Room_3E,
    Corridor,
    Entry,
    Exit,
    Default
}

enum Direction
{
    Left, Right, Forward, Back, Up, Down, End
}

class Room
{
    void Init(int aDepth)
    {
        myDepth = aDepth;
        myDoorways = 2;
    }

    int myDepth;
    int myDoorways;
    BlockType myType = BlockType.Room_1ER;
}

public class DungeonGenerator : MonoBehaviour
{

    DGenerate_InData myData;
    Dictionary<BlockType, GameObject[]> myBlocks;
    public void GenerateDungeon(DGenerate_InData aData)
    {
        Debug.Log("Starting Dungeon Generation");
        Vector3 position = Vector3.zero;
        myData = aData;
        myBlocks = new Dictionary<BlockType, GameObject[]>();
        myBlocks[BlockType.Room_1ER] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/1ER");       

        GameObject dungeonParent = new GameObject("DungeonRoot");
        GameObject currentParent = dungeonParent;

        GenerateBlock(currentParent, currentParent.transform.position, 0, aData.Depth);

    }
    void GenerateRoom(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    {
        GameObject room = InstantiateRandom(aParent.transform);
        Direction blockDirection;
        if (myData.AllowHeightDifference)
        {
            blockDirection = (Direction)Random.Range(0, (int)Direction.End);
        }
        else
        {
            blockDirection = (Direction)Random.Range(0, 4);
        }


        if (room.transform.parent.gameObject.name != "DungeonRoot")
        {
            switch (blockDirection)
            {
                case Direction.Left:
                    room.transform.position += Vector3.left;
                    room.transform.localEulerAngles = new Vector3(0f, -90, 0);
                    break;
                case Direction.Right:
                    room.transform.position += Vector3.right;
                   room.transform.localEulerAngles = new Vector3(0f, 90, 0);
                    break;
                case Direction.Forward:
                    room.transform.position += Vector3.forward;
                    break;

                case Direction.Back:
                    room.transform.position += Vector3.back;
                    room.transform.localEulerAngles = new Vector3(0f, 180, 0);
                    break;

                case Direction.Up:
                    room.transform.position += Vector3.up;
                    break;

                case Direction.Down:
                    room.transform.position += Vector3.down;
                    break;
            }
        }

        GenerateBlock(room, room.transform.position, aCurrentDepth + 1, aMaxDepth);
    }

    GameObject InstantiateRandom(Transform aTransform)
    {
        BlockType type = (BlockType)Random.Range(0, (int)BlockType.Default);
        GameObject gameObject = GameObject.Instantiate(myBlocks[BlockType.Room_1ER][0], aTransform);
        return gameObject;
    }

    void GenerateBlock(GameObject aParent, Vector3 aPosition, int aCurrentDepth, int aMaxDepth)
    {
        if (aCurrentDepth > aMaxDepth)
        {
            Debug.Log("Finished Generating Dungeon.");
            return;
        }
        GenerateRoom(aParent, aCurrentDepth, aMaxDepth);
    }

}
