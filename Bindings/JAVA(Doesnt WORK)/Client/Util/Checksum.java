package Client.Util;

public class Checksum {

    public static short checksum(byte[] buffer, int off, int length) {
        int acc = 0;
        for (int i = 0; i < length; i++) {
            acc += (int) (buffer[i + off] & 0xFF);
        }
        return (short) (acc & 0xFFFF);
    }

    public static short checksum(byte[] buffer) {
        return checksum(buffer, 0, buffer.length);
    }
}
