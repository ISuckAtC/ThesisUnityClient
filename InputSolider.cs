using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputSolider : MonoBehaviour
{
    public int id;
    public Vector3 position;

    public void FixedUpdate()
    {
        transform.position = position;
    }

    public void Input(bool[] buttons, Vector2 analog)
    {
        position += new Vector3(analog.x, 0f, 0f);
        if (buttons[0]) position += new Vector3(0f, 5f, 0f);
    }
}
