import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import java.io.PrintWriter;
import java.io.FileWriter;

// Scans every initialized memory block for a raw little-endian 4-byte value (e.g. a pointer
// literal), regardless of whether Ghidra classified it as data or built a Reference for it.
// args[0] = hex value (e.g. 0049a46b), args[1] = output path.
public class ES2FindDwordValue extends GhidraScript {
    @Override
    public void run() throws Exception {
        long value = Long.parseLong(getScriptArgs()[0], 16);
        String outPath = getScriptArgs()[1];
        byte[] pat = new byte[] {
            (byte) (value & 0xff), (byte) ((value >> 8) & 0xff),
            (byte) ((value >> 16) & 0xff), (byte) ((value >> 24) & 0xff)
        };
        Memory mem = currentProgram.getMemory();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (MemoryBlock block : mem.getBlocks()) {
                if (!block.isInitialized()) continue;
                byte[] data;
                try {
                    data = new byte[(int) block.getSize()];
                    block.getBytes(block.getStart(), data);
                } catch (Exception e) { continue; }
                for (int i = 0; i + 4 <= data.length; i++) {
                    if (data[i] == pat[0] && data[i+1] == pat[1] && data[i+2] == pat[2] && data[i+3] == pat[3]) {
                        Address a = block.getStart().add(i);
                        pw.println(a + " in block " + block.getName());
                    }
                }
            }
        }
        println("wrote dword search to " + outPath);
    }
}
