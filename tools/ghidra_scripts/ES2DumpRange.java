import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;
import java.io.FileWriter;

// Dumps a raw byte range as hex + printable-ASCII, 16 bytes/line, regardless of Ghidra's data
// typing. args[0] = start address (hex), args[1] = length, args[2] = output path.
public class ES2DumpRange extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address start = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int len = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        Memory mem = currentProgram.getMemory();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (int row = 0; row < len; row += 16) {
                Address a = start.add(row);
                StringBuilder hex = new StringBuilder();
                StringBuilder ascii = new StringBuilder();
                for (int i = 0; i < 16 && row + i < len; i++) {
                    byte b;
                    try { b = mem.getByte(a.add(i)); } catch (Exception e) { b = 0; }
                    hex.append(String.format("%02X ", b & 0xff));
                    char c = (char) (b & 0xff);
                    ascii.append((c >= 32 && c < 127) ? c : '.');
                }
                pw.println(a + "  " + hex + " " + ascii);
            }
        }
        println("wrote range dump to " + outPath);
    }
}
