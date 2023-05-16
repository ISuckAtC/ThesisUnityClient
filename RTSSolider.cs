using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTSSolider : MonoBehaviour
{
    public int id;
    public bool walking;
    public Vector2 target;
    public float speed;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (walking)
        {
            Vector2 current = new Vector2(transform.position.x, transform.position.z);
            Vector2 move = Vector2.MoveTowards(current, target, speed);
            if (Vector2.Distance(target, move) < 0.1f) walking = false;
            transform.position = new Vector3(move.x, 0, move.y);
        }
    }

    public void SetTarget(Vector2 target)
    {
        if (this.target == target) return;
        walking = true;
        this.target = target;
    }
}
