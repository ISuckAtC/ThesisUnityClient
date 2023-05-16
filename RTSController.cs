using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTSController : MonoBehaviour
{
    bool[] selected;

    public GameObject[] selectedIndicators;

    void Start()
    {
        selected = new bool[5];
        System.Array.Fill<bool>(selected, false);
    }
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray camRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(camRay, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.UseGlobal))
            {
                RTSGameManager.debugMessage = "HIT: " + hit.point.ToString();
                RTSPacket packet = (RTSPacket)Network.sendPacket;
                packet.target = new Vector2(hit.point.x, hit.point.z);
                System.Array.Copy(selected, 0, packet.selectedUnits, 0, 5);
                Network.queuedActions.Add(packet);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            selected[0] = !selected[0];
            selectedIndicators[0].SetActive(selected[0]);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            selected[1] = !selected[1];
            selectedIndicators[1].SetActive(selected[1]);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            selected[2] = !selected[2];
            selectedIndicators[2].SetActive(selected[2]);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            selected[3] = !selected[3];
            selectedIndicators[3].SetActive(selected[3]);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            selected[4] = !selected[4];
            selectedIndicators[4].SetActive(selected[4]);
        }
    }
}
