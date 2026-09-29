using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    public float X = 0f;
    public float Y = 0f;
    public float Z = 0f;
    // Start is called before the first frame update
    void Start()
    {
    
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.Rotate(X, Y, Z);
    }
}
