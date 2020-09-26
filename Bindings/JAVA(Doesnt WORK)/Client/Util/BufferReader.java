package Client.Util;

public class BufferReader {

    private int offset;
    private byte[] buffer;

    public void Start(byte[] b) {
        offset = 0;
        buffer = b;
    }

    public int Int() {
        int ret = (int) (buffer[0 + offset] & 0xFF)
                | (int) ((buffer[1 + offset] & 0xFF) << 8)
                | (int) ((buffer[2 + offset] & 0xFF) << 16)
                | (int) ((buffer[3 + offset] & 0xFF) << 24);
        offset += 4;
        return ret;
    }

    public short Short() {
        short ret = (short) ((short) (buffer[0 + offset] & 0xFF) | (short) ((buffer[1 + offset] & 0xFF) << 8));
        offset += 2;
        return ret;
    }

    public byte Byte() {
        byte b = buffer[offset];
        offset++;
        return b;
    }

    public int IntFromByte() {
        return (int) (Byte()) & 0xFF;
    }

    public boolean Bool() {
        return Byte() != 0;
    }

    public float Float() {
        return Float.intBitsToFloat(Int());
    }

    public long Long() {
        long ret = (long) (buffer[0 + offset] & 0xFF)
                | ((long) ((buffer[1 + offset] & 0xFF)) << 8)
                | ((long) ((buffer[2 + offset] & 0xFF)) << 16)
                | ((long) ((buffer[3 + offset] & 0xFF)) << 24)
                | ((long) ((buffer[4 + offset] & 0xFF)) << 32)
                | ((long) ((buffer[5 + offset] & 0xFF)) << 40)
                | ((long) ((buffer[6 + offset] & 0xFF)) << 48)
                | ((long) ((buffer[7 + offset] & 0xFF)) << 56);
        offset += 8;
        return ret;
    }

    public double Double() {
        return Double.longBitsToDouble(Long());
    }
}
