using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateController : MonoBehaviour
{
    public float speed;
    private Rigidbody rb;
    private UpdateSolider solider;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        solider = GetComponent<UpdateSolider>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 axisMovement = (new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))).normalized * speed * Time.deltaTime;

        solider.position += new Vector3(axisMovement.x, 0f, axisMovement.y);
    }
}
