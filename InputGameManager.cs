using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputGameManager : MonoBehaviour
{
    public static string debugLatency = "";
    private string prevDebugLatency = "";
    public TMPro.TMP_Text debugLatencyTextField;
    public static string debugMessage = "";
    private string prevDebugMessage = "";
    public TMPro.TMP_Text debugTextField;
    public static Dictionary<int, InputSolider> inputSoliders = new Dictionary<int, InputSolider>();
    public static Dictionary<int, UpdateSolider> controlledSoliders = new Dictionary<int, UpdateSolider>();

    public GameObject inputSoliderPrefab;
    public GameObject updateSoliderPrefab;

    public int distanceLatency = 800;

    private bool setup = true;
    // Start is called before the first frame update
    void Start()
    {
        Network.distanceLatency = distanceLatency;
        Network.localTickrate = 33;
        Network.packetType = PacketType.rts;
        Network.networkMode = NetworkMode.serverpass;
        RTSPacket rTSPacket = new RTSPacket();
        rTSPacket.selectedUnits = new bool[5];
        rTSPacket.target = new Vector2(0, 0);
        Network.sendPacket = rTSPacket;



        Network.Connect();
    }

    void OnPacketRecieve(Packet packet)
    {
        try
        {
            if (Network.networkMode == NetworkMode.servercontrol)
            {
                UpdatePacket updatePacket = (UpdatePacket)packet;

                if (!controlledSoliders.ContainsKey(updatePacket.Id))
                {
                    UpdateSolider newSolider = Instantiate(updateSoliderPrefab, updatePacket.position, Quaternion.identity).GetComponent<UpdateSolider>();
                    controlledSoliders.Add(updatePacket.Id, newSolider);
                }

                UpdateSolider solider = controlledSoliders[updatePacket.Id];
                solider.position = updatePacket.position;
            }
            else
            {
                InputPacket inputPacket = (InputPacket)packet;

                InputSolider solider = inputSoliders[inputPacket.Id];
                solider.Input(inputPacket.buttons, inputPacket.analog);
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

        if (Network.networkMode == NetworkMode.servercontrol) return;

        if (Network.playerCountChanged)
        {
            if (setup)
            {
                Network.onPacketRecieve += OnPacketRecieve;
                setup = false;
            }
            Network.playerCountChanged = false;

            for (int i = 0; i < inputSoliders.Count; ++i)
            {
                Destroy(inputSoliders[i]);
            }
            inputSoliders.Clear();

            for (int i = 0; i < Network.playerCount; ++i)
            {
                
                InputSolider soliderScript = Instantiate(inputSoliderPrefab, Vector3.zero, Quaternion.identity).GetComponent<InputSolider>();

                inputSoliders.Add(i, soliderScript);
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
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
