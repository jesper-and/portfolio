using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public enum GenerationType
{
    DepthFirst,
    BreadthFirst
}

[System.Serializable]
public class DGenerate_InData
{
    public int Depth = 1;
    public int Seed = 0;
    public bool AllowHeightDifference = true;
    public GenerationType genType = GenerationType.DepthFirst;
}

public enum BlockType
{
    Room_1ER,
    Room_1EL,
    Room_1EF,
    Room_2ELR,
    Room_2EFR,
    Room_2EFL,
    Room_3E,
    Staircase,
    Default,
    Entry
    // Exit,
}

public enum Direction
{
    Left, Right, Forward, Back, Up, Down, End
}

public class DungeonGenerator : MonoBehaviour
{
    DGenerate_InData myData;
    Dictionary<BlockType, GameObject[]> myBlocks;
    Dictionary<Vector3Int, bool> myDungeonSlots;
    List<GameObject> myDungeon;
    //The blocktype direction map stores what directions a room may proceed building towards. eg rooms with no exit to the left wont try to build a room to the left of them.
    Dictionary<BlockType, List<Direction>> myBlockTypeDirectionMap;
    public List<GameObject> GenerateDungeon(DGenerate_InData aData)
    {
        Debug.Log("Starting Dungeon Generation");
        myData = aData;
        myDungeonSlots = new Dictionary<Vector3Int, bool>();
        myDungeon = new List<GameObject>();

        InitializeBuildingBlocks();
        GameObject dungeonParent = InitializeParent();


        if (myData.Seed != 0)
        {
            Random.InitState(myData.Seed);
        }
        GenerateBlock(dungeonParent, 1, aData.Depth);
        Debug.Log("Finished Generating Dungeon.");
        return myDungeon;
    }

    void GenerateRoom(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    {
        Transform exitParent = aParent.transform.Find("Exits");
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < exitParent.childCount; i++)
        {
            Transform child = exitParent.GetChild(i);
            if (!myDungeonSlots.ContainsKey(RoundToInt(child.transform.position)))
            {
                GameObject room = InstantiateRandom(child);
                list.Add(room);
                room.transform.rotation = child.rotation;
                room.transform.position = child.transform.position;
                Room roomComp = room.GetComponent<Room>();
                roomComp.SetWorldPos(child.transform.position);
                roomComp.SetDepth(aCurrentDepth);
                myDungeonSlots[RoundToInt(child.transform.position)] = true;

                if (roomComp.myType == BlockType.Staircase)
                {
                    myDungeonSlots[RoundToInt(child.transform.position) + Vector3Int.up] = true;
                }

                if (myData.genType == GenerationType.DepthFirst)
                {
                    GenerateBlock(room, aCurrentDepth + 1, aMaxDepth);
                }
            }
        }

        if (myData.genType == GenerationType.BreadthFirst)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Room roomComp = list[i].GetComponent<Room>();
                if (roomComp.myType == BlockType.Staircase)
                {
                    myDungeonSlots[RoundToInt(list[i].transform.position) + Vector3Int.up] = true;
                }
                GenerateBlock(list[i], aCurrentDepth + 1, aMaxDepth);
            }
        }
    }

    GameObject InstantiateRandom(Transform aTransform)
    {
        BlockType type;

        if (Random.Range(0, 100) < 40)
        {
            type = BlockType.Room_1EF;
        }
        else
        {
            if (myData.AllowHeightDifference)
            {
                type = (BlockType)Random.Range(0, (int)BlockType.Default);
            }
            else
            {
                type = (BlockType)Random.Range(0, (int)BlockType.Staircase);
            }
        }

        GameObject gameObject;
        gameObject = Instantiate(myBlocks[type][0], aTransform);
        gameObject.transform.position = RoundToInt(gameObject.transform.position);
        Room room = gameObject.AddComponent<Room>();
        room.myType = type;
        return gameObject;
    }

    GameObject InstantiateRoomOfType(Transform aTransform, BlockType aType)
    {
        GameObject gameObject;
        gameObject = Instantiate(myBlocks[aType][0], aTransform);
        gameObject.transform.position = RoundToInt(gameObject.transform.position);
        Room room = gameObject.AddComponent<Room>();
        room.myType = aType;
        return gameObject;
    }

    public Vector3Int RoundToInt(Vector3 aVector)
    {
        Vector3Int result = new Vector3Int();
        result.x = Mathf.RoundToInt(aVector.x);
        result.y = Mathf.RoundToInt(aVector.y);
        result.z = Mathf.RoundToInt(aVector.z);

        return result;
    }

    void GenerateBlock(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    {
        myDungeon.Add(aParent);
        if (aCurrentDepth > aMaxDepth)
        {
            return;
        }
        GenerateRoom(aParent, aCurrentDepth, aMaxDepth);
    }

    GameObject InitializeParent()
    {
        GameObject dungeonParent = new GameObject("DungeonRoot");
        GameObject exitChild = InstantiateRoomOfType(dungeonParent.transform, BlockType.Entry);

        myDungeonSlots[RoundToInt(exitChild.transform.position)] = true;
        GameObject start = GameObject.CreatePrimitive(PrimitiveType.Cube);
        start.transform.parent = dungeonParent.transform;
        start.transform.position += Vector3.up;
        start.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        start.GetComponent<MeshRenderer>().sharedMaterial.color = Color.green;
        return exitChild;
    }

    void InitializeBuildingBlocks()
    {
        myBlocks = new Dictionary<BlockType, GameObject[]>();
        myBlocks[BlockType.Entry] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/End Rooms");
        myBlocks[BlockType.Room_1ER] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/1ER");
        myBlocks[BlockType.Room_1EF] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/1EF");
        myBlocks[BlockType.Room_1EL] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/1EL");
        myBlocks[BlockType.Room_2EFL] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/2EFL");
        myBlocks[BlockType.Room_2EFR] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/2EFR");
        myBlocks[BlockType.Room_2ELR] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/2ELR");
        myBlocks[BlockType.Room_3E] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/3E");
        myBlocks[BlockType.Staircase] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Staircase");

        InitializeDirectionMap();
    }

    void InitializeDirectionMap()
    {
        myBlockTypeDirectionMap = new Dictionary<BlockType, List<Direction>>();

        myBlockTypeDirectionMap[BlockType.Entry] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Entry].Add(Direction.Forward);

        myBlockTypeDirectionMap[BlockType.Room_1ER] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Room_1ER].Add(Direction.Right);

        myBlockTypeDirectionMap[BlockType.Room_1EF] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Room_1EF].Add(Direction.Forward);

        myBlockTypeDirectionMap[BlockType.Room_1EL] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Room_1EL].Add(Direction.Left);

        myBlockTypeDirectionMap[BlockType.Room_2EFL] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Room_2EFL].Add(Direction.Forward);
        myBlockTypeDirectionMap[BlockType.Room_2EFL].Add(Direction.Left);

        myBlockTypeDirectionMap[BlockType.Room_2EFR] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Room_2EFR].Add(Direction.Forward);
        myBlockTypeDirectionMap[BlockType.Room_2EFR].Add(Direction.Right);

        myBlockTypeDirectionMap[BlockType.Room_2ELR] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Room_2ELR].Add(Direction.Forward);
        myBlockTypeDirectionMap[BlockType.Room_2ELR].Add(Direction.Left);

        myBlockTypeDirectionMap[BlockType.Room_3E] = new List<Direction>();
        myBlockTypeDirectionMap[BlockType.Room_3E].Add(Direction.Forward);
        myBlockTypeDirectionMap[BlockType.Room_3E].Add(Direction.Right);
        myBlockTypeDirectionMap[BlockType.Room_3E].Add(Direction.Left);
    }

}
