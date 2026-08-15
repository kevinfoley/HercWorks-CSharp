import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.MemoryBlock;
import java.io.PrintWriter;
import java.io.FileWriter;

// Reads a data value straight out of the mapped program image and reports whether the containing
// memory block is initialized (i.e. backed by literal file bytes) or not (BSS/zero-fill), so a
// global's "is this a compile-time constant" question can be answered directly instead of guessed.
// args[0] = address (hex), args[1] = byte count (1/2/4), args[2] = output path
public class ES2ReadDataValue extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address addr = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int len = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            MemoryBlock block = currentProgram.getMemory().getBlock(addr);
            pw.println("address: " + addr);
            if (block != null) {
                pw.println("block: " + block.getName() + " start=" + block.getStart() + " end=" + block.getEnd()
                        + " initialized=" + block.isInitialized() + " writable=" + block.isWrite());
            } else {
                pw.println("block: null (unmapped)");
            }
            byte[] bytes = new byte[len];
            currentProgram.getMemory().getBytes(addr, bytes);
            long value = 0;
            StringBuilder hex = new StringBuilder();
            for (int i = 0; i < len; i++) {
                hex.append(String.format("%02X ", bytes[i] & 0xff));
                value |= ((long) (bytes[i] & 0xff)) << (8 * i);
            }
            pw.println("raw bytes (LE): " + hex.toString().trim());
            pw.println("value (LE, unsigned): " + value + " (0x" + Long.toHexString(value) + ")");
        }
        println("wrote data value to " + outPath);
    }
}
