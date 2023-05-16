using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateSolider : MonoBehaviour
{
    public int id;
    public Vector3 position;

    public void FixedUpdate()
    {
        transform.position = position;
    }
}
