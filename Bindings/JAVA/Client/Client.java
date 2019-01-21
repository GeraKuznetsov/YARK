package Client;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;

import Client.Util.Header;
import static Client.Util.Checksum.*;

public class Client {

    // defines for MainControls
    public static final int MC_SAS = (1 << 0);
    public static final int MC_RCS = (1 << 1);
    public static final int MC_LIGHTS = (1 << 2);
    public static final int MC_GEAR = (1 << 3);
    public static final int MC_BRAKES = (1 << 4);
    public static final int MC_ABORT = (1 << 5);
    public static final int MC_STAGE = (1 << 6);

    // Action group flags
    public static final int AG_1 = (1 << 0);
    public static final int AG_2 = (1 << 1);
    public static final int AG_3 = (1 << 2);
    public static final int AG_4 = (1 << 3);
    public static final int AG_5 = (1 << 4);
    public static final int AG_6 = (1 << 5);
    public static final int AG_7 = (1 << 6);
    public static final int AG_8 = (1 << 7);
    public static final int AG_9 = (1 << 8);
    public static final int AG_10 = (1 << 9);

    // SAS mode definitions
    public static final int SAS_HOLD = 1;
    public static final int SAS_PROGRADE = 2;
    public static final int SAS_RETROGRADE = 3;
    public static final int SAS_NORMAL = 4;
    public static final int SAS_ANTINORMAL = 5;
    public static final int SAS_RADIN = 6;
    public static final int SAS_RADOUT = 7;
    public static final int SAS_TARGET = 8;
    public static final int SAS_ANTITARGET = 9;
    public static final int SAS_MAN = 10;
    public static final int SAS_HOLD_VECTOR = 11;

    // Timewarp mode definitions
    public static final int TIMEWARP_x1 = 0;
    public static final int TIMEWARP_x2p = 1;
    public static final int TIMEWARP_x3p = 2;
    public static final int TIMEWARP_x4p = 3;
    public static final int TIMEWARP_x5 = 4;
    public static final int TIMEWARP_x10 = 5;
    public static final int TIMEWARP_x50 = 6;
    public static final int TIMEWARP_x100 = 7;
    public static final int TIMEWARP_x1000 = 8;
    public static final int TIMEWARP_x10000 = 9;
    public static final int TIMEWARP_x100000 = 10;

    // For enableing / disableing axis input
    public static final int CONTROLLER_ROT = 0;
    public static final int CONTROLLER_TRANS = 1;
    public static final int CONTROLLER_THROTTLE = 2;
    public static final int CONTROLLER_WHEEL = 3;

    // Always uses internal KSP value, ignoring client value
    public static final int AXIS_IGNORE = 0;
    // Client always used, overrides KSP value
    public static final int AXIS_OVERIDE = 1;
    // Client value is used if the internal KSP value is zero, otherwise
    // internal KSP value is used (KSP internal value overrides client value)
    public static final int AXIS_INT_NZ = 2;
    // internal KSP value is used if the client value is zero, otherwise client
    // value is sent (Client value overrides KSP internal value)
    public static final int AXIS_EXT_NZ = 3;

    public static final int TCP_CONNECTING = 0;
    public static final int TCP_FAILED = 1;
    public static final int TCP_CONNECTED = 2;

    public static final byte[] HEADER_ARRAY = {(byte) 0xFF, (byte) 0xC4,
        (byte) 'Y', (byte) 'A', (byte) 'R', (byte) 'K', (byte) 0x00,
        (byte) 0xFF};

    private OutputStream out;
    private InputStream in;
    private Socket ConnectSocket;
    private boolean Running;

    // IO packets
    public StatusPacket Status = new StatusPacket();
    public VesselPacket Vessel = new VesselPacket();
    public ControlPacket Control = new ControlPacket();

    // Needs to be volatile or else java will optimize out GetState() loops
    private volatile int state = TCP_CONNECTING;

    public String error;

    // connection Methods
    public Client() {
        Status = new StatusPacket();
    }

    public void Connect(String IP, int PORT) {
        new Thread(() -> {
            ConnectRun(IP, PORT);
        }).start();
    }

    public void WaitForConnection() {
        while (GetState() == TCP_CONNECTING) {// wait for connection
            try {
                Thread.sleep(50);
            } catch (InterruptedException e) {
            }
        }
    }

    private byte[] ReadBytes(int bytesToRead) throws IOException {
        byte[] buffer = new byte[bytesToRead];
        int bytesRead = 0;
        while (bytesRead < bytesToRead) {
            int result = in.read(buffer, bytesRead, bytesToRead - bytesRead);
            if (result > 0) {
                bytesRead += result;
            }
        }
        return buffer;
    }

    private void ConnectRun(String IP, int PORT) {
        try {
            ConnectSocket = new Socket(IP, PORT);
            in = ConnectSocket.getInputStream();
            out = ConnectSocket.getOutputStream();
        } catch (IOException e) {
            state = TCP_FAILED;
            error = e.getMessage();
            return;
        }
        state = TCP_CONNECTED;
        Running = true;
        StatusPacket sP;// = new StatusPacket();
        VesselPacket vP;// = new VesselPacket();
        Header header = new Header();

        byte[] recvSP;
        byte[] recvVP;

        try {
            while (Running && state == TCP_CONNECTED) {
                header.ParseFrom(ReadBytes(Header.LENGTH));
                if (header.CheckHeader()) {
                    if (header.type == 1) {
                        sP = new StatusPacket(recvSP = ReadBytes(StatusPacket.LENGTH));
                        if (checksum(recvSP) == header.checksum) {
                            if (sP.getID() > Status.getID()) {
                                Status = sP;
                            }
                        }
                    } else if (header.type == 2) {
                        vP = new VesselPacket(
                                recvVP = ReadBytes(VesselPacket.LENGTH));
                        if (checksum(recvVP) == header.checksum) {
                            if (vP.getID() > Vessel.getID()) {
                                Vessel = vP;
                            }
                        }
                    } else {
                        errBadPacket();
                    }
                } else {
                    errBadPacket();
                }
            }
        } catch (IOException e) {
            e.printStackTrace();
            error = e.getMessage();
        }
        if (Running) {
            Shutdown();
        }
    }

    private void errBadPacket() {
        error = "Malformed Packet";
        state = TCP_FAILED;
        Running = false;
    }

    public int GetState() {
        return state;
    }

    public boolean Connected() {
        return state == TCP_CONNECTED;
    }

    public void SendControls() {
        try {
            out.write(Control.getBytes(), 0, ControlPacket.LENGTH);
        } catch (IOException e) {
            Running = false;
            state = TCP_FAILED;
            error = e.getMessage();
            e.printStackTrace();
        }
    }

    public void Shutdown() {
        state = TCP_FAILED;
        Running = false;
        if (ConnectSocket != null) {
            try {
                ConnectSocket.close();
            } catch (IOException e) {
                // TODO Auto-generated catch block
                e.printStackTrace();
            }
        }
    }
}
