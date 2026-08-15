import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import java.io.PrintWriter;
import java.io.FileWriter;

// Scans initialized memory for E8 (CALL rel32) opcodes whose computed target equals the given
// address -- finds callers even when Ghidra hasn't disassembled the call site (so no Reference
// object exists yet). args[0] = target address (hex), args[1] = output path.
public class ES2FindCallsTo extends GhidraScript {
    @Override
    public void run() throws Exception {
        long target = Long.parseLong(getScriptArgs()[0], 16);
        String outPath = getScriptArgs()[1];
        Memory mem = currentProgram.getMemory();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (MemoryBlock block : mem.getBlocks()) {
                if (!block.isInitialized() || !block.isExecute()) continue;
                byte[] data;
                long baseOffset = block.getStart().getOffset();
                try {
                    data = new byte[(int) block.getSize()];
                    block.getBytes(block.getStart(), data);
                } catch (Exception e) { continue; }
                for (int i = 0; i + 5 <= data.length; i++) {
                    if ((data[i] & 0xff) != 0xE8) continue;
                    int rel = (data[i+1] & 0xff) | ((data[i+2] & 0xff) << 8)
                            | ((data[i+3] & 0xff) << 16) | ((data[i+4] & 0xff) << 24);
                    long instrAddr = baseOffset + i;
                    long nextAddr = instrAddr + 5;
                    long computedTarget = (nextAddr + rel) & 0xffffffffL;
                    if (computedTarget == target) {
                        Address a = block.getStart().add(i);
                        pw.println(a + " CALL -> " + Long.toHexString(computedTarget) + " in block " + block.getName());
                    }
                }
            }
        }
        println("wrote call search to " + outPath);
    }
}
