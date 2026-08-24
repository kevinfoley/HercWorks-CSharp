import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;
import java.io.FileWriter;

// Dumps an array of pointers-to-ASCII-strings.
// args[0] = array address (hex), args[1] = count, args[2] = output path
public class ES2DumpStringPtrArray extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address base = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int count = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        Memory mem = currentProgram.getMemory();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (int i = 0; i < count; i++) {
                long p = mem.getInt(base.add(i * 4L)) & 0xffffffffL;
                String s;
                try {
                    Address sa = currentProgram.getAddressFactory().getAddress(Long.toHexString(p));
                    StringBuilder sb = new StringBuilder();
                    for (int j = 0; j < 128; j++) {
                        byte b = mem.getByte(sa.add(j));
                        if (b == 0) break;
                        sb.append((char) (b & 0xff));
                    }
                    s = sb.toString();
                } catch (Exception e) {
                    s = "<unreadable>";
                }
                pw.println(String.format("[%d] %08X  \"%s\"", i, p, s));
            }
        }
    }
}
