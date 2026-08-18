import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;
import java.io.FileWriter;

// Dumps every printable-ASCII run (>= minLen, NUL-terminated) in [lo..hi], with its address.
// args[0]=lo hex, args[1]=hi hex, args[2]=minLen, args[3]=out path
public class ES2ScanStringsRange extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address lo = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        Address hi = currentProgram.getAddressFactory().getAddress(getScriptArgs()[1]);
        int minLen = Integer.parseInt(getScriptArgs()[2]);
        String outPath = getScriptArgs()[3];
        Memory mem = currentProgram.getMemory();
        long len = hi.subtract(lo);
        byte[] data = new byte[(int) len];
        mem.getBytes(lo, data);
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            int start = -1;
            for (int i = 0; i < data.length; i++) {
                int b = data[i] & 0xff;
                boolean printable = b >= 0x20 && b < 0x7f;
                if (printable) {
                    if (start < 0) start = i;
                } else {
                    if (start >= 0 && b == 0 && i - start >= minLen) {
                        pw.println(lo.add(start) + ": " + new String(data, start, i - start, "ISO-8859-1"));
                    }
                    start = -1;
                }
            }
        }
        println("done");
    }
}
