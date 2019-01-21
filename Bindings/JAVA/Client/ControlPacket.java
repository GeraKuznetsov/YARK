package Client;

import static Client.Client.*;
import static Client.Util.Checksum.*;
import Client.Util.BufferWriter;
import Client.Util.Header;

public class ControlPacket {

    public static final int LENGTH = 56; // counting header

    private Header header;
    private int ID;
    byte MainControls; // SAS RCS Lights Gear Brakes Abort Stage
    short ActionGroups; // action groups 1-10 in 2 bytes
    // Throttle and axis controls have the following settings:
    // 0: The internal value (supplied by KSP) is always used.
    // 1: The external value (read from serial packet) is always used.
    // 2: If the internal value is not zero use it, otherwise use the external
    // value.
    // 3: If the external value is not zero use it, otherwise use the internal
    // value.
    byte ControlerMode; // DDCCBBAA (2 bits each)
    float SASTol;
    short Pitch; // -1000 -> 1000 //A
    short Roll; // -1000 -> 1000
    short Yaw; // -1000 -> 1000
    short TX; // -1000 -> 1000 //B
    short TY; // -1000 -> 1000
    short TZ; // -1000 -> 1000
    short Throttle; // 0 -> 1000 //C
    short WheelSteer; // -1000 -> 1000 //D
    short WheelThrottle; // 0 -> 1000
    float targetHeading, targetPitch, targetRoll; // E
    byte SASMode; // hold, prograde, retro, etc...
    byte SpeedMode; // Surface, orbit target
    byte timeWarpRateIndex;

    public ControlPacket() {
        ID = 0;
        header = new Header();
        for (int i = 0; i < HEADER_ARRAY.length; i++) {
            header.header[i] = HEADER_ARRAY[i];
        }
        header.type = 1;
    }

    BufferWriter b = new BufferWriter();

    public byte[] getBytes() {
        ID++;
        b.Start(LENGTH - Header.LENGTH);
        b.Int(ID);
        b.Byte(MainControls);
        b.Short(ActionGroups);
        b.Byte(ControlerMode);
        b.Float(SASTol);

        b.Short(Pitch);
        b.Short(Roll);
        b.Short(Yaw);
        b.Short(TX);
        b.Short(TY);
        b.Short(TZ);
        b.Short(Throttle);
        b.Short(WheelSteer);
        b.Short(WheelThrottle);
        b.Float(targetHeading);
        b.Float(targetPitch);
        b.Float(targetRoll);
        b.Byte(SASMode);
        b.Byte(SpeedMode);
        b.Byte(timeWarpRateIndex);

        byte[] data = b.End();

        header.checksum = checksum(data);

        byte[] dataOut = new byte[LENGTH];
        header.WriteHeaderToStartByteBuffer(dataOut);
        System.arraycopy(data, 0, dataOut, Header.LENGTH, LENGTH
                - Header.LENGTH);

        return dataOut;
    }

    // helper methods
    public void SetControlerMode(int controler, int mode) {
        switch (controler) {
            case CONTROLLER_ROT:
                ControlerMode = (byte) (ControlerMode & 0b11111100 | mode << (2 * 0));
                break;
            case CONTROLLER_TRANS:
                ControlerMode = (byte) (ControlerMode & 0b11110011 | mode << (2 * 1));
                break;
            case CONTROLLER_THROTTLE:
                ControlerMode = (byte) (ControlerMode & 0b11001111 | mode << (2 * 2));
                break;
            case CONTROLLER_WHEEL:
                ControlerMode = (byte) (ControlerMode & 0b00111111 | mode << (2 * 3));
                break;
        }
    }

    public void ReSetSASHoldVector() {
        targetHeading = targetPitch = targetRoll = Float.NaN;
    }

    public void SetSASHoldVector(float pitch, float heading, float roll) {
        targetHeading = heading;
        targetPitch = pitch;
        targetRoll = roll;
    }

    public void InputRot(float pitch, float yaw, float roll) {
        Pitch = (short) pitch;
        Roll = (short) roll;
        Yaw = (short) yaw;
    }

    public void InputTran(float tx, float ty, float tz) {
        TX = (short) tx;
        TY = (short) ty;
        TZ = (short) tz;
    }

    public void InputThrottle(float throttle) {
        Throttle = (short) throttle;
    }

    public void SetMainControl(int control, boolean s) {
        if (s) {
            MainControls |= control;
        } else {
            MainControls &= ~((byte) control);
        }
    }

    public void SetActionGroup(int group, boolean s) {
        if (s) {
            ActionGroups |= group;
        } else {
            ActionGroups &= ~((byte) group);
        }
    }
}
