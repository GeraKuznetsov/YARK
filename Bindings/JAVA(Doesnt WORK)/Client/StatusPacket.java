package Client;

import Client.Util.BufferReader;

public class StatusPacket {

    public static final int LENGTH = 37;

    private int ID;
    public boolean inFlight;
    public String vesselName;

    BufferReader b = new BufferReader();

    public StatusPacket() {
        vesselName = "";
    }

    public StatusPacket(byte[] source) {
        b.Start(source);
        ID = b.Int();
        inFlight = b.Bool();
        char[] name = new char[32];
        for (int i = 0; i < 32; i++) {
            name[i] = (char) b.Byte();
        }
        vesselName = new String(name);
    }

    public int getID() {
        return ID;
    }

}
