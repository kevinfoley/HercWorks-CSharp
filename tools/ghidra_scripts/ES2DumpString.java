import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;
import java.io.FileWriter;

// Reads a null-terminated ASCII string at a raw address, regardless of whether Ghidra has it
// defined as a String data type. Useful when a data xref points at a plain byte array that
// wasn't auto-classified as a string.
// args[0] = address (hex), args[1] = max length, args[2] = output path
public class ES2DumpString extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address addr = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int maxLen = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        Memory mem = currentProgram.getMemory();
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < maxLen; i++) {
            byte b = mem.getByte(addr.add(i));
            if (b == 0) break;
            sb.append((char) (b & 0xff));
        }
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            pw.println(sb.toString());
        }
        println("string at " + addr + ": " + sb.toString());
    }
}
