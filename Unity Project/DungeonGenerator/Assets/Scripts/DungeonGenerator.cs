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
    public float CorridorBias = 0f;
    public float RoomChance = 0f;
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
    Staircase_UP,
    Staircase_DOWN,
    Piece_Center,
    Piece_Door,
    Piece_RCorner,
    Piece_LCorner,
    Piece_LeftWall,
    Piece_RightWall,
    Piece_FrontWall,
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
    Dictionary<Vector3Int, GameObject> myDungeonSlots;
    List<GameObject> myDungeon;
    //The blocktype direction map stores what directions a room may proceed building towards. eg rooms with no exit to the left wont try to build a room to the left of them.
    Dictionary<BlockType, List<Direction>> myBlockTypeDirectionMap;
    public List<GameObject> GenerateDungeon(DGenerate_InData aData)
    {
        Debug.Log("Starting Dungeon Generation");
        myData = aData;
        myDungeonSlots = new Dictionary<Vector3Int, GameObject>();
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

    void GenerateCorridor(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    {
        Transform exitParent = aParent.transform.Find("Exits");

        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < exitParent.childCount; i++)
        {
            Transform child = exitParent.GetChild(i);
            if (!myDungeonSlots.ContainsKey(RoundToInt(child.transform.position)))
            {
                GameObject room;
                bool roomFits = false;
                Room roomComp;
                while (!roomFits)
                {
                    roomFits = false;
                    room = InstantiateRandom(child);
                    list.Add(room);
                    room.transform.rotation = child.rotation;
                    room.transform.position = child.transform.position;
                    roomComp = room.GetComponent<Room>();
                    roomComp.SetWorldPos(child.transform.position);
                    roomComp.SetDepth(aCurrentDepth);


                    List<Vector3Int> possibleNeighbours= new List<Vector3Int>();
                    possibleNeighbours.Add(RoundToInt(child.transform.position) + Vector3Int.right);
                    possibleNeighbours.Add(RoundToInt(child.transform.position) + Vector3Int.left);
                    possibleNeighbours.Add(RoundToInt(child.transform.position) + Vector3Int.forward);
                    possibleNeighbours.Add(RoundToInt(child.transform.position) + Vector3Int.back);

                    for (int x = 0; x < possibleNeighbours.Count; x++)
                    {
                        bool spotIsTaken = false;
                        if (myDungeonSlots.ContainsKey(RoundToInt(possibleNeighbours[x])))
                        {
                            spotIsTaken = true;
                        }

                        bool stillFits = false;
                        if (spotIsTaken)
                        {
                            Transform neighbour = myDungeonSlots[RoundToInt(possibleNeighbours[x])].transform;
                            if (neighbour.Find("Exits"))
                            {
                                neighbour = neighbour.Find("Exits");
                                for (int y = 0; y < neighbour.childCount; y++)
                                {
                                    if (RoundToInt(neighbour.GetChild(y).transform.position) == RoundToInt(room.transform.position))
                                    {
                                        stillFits = true;
                                    }
                                }
                            }
                            else
                            {
                                stillFits = false;
                            }
                        }

                        if (spotIsTaken == false || stillFits)
                        {
                            roomFits = true;
                            break;
                        }
                    }

                    if (roomFits == false)
                    {
                        list.Remove(room);
                        DestroyImmediate(room);
                        continue;
                    }
                    else
                    {
                        myDungeonSlots[RoundToInt(child.transform.position)] = child.gameObject;

                        if (myData.genType == GenerationType.DepthFirst)
                        {
                            if (roomComp.myType == BlockType.Staircase_UP)
                            {
                                myDungeonSlots[RoundToInt(room.transform.position) + Vector3Int.up] = room;
                            }
                            else if (roomComp.myType == BlockType.Staircase_DOWN)
                            {
                                myDungeonSlots[RoundToInt(room.transform.position) + Vector3Int.down] = room;
                            }

                            GenerateBlock(room, aCurrentDepth + 1, aMaxDepth);
                        }
                    }
                }
            }
        }

        if (myData.genType == GenerationType.BreadthFirst)
        {
            for (int i = 0; i < list.Count; i++)
            {

                if (!list[i])
                {
                    continue;
                }

                Room roomComp = list[i].GetComponent<Room>();
                if (roomComp.myType == BlockType.Staircase_UP)
                {
                    myDungeonSlots[RoundToInt(list[i].transform.position) + Vector3Int.up] = list[i];
                }
                else if (roomComp.myType == BlockType.Staircase_DOWN)
                {
                    myDungeonSlots[RoundToInt(list[i].transform.position) + Vector3Int.down] = list[i];
                }
                GenerateBlock(list[i], aCurrentDepth + 1, aMaxDepth);
            }
        }
    }

    GameObject InstantiateRandom(Transform aTransform)
    {
        BlockType type;

        if (Random.Range(0, 100) <= myData.CorridorBias)
        {
            type = BlockType.Room_1EF;
        }
        else
        {
            if (myData.AllowHeightDifference)
            {
                type = (BlockType)Random.Range(0, (int)BlockType.Piece_Center);
            }
            else
            {
                type = (BlockType)Random.Range(0, (int)BlockType.Staircase_UP);
            }
        }

        GameObject gameObject;
        gameObject = Instantiate(myBlocks[type][0], aTransform);
        gameObject.transform.position = RoundToInt(gameObject.transform.position);
        Room room = gameObject.AddComponent<Room>();
        room.myType = type;
        return gameObject;
    }


    void GenerateRoom(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    {

        if (aCurrentDepth >= aMaxDepth)
        {
            return;
        }

        Transform exitParent = aParent.transform.Find("Exits");
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < exitParent.childCount; i++)
        {
            Transform child = exitParent.GetChild(i);
            if (!myDungeonSlots.ContainsKey(RoundToInt(child.transform.position)))
            {

                BlockType type = (BlockType)(Random.Range((int)BlockType.Piece_Center, (int)BlockType.Piece_FrontWall));
                GameObject room = InstantiateRoomOfType(child, type);
                list.Add(room);
                room.transform.rotation = child.rotation;
                room.transform.position = child.transform.position;
                Room roomComp = room.GetComponent<Room>();
                roomComp.SetWorldPos(child.transform.position);
                roomComp.SetDepth(aCurrentDepth);
                myDungeonSlots[RoundToInt(child.transform.position)] = child.gameObject;


                if (myData.genType == GenerationType.DepthFirst && roomComp.myType != BlockType.Piece_Door)
                {
                    GenerateRoom(room, aCurrentDepth + 1, aMaxDepth);
                }
                else if (roomComp.myType == BlockType.Piece_Door)
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
                if (roomComp.myType == BlockType.Piece_Door)
                {
                    GenerateBlock(list[i], aCurrentDepth + 1, aMaxDepth);
                    continue;
                }
                GenerateRoom(list[i], aCurrentDepth + 1, aMaxDepth);
            }
        }
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

        // if (Random.Range(0, 100) <= myData.RoomChance /*&& aParent.GetComponent<Room>().myType != BlockType.Piece_Door*/)
        // {
        //     GameObject door = InstantiateRoomOfType(aParent.transform, BlockType.Piece_Door);
        //     GenerateRoom(door, aCurrentDepth, aMaxDepth);
        // }
        // else
        // {
        GenerateCorridor(aParent, aCurrentDepth, aMaxDepth);
        // }

    }

    GameObject InitializeParent()
    {
        GameObject dungeonParent = new GameObject("DungeonRoot");
        GameObject exitChild = InstantiateRoomOfType(dungeonParent.transform, BlockType.Entry);

        myDungeonSlots[RoundToInt(exitChild.transform.position)] = exitChild;
        GameObject start = GameObject.CreatePrimitive(PrimitiveType.Cube);
        start.transform.parent = dungeonParent.transform;
        start.transform.position += Vector3.up * 0.5f;
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
        myBlocks[BlockType.Staircase_UP] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Staircase_UP");
        myBlocks[BlockType.Staircase_DOWN] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Staircase_DOWN");
        myBlocks[BlockType.Piece_Center] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Piece_Center");
        myBlocks[BlockType.Piece_Door] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Piece_Door");
        myBlocks[BlockType.Piece_FrontWall] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Piece_LRWall");
        myBlocks[BlockType.Piece_LeftWall] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Piece_FRWall");
        myBlocks[BlockType.Piece_RightWall] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Piece_RightWall");
        myBlocks[BlockType.Piece_LCorner] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Piece_LCorner");
        myBlocks[BlockType.Piece_RCorner] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Piece_RCorner");
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
