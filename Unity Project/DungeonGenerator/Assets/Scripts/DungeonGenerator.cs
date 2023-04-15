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
    public int myLargeRooms = 1;
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
}

public enum TileState
{
    Open, Closed, Empty,
    Passable,
    Door,
    Entry,
    Stairs
}

enum Directions
{
    North, East, South, West,
    Down,
    Up
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
    GameObject[] myRooms;

    public List<GameObject> GenerateDungeon(DGenerate_InData aData)
    {
        Debug.Log("Starting Dungeon Generation");
        myData = aData;
        myDungeonSlots = new GridData[aData.gridSize.x, aData.gridSize.y, aData.gridSize.z];
        myDungeonList = new List<GameObject>();
        myRooms = new GameObject[aData.myLargeRooms];

        InitializeBuildingBlocks();
        GameObject dungeonParent = InitializeParent();

        if (myData.Seed != 0)
        {
            Random.InitState(myData.Seed);
        }
        else
        {
#pragma warning disable CS0618 // Type or member is obsolete
            aData.Seed = Random.seed;
#pragma warning restore CS0618 // Type or member is obsolete
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

        List<Vector3Int> allPaths = PathfindAllDoors(allDoors);

        GameObject[] entryPoints = GameObject.FindGameObjectsWithTag("Entry");
        foreach (GameObject entry in entryPoints)
        {
            GridData entryData = new GridData();
            entryData.myTile = entry;
            entryData.myState = TileState.Entry;
            entryData.WorldPosition = RoundToInt(entry.transform.position);

            myDungeonSlots[entryData.WorldPosition.x, entryData.WorldPosition.y, entryData.WorldPosition.z] = entryData;
            allPaths.Add(entryData.WorldPosition);
        }

        PopulatePaths(allPaths, dungeonParent, entryPoints);

        Debug.Log("Finished Generating Dungeon.");
        return myDungeonList;
    }

    public static float Manhattan(GridData start, GridData end)
    {
        return Mathf.Abs(start.WorldPosition.x - end.WorldPosition.x) + Mathf.Abs(start.WorldPosition.y - end.WorldPosition.y) + Mathf.Abs(start.WorldPosition.z - end.WorldPosition.z);
    }

    void SetupConnections(GameObject[] someDoors)
    {
        foreach (GameObject door in someDoors)
        {
            Doormat og_doormat = door.GetComponent<Doormat>();
            Doormat linkTarget = og_doormat;
            int og_ID = door.gameObject.GetInstanceID();
            int linkID = linkTarget.gameObject.GetInstanceID();

            bool allowMultiConnections = false;
            while (linkID == og_ID)
            {
                float distance = float.MaxValue;

                if (allowMultiConnections)
                {
                    distance = 0f;
                }

                foreach (GameObject otherDoor in someDoors)
                {
                    Doormat ot_doormat = otherDoor.GetComponent<Doormat>();
                    if ((otherDoor.gameObject.GetInstanceID() == door.gameObject.GetInstanceID()))
                    {
                        continue;
                    }

                    if (DoorsAreInSameRoom(og_doormat, ot_doormat) && myData.myLargeRooms != 1)
                    {
                        continue;
                    }

                    if (allowMultiConnections == false && ot_doormat.connections.Count > 0)
                    {
                        continue;
                    }

                    float newdistance = Vector3.Distance(door.transform.position, otherDoor.transform.position);

                    if ((allowMultiConnections && distance < newdistance) || (!allowMultiConnections && distance > newdistance))
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

    void PopulatePaths(List<Vector3Int> allPaths, GameObject dungeonParent, GameObject[] entryPoints)
    {
        foreach (Vector3Int position in allPaths)
        {
            GridData data = myDungeonSlots[position.x, position.y, position.z];
            if (data.myTile != null)
            {
                Doormat doormat;
                if (data.myTile.TryGetComponent<Doormat>(out doormat))
                {
                    data.myTile = PlaceCorridor(data.WorldPosition, dungeonParent, allPaths);
                    data.myTile.transform.position = data.WorldPosition;
                    data.myState = TileState.Door;
                }
            }
        }

        #region cleansing
        List<Vector3Int> removes = new List<Vector3Int>();
        for (int i = 0; i < allPaths.Count; i++)
        {
            GridData data = myDungeonSlots[allPaths[i].x, allPaths[i].y, allPaths[i].z];
            if (data.myState == TileState.Entry)
            {
                removes.Add(allPaths[i]);
            }
        }

        foreach (Vector3Int index in removes)
        {
            allPaths.Remove(index);
        }
        #endregion

        foreach (Vector3Int position in allPaths)
        {
            GridData data = myDungeonSlots[position.x, position.y, position.z];
            if (data.myState == TileState.Door)
            {
                continue;
            }

            data.myTile = PlaceCorridor(position, dungeonParent, allPaths);
            data.myTile.transform.position = data.WorldPosition;
            data.myTile.GetComponent<Room>().SetWorldPos(data.WorldPosition);
            myDungeonList.Add(data.myTile);
        }
    }

    GameObject PlaceCorridor(Vector3Int aPosition, GameObject dungeonParent, List<Vector3Int> allPaths)
    {
        Dictionary<Directions, GridData> neighbours = new Dictionary<Directions, GridData>();

        if (aPosition.x > 0)
        {
            if (allPaths.Contains(new Vector3Int(aPosition.x - 1, aPosition.y, aPosition.z)))
            {
                neighbours.Add(Directions.West, myDungeonSlots[aPosition.x - 1, aPosition.y, aPosition.z]);
            }
        }

        if (aPosition.x < myData.gridSize.x - 1)
        {
            if (allPaths.Contains(new Vector3Int(aPosition.x + 1, aPosition.y, aPosition.z)))
            {
                neighbours.Add(Directions.East, myDungeonSlots[aPosition.x + 1, aPosition.y, aPosition.z]);
            }
        }

        if (aPosition.z > 0)
        {
            if (allPaths.Contains(new Vector3Int(aPosition.x, aPosition.y, aPosition.z - 1)))
            {
                neighbours.Add(Directions.South, myDungeonSlots[aPosition.x, aPosition.y, aPosition.z - 1]);
            }
        }

        if (aPosition.z < myData.gridSize.z - 1)
        {
            if (allPaths.Contains(new Vector3Int(aPosition.x, aPosition.y, aPosition.z + 1)))
            {
                neighbours.Add(Directions.North, myDungeonSlots[aPosition.x, aPosition.y, aPosition.z + 1]);
            }
        }

        if (myDungeonSlots[aPosition.x, aPosition.y, aPosition.z].myState == TileState.Stairs)
        {
            if (aPosition.y > 0)
            {
                if (allPaths.Contains(new Vector3Int(aPosition.x, aPosition.y - 1, aPosition.z)))
                {
                    neighbours.Add(Directions.Down, myDungeonSlots[aPosition.x, aPosition.y - 1, aPosition.z]);
                }
            }

            if (aPosition.y < myData.gridSize.y - 1)
            {
                if (allPaths.Contains(new Vector3Int(aPosition.x, aPosition.y + 1, aPosition.z)))
                {
                    neighbours.Add(Directions.Up, myDungeonSlots[aPosition.x, aPosition.y + 1, aPosition.z]);
                }
            }
        }

        int neighbourCount = neighbours.Count;
        Vector3 rotation = Vector3.zero;

        GameObject result;
        BlockType type = BlockType.Entry;
        switch (neighbourCount)
        {
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
                else if (neighbours.ContainsKey(Directions.South))
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

            case 5:

                if (neighbours.ContainsKey(Directions.North) && neighbours.ContainsKey(Directions.South) && neighbours.ContainsKey(Directions.West) && neighbours.ContainsKey(Directions.East))
                {
                    type = BlockType.Staircase_DOWN;
                }

                break;
            default:
                break;
        }

        if (myDungeonSlots[aPosition.x, aPosition.y, aPosition.z].myState == TileState.Stairs)
        {
            type = BlockType.Staircase_UP;
        }

        result = InstantiateRoomOfType(dungeonParent.transform, type);
        result.transform.rotation = Quaternion.Euler(rotation);
        return result;
    }

    List<Vector3Int> PathfindAllDoors(GameObject[] allDoors)
    {
        List<Vector3Int> result = new List<Vector3Int>();

        foreach (GameObject door in allDoors)
        {
            Doormat doormat = door.GetComponent<Doormat>();
            foreach (Doormat connection in doormat.connections)
            {
                Vector3Int startPos = RoundToInt(door.transform.position);
                Vector3Int endPos = RoundToInt(connection.gameObject.transform.position);
                GridData[,,] usableData;
                List<GridData> path = Pathfind(startPos, endPos, out usableData);

                foreach (GridData data in path)
                {
                    result.Add(data.WorldPosition);
                }
            }
        }

        result = FilterDoubles<Vector3Int>(result);
        return result;
    }

    List<T> FilterDoubles<T>(List<T> aList)
    {
        HashSet<T> temp = new HashSet<T>(aList);
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

    List<GridData> Pathfind(Vector3Int Start, Vector3Int Target, out GridData[,,] usableData)
    {
        List<GridData> open = new List<GridData>();
        List<GridData> path = new List<GridData>();
        usableData = new GridData[myData.gridSize.x, myData.gridSize.y, myData.gridSize.z];

        for (int x = 0; x < myData.gridSize.x; x++)
        {
            for (int y = 0; y < myData.gridSize.y; y++)
            {
                for (int z = 0; z < myData.gridSize.z; z++)
                {
                    usableData[x, y, z] = new GridData();
                    usableData[x, y, z].myState = myDungeonSlots[x, y, z].myState;
                    usableData[x, y, z].WorldPosition = myDungeonSlots[x, y, z].WorldPosition;
                }
            }
        }

        GridData startNode = usableData[Start.x, Start.y, Start.z];
        GridData endNode = usableData[Target.x, Target.y, Target.z];

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
                    if (current.Parent != null)
                    {
                        if (current.Parent.WorldPosition == current.WorldPosition + Vector3Int.down || current.Parent.WorldPosition == current.WorldPosition + Vector3Int.up)
                        {
                            myDungeonSlots[current.WorldPosition.x, current.WorldPosition.y, current.WorldPosition.z].myState = TileState.Stairs;
                            myDungeonSlots[current.Parent.WorldPosition.x, current.Parent.WorldPosition.y, current.Parent.WorldPosition.z].myState = TileState.Stairs;
                        }
                    }

                    path.Add(current);
                    current = current.Parent;
                }
                done = true;
                break;
            }

            #region neighbour checks
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

            if (q.WorldPosition.y > 0)
            {
                NeighbourDistanceCheck(usableData[q.WorldPosition.x, q.WorldPosition.y - 1, q.WorldPosition.z], q, open); //down
            }

            if (q.WorldPosition.y > myData.gridSize.y - 1)
            {
                NeighbourDistanceCheck(usableData[q.WorldPosition.x, q.WorldPosition.y + 1, q.WorldPosition.z], q, open); //up
            }
            #endregion
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

    GameObject InstantiateRoomOfType(Transform aTransform, BlockType aType)
    {
        GameObject gameObject;
        gameObject = Instantiate(myBlocks[aType][0], aTransform);
        gameObject.transform.position = RoundToInt(gameObject.transform.position);
        Room room = gameObject.AddComponent<Room>();
        room.myType = aType;
        room.SetWorldPos(RoundToInt(gameObject.transform.position));
        return gameObject;
    }

    public Vector3Int RoundToInt(Vector3 aVector)
    {
        return new Vector3Int(
            Mathf.RoundToInt(aVector.x),
            Mathf.RoundToInt(aVector.y),
            Mathf.RoundToInt(aVector.z)
            );
    }

    void GenerateRoom(Transform aParent)
    {
        bool overlapping = true;
        GameObject room = Instantiate(myBlocks[BlockType.LargeRoom][Random.Range(0, myBlocks[BlockType.LargeRoom].Length)], aParent);
        int index = 0;
        do
        {
            overlapping = false;
            int roomIndex = Random.Range(0, myBlocks[BlockType.LargeRoom].Length);
            Vector3Int position = new Vector3Int(Random.Range(0, myData.gridSize.x), 0, Random.Range(0, myData.gridSize.z));
            int height = Random.Range(0, myData.gridSize.y);
            position.y = height;

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
                myRooms[index] = room;
                index++;
            }

        } while (overlapping);
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
