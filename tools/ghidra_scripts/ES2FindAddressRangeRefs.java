import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import java.io.PrintWriter;
import java.io.FileWriter;

// Finds every 4-byte little-endian value in the image that falls inside an address RANGE, not just
// those equal to one exact address. Catches base-plus-offset access: code that reaches a field by
// loading the enclosing structure's base and indexing in never mentions the field's own address, so
// an exact-address search reports zero references for data that is in fact written every frame.
//
// args[0] = range low (hex), args[1] = range high (hex, inclusive), args[2] = output path.
public class ES2FindAddressRangeRefs extends GhidraScript {
    @Override
    public void run() throws Exception {
        long lo = Long.parseLong(getScriptArgs()[0], 16);
        long hi = Long.parseLong(getScriptArgs()[1], 16);
        String outPath = getScriptArgs()[2];

        Memory mem = currentProgram.getMemory();
        int hits = 0;

        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            pw.println(String.format("values in [%x..%x]", lo, hi));

            for (MemoryBlock block : mem.getBlocks()) {
                if (!block.isInitialized()) continue;
                byte[] data;
                try {
                    data = new byte[(int) block.getSize()];
                    block.getBytes(block.getStart(), data);
                } catch (Exception e) { continue; }

                for (int i = 0; i + 4 <= data.length; i++) {
                    long v = (data[i] & 0xFFL)
                           | ((data[i + 1] & 0xFFL) << 8)
                           | ((data[i + 2] & 0xFFL) << 16)
                           | ((data[i + 3] & 0xFFL) << 24);
                    if (v < lo || v > hi) continue;

                    Address at = block.getStart().add(i);
                    Function f = currentProgram.getFunctionManager().getFunctionContaining(at);
                    pw.println(String.format("%s -> %x   %s   [%s]",
                        at, v,
                        f != null ? f.getName() + " @ " + f.getEntryPoint() : "(no function)",
                        block.getName()));
                    hits++;
                }
            }
            pw.println("total hits: " + hits);
        }
        println("Wrote " + hits + " hit(s) to " + outPath);
    }
}
