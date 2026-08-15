import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSetView;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import java.io.PrintWriter;
import java.io.FileWriter;

// Scans every initialized memory block for a literal ASCII needle (case-insensitive), regardless
// of whether Ghidra classified the bytes as a string. args[0] = needle, args[1] = output path.
public class ES2RawByteSearch extends GhidraScript {
    @Override
    public void run() throws Exception {
        String needle = getScriptArgs()[0].toLowerCase();
        String outPath = getScriptArgs()[1];
        byte[] pat = needle.getBytes("US-ASCII");
        Memory mem = currentProgram.getMemory();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (MemoryBlock block : mem.getBlocks()) {
                if (!block.isInitialized()) continue;
                byte[] data;
                try {
                    data = new byte[(int) block.getSize()];
                    block.getBytes(block.getStart(), data);
                } catch (Exception e) { continue; }
                for (int i = 0; i + pat.length <= data.length; i++) {
                    boolean match = true;
                    for (int j = 0; j < pat.length; j++) {
                        byte b = data[i + j];
                        byte lb = (byte) Character.toLowerCase((char) (b & 0xff));
                        if (lb != pat[j]) { match = false; break; }
                    }
                    if (match) {
                        Address a = block.getStart().add(i);
                        pw.println(a + " in block " + block.getName());
                    }
                }
            }
        }
        println("wrote raw byte search to " + outPath);
    }
}
