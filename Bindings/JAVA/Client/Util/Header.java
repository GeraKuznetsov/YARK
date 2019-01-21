package Client.Util;

import static Client.Client.HEADER_ARRAY;

public class Header {

    public static final int LENGTH = 11;

    public final byte header[] = new byte[8];
    public short checksum;
    public byte type;

    BufferReader b = new BufferReader();

    public void ParseFrom(byte[] source) {
        b.Start(source);
        for (int i = 0; i < 8; i++) {
            header[i] = b.Byte();
        }
        checksum = b.Short();
        type = b.Byte();
    }

    public void WriteHeaderToStartByteBuffer(byte[] buffer) {
        for (int i = 0; i < header.length; i++) {
            buffer[i] = header[i];
        }
        buffer[header.length + 0] = (byte) checksum;
        buffer[header.length + 1] = (byte) (checksum >>> 8);
        buffer[header.length + 2] = type;
    }

    public boolean CheckHeader() {
        for (int i = 0; i < HEADER_ARRAY.length; i++) {
            if (header[i] != HEADER_ARRAY[i]) {
                return false;
            }
        }
        return true;
    }
}
