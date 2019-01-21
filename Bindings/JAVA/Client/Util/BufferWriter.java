package Client.Util;

import java.nio.ByteBuffer;

public class BufferWriter {

    private int offset;
    private byte[] buffer;

    public void Start(int length) {
        buffer = new byte[length];
        offset = 0;
    }

    public byte[] End() {
        return buffer;
    }

    public void Int(int value) {
        buffer[offset + 0] = (byte) value;
        buffer[offset + 1] = (byte) (value >>> 8);
        buffer[offset + 2] = (byte) (value >>> 16);
        buffer[offset + 3] = (byte) (value >>> 24);
        offset += 4;
    }

    public void Short(short value) {
        buffer[offset + 0] = (byte) value;
        buffer[offset + 1] = (byte) (value >>> 8);
        offset += 2;
    }

    public void Byte(byte value) {
        buffer[offset] = value;
        offset++;
    }

    public void Float(float value) {
        byte[] bytes = ByteBuffer.allocate(4).putFloat(value).array();
        for (int i = 0; i < 4; i++) {
            buffer[offset] = bytes[i];
        }
        offset += 4;
    }
}
