using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Room : MonoBehaviour
{

    public BlockType myType;

    [SerializeField]
    Vector3 myWorldPos;

    [SerializeField]
    int myDepth;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetWorldPos(Vector3 aPosition)
    {
        myWorldPos = aPosition;
    }

    public void SetDepth(int aDepth)
    {
        myDepth = aDepth;
    }
}
