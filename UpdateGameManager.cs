using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateGameManager : MonoBehaviour
{
    public static string debugLatency = "";
    private string prevDebugLatency = "";
    public TMPro.TMP_Text debugLatencyTextField;
    public static string debugMessage = "";
    private string prevDebugMessage = "";
    public TMPro.TMP_Text debugTextField;
    public static Dictionary<int, UpdateSolider> soliders = new Dictionary<int, UpdateSolider>();
    public static Dictionary<int, UpdateSolider> controlledSoliders = new Dictionary<int, UpdateSolider>();
    public GameObject updateSoliderPrefab;
    public GameObject updateSoliderPlayerPrefab;

    public int distanceLatency = 0;

    private bool setup = true;
    // Start is called before the first frame update
    void Start()
    {
        Network.distanceLatency = distanceLatency;
        Network.localTickrate = 33;
        Network.packetType = PacketType.update;
        Network.networkMode = NetworkMode.servercontrol;
        UpdatePacket updatePacket = new UpdatePacket();
        updatePacket.position = new Vector3(0f, 1f, 0f);

        Network.sendPacket = updatePacket;



        Network.Connect();
    }

    void OnPacketRecieve(Packet packet)
    {
        try
        {
            UpdatePacket updatePacket = (UpdatePacket)packet;

            UnityEngine.Debug.Log("Packet recieve, id: " + updatePacket.Id + " | position: " + updatePacket.position);

            

            if (soliders.ContainsKey(updatePacket.Id))
            {
                if (updatePacket.Id != Network.localId) 
                {
                    soliders[updatePacket.Id].position = updatePacket.position;
                }
            }

            if (Network.networkMode == NetworkMode.servercontrol)
            {
                if (controlledSoliders.ContainsKey(updatePacket.Id))
                {
                    controlledSoliders[updatePacket.Id].position = updatePacket.position;
                }
                else
                {
                    UpdateSolider soliderScript = Instantiate(updateSoliderPrefab, updatePacket.position, Quaternion.identity).GetComponent<UpdateSolider>();
                    soliderScript.id = updatePacket.Id;
                    controlledSoliders.Add(updatePacket.Id, soliderScript);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message + "\n\n" + e.StackTrace);
        }
    }

    void Update()
    {
        if (prevDebugMessage != debugMessage)
        {
            prevDebugMessage = debugMessage;
            debugTextField.text = prevDebugMessage;
        }

        if (Network.playerCountChanged)
        {
            if (setup)
            {
                Network.onPacketRecieve += OnPacketRecieve;
                setup = false;
            }
            Network.playerCountChanged = false;

            for (int i = 0; i < soliders.Count; ++i)
            {
                Destroy(soliders[i].gameObject);
            }
            soliders.Clear();

            Debug.Log(Network.playerCount);

            for (int i = 0; i < Network.playerCount; ++i)
            {
                UpdateSolider soliderScript;
                if (Network.localId == i) soliderScript = Instantiate(updateSoliderPlayerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity).GetComponent<UpdateSolider>();
                else soliderScript = Instantiate(updateSoliderPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity).GetComponent<UpdateSolider>();
                soliderScript.position = new Vector3(0f, 1f, 0f);
                soliderScript.id = i;
                soliders.Add(i, soliderScript);
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (setup) return;
        UpdatePacket packet = (UpdatePacket)Network.sendPacket;
        packet.position = soliders[packet.Id].position;
        Network.sendPacket = packet;

        Network.distanceLatency = distanceLatency;

        if (Network.latencyTimes.Count == 60)
        {
            double average = 0;
            for (int i = 0; i < 60; ++i) average += Network.latencyTimes[i];
            average /= 60;
            debugLatencyTextField.text = average.ToString("0.##") + "ms";
        }
    }
}
