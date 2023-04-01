using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class DGenerate_InData
{
    public Vector3Int gridSize = Vector3Int.one;
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

public enum TileState
{
    Open, Closed, Empty,
    Passable,
    Door
}

enum Directions
{
    North, East, South, West
}

public class GridData
{
    public GameObject myTile;
    public TileState myState = TileState.Empty;
    public float G = 3000f;
    public float F(GridData Target)
    {
        return G + H(Target);
    }

    public float H(GridData Target)
    {
        return DungeonGenerator.Manhattan(this, Target);
    }

    public Vector3Int WorldPosition;
    public GridData Parent;
}

public class DungeonGenerator : MonoBehaviour
{
    DGenerate_InData myData;
    Dictionary<BlockType, GameObject[]> myBlocks;
    GridData[,,] myDungeonSlots;
    List<GameObject> myDungeonList;
    List<GameObject> myRooms;

    public static float Manhattan(GridData start, GridData end)
    {
        return Mathf.Abs(start.WorldPosition.x - end.WorldPosition.x) + Mathf.Abs(start.WorldPosition.y - end.WorldPosition.y) + Mathf.Abs(start.WorldPosition.z - end.WorldPosition.z);
    }

    //generate rooms
    //pick out door pairs.
    //priority is:
    //for every door, loop through all doors, if door is me, ignore. if door is taken, ignore. if door is closest, set as current option, if another door is closer, set that as current option.
    //if no door was free, take closest option.
    //Door isnt already in a connection
    //Door is closest to start point
    //pathfind inbetween door pairs.

    void SetupConnections(GameObject[] someDoors)
    {
        foreach (GameObject door in someDoors)
        {
            Doormat og_doormat = door.GetComponent<Doormat>();
            float distance = float.MaxValue;
            Doormat linkTarget = og_doormat;
            int og_ID = door.gameObject.GetInstanceID();
            int linkID = linkTarget.gameObject.GetInstanceID();

            bool allowMultiConnections = false;
            while (linkID == og_ID)
            {
                foreach (GameObject otherDoor in someDoors)
                {
                    Doormat ot_doormat = otherDoor.GetComponent<Doormat>();
                    if (otherDoor.gameObject.GetInstanceID() == door.gameObject.GetInstanceID() || DoorsAreInSameRoom(og_doormat, ot_doormat))
                    {
                        continue;
                    }


                    if (allowMultiConnections == false && ot_doormat.connections.Count > 0)
                    {
                        continue;
                    }

                    float newdistance = Vector3.Distance(door.transform.position, otherDoor.transform.position);
                    if (distance > newdistance) // finds one that is already taken and cant path there but also cant find new target?
                    {
                        linkTarget = ot_doormat;
                        distance = newdistance;
                        linkID = linkTarget.gameObject.GetInstanceID();
                    }
                }

                if (linkID == og_ID)
                {
                    allowMultiConnections = true;
                }
            }

            if (linkID != og_ID && !linkTarget.connections.Contains(og_doormat))
            {
                og_doormat.connections.Add(linkTarget);
                linkTarget.connections.Add(og_doormat);
            }
        }
    }

    public bool DoorsAreInSameRoom(Doormat one, Doormat two)
    {
        if (one.transform.parent.GetInstanceID() == two.transform.parent.GetInstanceID())
        {
            return true;
        }

        return false;
    }
    public List<GameObject> GenerateDungeon(DGenerate_InData aData)
    {
        Debug.Log("Starting Dungeon Generation");
        myData = aData;
        myDungeonSlots = new GridData[aData.gridSize.x, aData.gridSize.y, aData.gridSize.z];
        myDungeonList = new List<GameObject>();
        myRooms = new List<GameObject>();

        InitializeBuildingBlocks();
        GameObject dungeonParent = InitializeParent();

        if (myData.Seed != 0)
        {
            Random.InitState(myData.Seed);
        }

        for (int x = 0; x < aData.gridSize.x; x++)
        {
            for (int y = 0; y < aData.gridSize.y; y++)
            {
                for (int z = 0; z < aData.gridSize.z; z++)
                {
                    myDungeonSlots[x, y, z] = new GridData();
                    myDungeonSlots[x, y, z].WorldPosition = new Vector3Int(x, y, z);
                }
            }
        }

        for (int i = 0; i < myData.myLargeRooms; i++)
        {
            GenerateRoom(dungeonParent.transform);
        }

        GameObject[] allDoors = GameObject.FindGameObjectsWithTag("Doormat");

        SetupConnections(allDoors);

        List<GridData> allPaths = PathfindAllDoors(allDoors);

        PopulatePaths(allPaths, dungeonParent);

        Debug.Log("Finished Generating Dungeon.");
        return myDungeonList;
    }

    void PopulatePaths(List<GridData> allPaths, GameObject dungeonParent)
    {
        foreach (GridData data in allPaths)
        {
            data.myTile = PlaceCorridor(data.WorldPosition, dungeonParent, allPaths);
            data.myTile.transform.position = data.WorldPosition;
        }
    }

    GameObject PlaceCorridor(Vector3Int aPosition, GameObject dungeonParent, List<GridData> allPaths)
    {
        Dictionary<Directions, GridData> neighbours = new Dictionary<Directions, GridData>();

        if (aPosition.x > 0)
        {
            if (allPaths.Contains(myDungeonSlots[aPosition.x - 1, aPosition.y, aPosition.z]))
            {
                neighbours.Add(Directions.West, myDungeonSlots[aPosition.x - 1, aPosition.y, aPosition.z]);
            }
        }

        if (aPosition.x < myData.gridSize.x - 1)
        {
            if (allPaths.Contains(myDungeonSlots[aPosition.x + 1, aPosition.y, aPosition.z]))
            {
                neighbours.Add(Directions.East, myDungeonSlots[aPosition.x + 1, aPosition.y, aPosition.z]);
            }
        }

        if (aPosition.z > 0)
        {
            if (allPaths.Contains(myDungeonSlots[aPosition.x, aPosition.y, aPosition.z - 1]))
            {
                neighbours.Add(Directions.South, myDungeonSlots[aPosition.x, aPosition.y, aPosition.z - 1]);
            }
        }

        if (aPosition.z < myData.gridSize.z - 1)
        {
            if (allPaths.Contains(myDungeonSlots[aPosition.x, aPosition.y, aPosition.z + 1]))
            {
                neighbours.Add(Directions.North, myDungeonSlots[aPosition.x, aPosition.y, aPosition.z + 1]);
            }
        }

        int neighbourCount = neighbours.Count;
        Vector3 rotation = Vector3.zero;


        GameObject result;
        BlockType type = BlockType.Staircase_UP;
        switch (neighbourCount)
        {
            case 0:
                type = BlockType.Staircase_DOWN;
                break;

            case 1:
                //result = InstantiateRoomOfType(dungeonParent.transform, BlockType.End);
                break;

            case 2:

                if (neighbours.ContainsKey(Directions.North))
                {
                    if (neighbours.ContainsKey(Directions.East))
                    {
                        type = BlockType.Room_1EL;
                        rotation = Vector3.up * 180;
                    }
                    else if (neighbours.ContainsKey(Directions.West))
                    {
                        type = BlockType.Room_1ER;
                        rotation = Vector3.up * 180;
                    }
                    else if (neighbours.ContainsKey(Directions.South))
                    {
                        type = BlockType.Room_1EF;
                    }
                }
                else if (neighbours.ContainsKey(Directions.West))
                {
                    if (neighbours.ContainsKey(Directions.South))
                    {
                        type = BlockType.Room_1EL;
                    }
                    else if (neighbours.ContainsKey(Directions.East))
                    {
                        type = BlockType.Room_1EF;
                        rotation = Vector3.up * 90;
                    }
                }
                else if(neighbours.ContainsKey(Directions.South))
                {
                    if (neighbours.ContainsKey(Directions.East))
                    {
                        type = BlockType.Room_1ER;
                    }
                }

                break;

            case 3:

                if (neighbours.ContainsKey(Directions.North) && neighbours.ContainsKey(Directions.South))
                {
                    if (neighbours.ContainsKey(Directions.West))
                    {
                        type = BlockType.Room_2EFL;
                    }

                    if (neighbours.ContainsKey(Directions.East))
                    {
                        type = BlockType.Room_2EFR;
                    }
                }

                if (neighbours.ContainsKey(Directions.West) && neighbours.ContainsKey(Directions.East))
                {
                    rotation = Vector3.up * 90;
                    if (neighbours.ContainsKey(Directions.North))
                    {
                        type = BlockType.Room_2EFL;
                    }

                    if (neighbours.ContainsKey(Directions.South))
                    {
                        type = BlockType.Room_2EFR;
                    }
                }
                break;

            case 4:

                if (neighbours.ContainsKey(Directions.North) && neighbours.ContainsKey(Directions.South) && neighbours.ContainsKey(Directions.West) && neighbours.ContainsKey(Directions.East))
                {
                    type = BlockType.Room_3E;
                }
                break;

            default:
                result = InstantiateRoomOfType(dungeonParent.transform, BlockType.Entry);
                break;
        }

        result = InstantiateRoomOfType(dungeonParent.transform, type);
        result.transform.rotation = Quaternion.Euler(rotation);
        return result;
    }

    List<GridData> PathfindAllDoors(GameObject[] allDoors)
    {
        List<GridData> result = new List<GridData>();

        foreach (GameObject door in allDoors)
        {
            Doormat doormat = door.GetComponent<Doormat>();
            List<Doormat> sucessfulCons = new List<Doormat>();
            foreach (Doormat connection in doormat.connections)
            {
                if (!connection.connections.Contains(doormat))
                {
                    continue;
                }

                Vector3Int startPos = RoundToInt(door.transform.position);
                Vector3Int endPos = RoundToInt(connection.gameObject.transform.position);
                List<GridData> path = Pathfind(startPos, endPos);
                result.AddRange(path);
                sucessfulCons.Add(connection);
            }

            for (int i = 0; i < sucessfulCons.Count; i++)
            {
                doormat.connections.Remove(sucessfulCons[i]);
            }
        }


        result = FilterDoubles(result);
        return result;
    }

    List<GridData> FilterDoubles(List<GridData> aList)
    {
        HashSet<GridData> temp = new HashSet<GridData>(aList);
        return temp.ToList();
    }

    void NeighbourDistanceCheck(GridData aSuccessor, GridData q, List<GridData> aList)
    {
        if (aSuccessor.myState == TileState.Closed)
        {
            return;
        }

        aSuccessor.myState = TileState.Open;
        float newG = q.G + Manhattan(q, aSuccessor);
        if (newG < aSuccessor.G)
        {
            aSuccessor.G = newG;
            aSuccessor.Parent = q;
        }
    }

    List<GridData> Pathfind(Vector3Int Start, Vector3Int Target)
    {
        List<GridData> open = new List<GridData>();
        List<GridData> path = new List<GridData>();

        GridData[,,] usableData = new GridData[myData.gridSize.x, myData.gridSize.y, myData.gridSize.z];

        for (int x = 0; x < myData.gridSize.x; x++)
        {
            for (int y = 0; y < myData.gridSize.y; y++)
            {
                for (int z = 0; z < myData.gridSize.z; z++)
                {
                    usableData[x, y, z] = myDungeonSlots[x, y, z];
                }
            }
        }

        GridData startNode = usableData[Start.x, Start.y, Start.z];
        startNode = myDungeonSlots[Start.x, Start.y, Start.z];

        GridData endNode = usableData[Target.x, Target.y, Target.z];
        endNode = myDungeonSlots[Target.x, Target.y, Target.z];

        startNode.G = 0f;

        for (int x = 0; x < myData.gridSize.x; x++)
        {
            for (int y = 0; y < myData.gridSize.y; y++)
            {
                for (int z = 0; z < myData.gridSize.z; z++)
                {
                    open.Add(usableData[x, y, z]);
                }
            }
        }

        bool done = false;

        while (open.Count > 0 && !done)
        {
            GridData q = FindNodeWithLeastF(open, endNode);

            q.myState = TileState.Closed;
            open.Remove(q);

            if (q.WorldPosition == Target)
            {
                GridData current = q;
                while (current != null)
                {
                    path.Add(current);
                    current = current.Parent;
                }
                done = true;
                break;
            }

            if (q.WorldPosition.x > 0)
            {
                NeighbourDistanceCheck(usableData[q.WorldPosition.x - 1, q.WorldPosition.y, q.WorldPosition.z], q, open); //left
            }

            if (q.WorldPosition.x < myData.gridSize.x - 1)
            {
                NeighbourDistanceCheck(usableData[q.WorldPosition.x + 1, q.WorldPosition.y, q.WorldPosition.z], q, open); //right
            }

            if (q.WorldPosition.z > 0)
            {
                NeighbourDistanceCheck(usableData[q.WorldPosition.x, q.WorldPosition.y, q.WorldPosition.z - 1], q, open); //back
            }

            if (q.WorldPosition.z < myData.gridSize.z - 1)
            {
                NeighbourDistanceCheck(usableData[q.WorldPosition.x, q.WorldPosition.y, q.WorldPosition.z + 1], q, open); // front
            }
        }

        return path;
    }

    GridData FindNodeWithLeastF(List<GridData> aList, GridData anEndNode)
    {
        int smallestObjectIndex = 0;
        float smallestCurrentF = float.MaxValue;
        for (int i = 0; i < aList.Count; i++)
        {
            if (smallestCurrentF > aList[i].F(anEndNode))
            {
                smallestCurrentF = aList[i].F(anEndNode);
                smallestObjectIndex = i;
            }
        }

        return aList[smallestObjectIndex];
    }

    // void GenerateCorridor(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    // {
    //     Transform exitParent = aParent.transform.Find("Exits");
    //     List<GameObject> list = new List<GameObject>();
    //     for (int i = 0; i < exitParent.childCount; i++)
    //     {
    //         Transform child = exitParent.GetChild(i);
    //         if (!myDungeonSlots.ContainsKey(RoundToInt(child.transform.position)))
    //         {
    //             GameObject corridor = CreateValidCorridor(child, RoundToInt(child.transform.position));
    //             Room roomComp = corridor.GetComponent<Room>();
    //             roomComp.SetDepth(aCurrentDepth);
    //
    //             if (roomComp.myType == BlockType.Staircase_UP)
    //             {
    //                 myDungeonSlots[RoundToInt(corridor.transform.position) + Vector3Int.up] = corridor;
    //             }
    //             else if (roomComp.myType == BlockType.Staircase_DOWN)
    //             {
    //                 myDungeonSlots[RoundToInt(corridor.transform.position) + Vector3Int.down] = corridor;
    //             }
    //
    //             GenerateBlock(corridor, aCurrentDepth + 1, aMaxDepth);
    //         }
    //     }
    // }

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
        GameObject room = Instantiate(myBlocks[BlockType.LargeRoom][Random.Range(0, myBlocks[BlockType.LargeRoom].Length)], aParent);
        do
        {
            overlapping = false;
            int roomIndex = Random.Range(0, myBlocks[BlockType.LargeRoom].Length);

            Vector3Int position = new Vector3Int(Random.Range(0, myData.gridSize.x), 0, Random.Range(0, myData.gridSize.z));
            if (myData.AllowHeightDifference)
            {
                int height = Random.Range(0, myData.gridSize.y);
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
                Vector3Int tilePos = RoundToInt(tiles.GetChild(i).position);

                if (tilePos.x < 0 || tilePos.y < 0 || tilePos.z < 0)
                {
                    overlapping = true;
                    break;
                }

                if (tilePos.x >= myData.gridSize.x || tilePos.y >= myData.gridSize.y || tilePos.z >= myData.gridSize.z)
                {
                    overlapping = true;
                    break;
                }

                if (myDungeonSlots[tilePos.x, tilePos.y, tilePos.z].myState != TileState.Empty)
                {
                    overlapping = true;
                    break;
                }
            }

            if (overlapping == false)
            {
                for (int i = 0; i < tiles.childCount; i++)
                {
                    Vector3Int tilePos = RoundToInt(tiles.GetChild(i).position);
                    myDungeonSlots[tilePos.x, tilePos.y, tilePos.z] = new GridData();
                    myDungeonSlots[tilePos.x, tilePos.y, tilePos.z].myTile = tiles.GetChild(i).gameObject;

                    if (myDungeonSlots[tilePos.x, tilePos.y, tilePos.z].myTile.name.Contains("Doormat"))
                    {
                        myDungeonSlots[tilePos.x, tilePos.y, tilePos.z].myState = TileState.Door;
                    }
                    else
                    {
                        myDungeonSlots[tilePos.x, tilePos.y, tilePos.z].myState = TileState.Closed;
                    }

                    myDungeonSlots[tilePos.x, tilePos.y, tilePos.z].WorldPosition = tilePos;
                }
                myRooms.Add(room);
            }

        } while (overlapping);
    }

    //void GenerateBlock(GameObject aParent, int aCurrentDepth, int aMaxDepth)
    //{
    //    myDungeonList.Add(aParent);
    //    if (aCurrentDepth > aMaxDepth)
    //    {
    //        GameObject end = CreateValidCorridor(aParent.transform, RoundToInt(aParent.transform.position), true);
    //        myDungeonList.Add(end);
    //
    //        end.transform.parent = end.transform.parent.parent;
    //
    //        DestroyImmediate(aParent);
    //        return;
    //    }
    //
    //    GenerateCorridor(aParent, aCurrentDepth, aMaxDepth);
    //}

    // GameObject CreateValidCorridor(Transform aParent, Vector3Int aPosition, bool aEmptyTileConnectionsAreFail = false)
    // {
    //     GameObject corridor = InstantiateRandom(aParent);
    //     bool isValid = false;
    //     int attempts = 100;
    //
    //
    //     while (!isValid && attempts > 0)
    //     {
    //         attempts--;
    //         DestroyImmediate(corridor);
    //
    //         if (aEmptyTileConnectionsAreFail)
    //         {
    //             corridor = InstantiateRoomOfType(aParent, BlockType.End);
    //             break;
    //         }
    //         else
    //         {
    //             corridor = InstantiateRandom(aParent);
    //         }
    //
    //         List<Vector3Int> possibleNeighbours = new()
    //                 {
    //                     RoundToInt(aParent.position) + Vector3Int.right,
    //                     RoundToInt(aParent.position) + Vector3Int.left,
    //                     RoundToInt(aParent.position) + Vector3Int.forward,
    //                     RoundToInt(aParent.position) + Vector3Int.back,
    //                 };
    //
    //         if (corridor.GetComponent<Room>().myType == BlockType.Staircase_UP || corridor.GetComponent<Room>().myType == BlockType.Staircase_DOWN)
    //         {
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.forward);
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.back);
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.left);
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.up + Vector3Int.right);
    //
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.back);
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.forward);
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.right);
    //             possibleNeighbours.Add(RoundToInt(aParent.transform.position) + Vector3Int.down + Vector3Int.left);
    //         }
    //
    //         Transform myExits = corridor.transform.Find("Exits");
    //         List<Transform> realNeighbours = new List<Transform>();
    //         for (int i = 0; i < possibleNeighbours.Count; i++)
    //         {
    //             if (!myDungeonSlots.ContainsKey(possibleNeighbours[i]))
    //             {
    //                 continue;
    //             }
    //
    //             if (myDungeonSlots[possibleNeighbours[i]] == null)
    //             {
    //                 continue;
    //             }
    //
    //             if (!IsParentTo(myDungeonSlots[possibleNeighbours[i]], corridor))
    //             {
    //                 realNeighbours.Add(myDungeonSlots[possibleNeighbours[i]].transform);
    //             }
    //         }
    //
    //         Debug.Log("Neighbours defined : " + realNeighbours.Count);
    //         int okNeighbours = 0;
    //
    //         for (int i = 0; i < realNeighbours.Count; i++)
    //         {
    //
    //             if (ExitPointsHere(realNeighbours[i].Find("Exits"), RoundToInt(myExits.position)))
    //             {
    //                 okNeighbours++;
    //             }
    //         }
    //
    //         Debug.Log("Neighbour count : " + realNeighbours.Count + "\nokNeighbours : " + okNeighbours);
    //
    //         if (realNeighbours.Count == okNeighbours)
    //         {
    //             isValid = true;
    //             break;
    //         }
    //     }
    //
    //     if (isValid == false)
    //     {
    //         corridor.SetActive(false);
    //     }
    //
    //     myDungeonSlots[aPosition] = corridor.gameObject;
    //     Debug.Log("Generated corridor at position: " + aPosition + "\nattempt : " + (100 - attempts));
    //     return corridor;
    // }

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
        if (myDungeonSlots[aPosition.x, aPosition.y, aPosition.z] == null)
        {
            return BlockType.Default;
        }

        Room room;
        if (myDungeonSlots[aPosition.x, aPosition.y, aPosition.z].myTile.TryGetComponent<Room>(out room))
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
