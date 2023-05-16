using System;

public struct UpdatePacket : Packet
{
    private int id;
    public UnityEngine.Vector3 position;

    public int Id 
    { 
        get {return id;} 
        set {id = value;}
    }

    public static UpdatePacket Deserialize(byte[] bytes)
    {
        UpdatePacket packet;

        packet.id = BitConverter.ToInt32(bytes, 0);
        float posX = BitConverter.ToSingle(bytes, 4);
        float posY = BitConverter.ToSingle(bytes, 8);
        float posZ = BitConverter.ToSingle(bytes, 12);

        packet.position = new UnityEngine.Vector3(posX, posY, posZ);

        return packet;
    }

    public byte[] Serialize()
    {
        byte[] serialized = new byte[16];
        BitConverter.GetBytes(id).CopyTo(serialized, 0);

        BitConverter.GetBytes(position.x).CopyTo(serialized, 4);
        BitConverter.GetBytes(position.y).CopyTo(serialized, 8);
        BitConverter.GetBytes(position.z).CopyTo(serialized, 12);

        return serialized;
    }
}
