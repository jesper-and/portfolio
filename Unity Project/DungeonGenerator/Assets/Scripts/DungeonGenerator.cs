using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class DGenerate_InData
{
    public int Depth = 1;
    public int Seed = 0;
    public float CorridorBias = 0f;
    public int myLargeRooms = 1;
    public bool AllowHeightDifference = true;
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
    Default,
    Entry,
    End,
    LargeRoom
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
    List<GameObject> myRooms;
    //The blocktype direction map stores what directions a room may proceed building towards. eg rooms with no exit to the left wont try to build a room to the left of them.
    Dictionary<BlockType, List<Direction>> myBlockTypeDirectionMap;
    public List<GameObject> GenerateDungeon(DGenerate_InData aData)
    {
        Debug.Log("Starting Dungeon Generation");
        myData = aData;
        myDungeonSlots = new Dictionary<Vector3Int, GameObject>();
        myDungeon = new List<GameObject>();
        myRooms = new List<GameObject>();

        InitializeBuildingBlocks();
        GameObject dungeonParent = InitializeParent();

        if (myData.Seed != 0)
        {
            Random.InitState(myData.Seed);
        }

        for (int i = 0; i < myData.myLargeRooms; i++)
        {
            GenerateRoom(dungeonParent.transform);
        }

        foreach (GameObject room in myRooms)
        {
            Transform tiles = room.transform.Find("Tiles");
            for (int i = 0; i < tiles.childCount; i++)
            {
                if (tiles.GetChild(i).Find("Exits") == null)
                {
                    continue;
                }

                GenerateBlock(tiles.GetChild(i).gameObject, 1, aData.Depth);
            }
        }

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
                GameObject corridor = CreateValidCorridor(child, RoundToInt(child.transform.position));
                Room roomComp = corridor.GetComponent<Room>();
                roomComp.SetDepth(aCurrentDepth);

                if (roomComp.myType == BlockType.Staircase_UP)
                {
                    myDungeonSlots[RoundToInt(corridor.transform.position) + Vector3Int.up] = corridor;
                }
                else if (roomComp.myType == BlockType.Staircase_DOWN)
                {
                    myDungeonSlots[RoundToInt(corridor.transform.position) + Vector3Int.down] = corridor;
                }

                GenerateBlock(corridor, aCurrentDepth + 1, aMaxDepth);
            }
        }
    }

    GameObject InstantiateRandom(Transform aTransform)
    {
        BlockType type;

        Transform parent = aTransform;
        float CorridorPercentage = myData.CorridorBias;
        while (parent.parent)
        {
            Room attemptRoom;
            if (parent.parent.gameObject.TryGetComponent<Room>(out attemptRoom))
            {
                if (attemptRoom.myType == BlockType.Room_1EF)
                {
                    CorridorPercentage -= 1;
                }
                else
                {
                    CorridorPercentage = myData.CorridorBias;
                }
            }
            parent = parent.parent;
        }

        if (Random.Range(0, 100) <= CorridorPercentage)
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

    void GenerateRoom(Transform aParent)
    {
        bool overlapping = true;
        GameObject room = Instantiate(myBlocks[BlockType.LargeRoom][0], aParent);
        do
        {
            overlapping = false;
            int roomIndex = Random.Range(0, myBlocks[BlockType.LargeRoom].Length);
            int halfDepth = myData.Depth / 2;

            Vector3Int position = new Vector3Int(Random.Range(-halfDepth, halfDepth), 0, Random.Range(-halfDepth, halfDepth));
            if (myData.AllowHeightDifference)
            {
                int height = Random.Range(-halfDepth, halfDepth);
                position.y = height;
            }
            Vector3Int rotation = new Vector3Int(0, Random.Range(0, 3) * 90, 0);

            DestroyImmediate(room);
            room = Instantiate(myBlocks[BlockType.LargeRoom][roomIndex], aParent);
            room.transform.position = position;
            room.transform.rotation = Quaternion.Euler(rotation);

            Transform roomTransform = room.transform;
            Transform tiles = roomTransform.Find("Tiles");

            for (int i = 0; i < tiles.childCount; i++)
            {
                if (myDungeonSlots.ContainsKey(RoundToInt(tiles.GetChild(i).position)))
                {
                    overlapping = true;
                    break;
                }
            }

            if (overlapping == false)
            {
                for (int i = 0; i < tiles.childCount; i++)
                {
                    myDungeonSlots[RoundToInt(tiles.GetChild(i).position)] = tiles.GetChild(i).gameObject;
                }

                myRooms.Add(room);
            }

        } while (overlapping);

    }

    void GenerateBlock(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    {
        myDungeon.Add(aParent);
        if (aCurrentDepth > aMaxDepth)
        {
            GameObject end = CreateValidCorridor(aParent.transform, RoundToInt(aParent.transform.position), true);
            myDungeon.Add(end);

            end.transform.parent = end.transform.parent.parent;

            DestroyImmediate(aParent);
            return;
        }

        GenerateCorridor(aParent, aCurrentDepth, aMaxDepth);
    }

    GameObject CreateValidCorridor(Transform aParent, Vector3Int aPosition, bool aEmptyTileConnectionsAreFail = false)
    {
        GameObject corridor = InstantiateRandom(aParent);
        bool isValid = false;
        int attempts = 100;


        while (!isValid && attempts > 0)
        {
            attempts--;
            DestroyImmediate(corridor);

            if (aEmptyTileConnectionsAreFail)
            {
                corridor = InstantiateRoomOfType(aParent, BlockType.End);
                break;
            }
            else
            {
                corridor = InstantiateRandom(aParent);
            }

            List<Vector3Int> possibleNeighbours = new()
                    {
                        RoundToInt(aParent.position) + Vector3Int.right,
                        RoundToInt(aParent.position) + Vector3Int.left,
                        RoundToInt(aParent.position) + Vector3Int.forward,
                        RoundToInt(aParent.position) + Vector3Int.back,
                    };

            if (corridor.GetComponent<Room>().myType == BlockType.Staircase_UP || corridor.GetComponent<Room>().myType == BlockType.Staircase_DOWN)
            {
                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.forward);
                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.back);
                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.left);
                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.right);

                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.back);
                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.forward);
                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.right);
                possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.left);
            }

            Transform myExits = corridor.transform.Find("Exits");
            List<Transform> realNeighbours = new List<Transform>();
            for (int i = 0; i < possibleNeighbours.Count; i++)
            {
                if (!myDungeonSlots.ContainsKey(possibleNeighbours[i]))
                {
                    continue;
                }

                if (myDungeonSlots[possibleNeighbours[i]] == null)
                {
                    continue;
                }

                if (!IsParentTo(myDungeonSlots[possibleNeighbours[i]], corridor))
                {
                    realNeighbours.Add(myDungeonSlots[possibleNeighbours[i]].transform);
                }
            }

            Debug.Log("Neighbours defined : " + realNeighbours.Count);
            int okNeighbours = 0;

            for (int i = 0; i < realNeighbours.Count; i++)
            {

                if (ExitPointsHere(myExits, RoundToInt(realNeighbours[i].position)))
                {
                    okNeighbours++;
                }
            }

            Debug.Log("Neighbour count : " + realNeighbours.Count + "\nokNeighbours : " + okNeighbours);

            if (realNeighbours.Count == okNeighbours)
            {
                isValid = true;
            }
        }

        myDungeonSlots[aPosition] = corridor.gameObject;
        Debug.Log("Generated corridor at position: " + aPosition + "\nattempt : " + (100 - attempts));
        return corridor;
    }

    bool IsParentTo(GameObject potentialParent, GameObject potentialChild)
    {
        Transform parentExits = potentialParent.transform.Find("Exits");
        if (parentExits == null)
        {
            Debug.Log("Could not find exit object in potential parent, returning false.");
            return false;
        }

        if (potentialChild.transform.parent.parent.parent.gameObject.GetInstanceID() == parentExits.gameObject.GetInstanceID())
        {
            return true;
        }
        return false;
    }

    bool ExitPointsHere(Transform anExitParent, Vector3Int aPosition)
    {
        for (int i = 0; i < anExitParent.childCount; i++)
        {
            Debug.Log("Checking if " + RoundToInt(anExitParent.GetChild(i).position) + " is = " + aPosition);
            if (RoundToInt(anExitParent.GetChild(i).position) == aPosition)
            {
                return true;
            }
        }
        return false;
    }

    BlockType TypeOfObjectAtPosition(Vector3Int aPosition)
    {
        if (!myDungeonSlots.ContainsKey(aPosition))
        {
            return BlockType.Default;
        }

        Room room;
        if (myDungeonSlots[aPosition].gameObject.TryGetComponent<Room>(out room))
        {
            return room.myType;
        }

        return BlockType.Default;
    }

    GameObject InitializeParent()
    {
        GameObject dungeonParent = new GameObject("DungeonRoot");
        return dungeonParent;
    }
    void InitializeBuildingBlocks()
    {
        myBlocks = new Dictionary<BlockType, GameObject[]>();
        myBlocks[BlockType.Entry] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Start");
        myBlocks[BlockType.Room_1ER] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/1ER");
        myBlocks[BlockType.Room_1EF] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/1EF");
        myBlocks[BlockType.Room_1EL] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/1EL");
        myBlocks[BlockType.Room_2EFL] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/2EFL");
        myBlocks[BlockType.Room_2EFR] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/2EFR");
        myBlocks[BlockType.Room_2ELR] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/2ELR");
        myBlocks[BlockType.Room_3E] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/3E");
        myBlocks[BlockType.Staircase_UP] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Staircase_UP");
        myBlocks[BlockType.Staircase_DOWN] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Staircase_DOWN");
        myBlocks[BlockType.LargeRoom] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/Rooms");
        myBlocks[BlockType.End] = Resources.LoadAll<GameObject>("prefabs/BuildingBlocks/End");
    }
}
