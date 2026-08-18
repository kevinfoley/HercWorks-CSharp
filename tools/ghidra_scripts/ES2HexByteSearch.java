import ghidra.app.script.GhidraScript;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import java.io.PrintWriter;
import java.io.FileWriter;

// Scans every initialized memory block for a literal byte pattern given as hex, so non-string data
// (tables, structs) can be located the way ES2RawByteSearch locates ASCII literals.
// args[0] = hex needle, e.g. "2A000600" (whitespace allowed). args[1] = output path.
public class ES2HexByteSearch extends GhidraScript {
    @Override
    public void run() throws Exception {
        String hex = getScriptArgs()[0].replaceAll("\\s+", "");
        String outPath = getScriptArgs()[1];

        if (hex.length() % 2 != 0) {
            println("Hex needle must have an even number of digits: " + hex);
            return;
        }

        byte[] pat = new byte[hex.length() / 2];
        for (int i = 0; i < pat.length; i++) {
            pat[i] = (byte) Integer.parseInt(hex.substring(i * 2, i * 2 + 2), 16);
        }

        Memory mem = currentProgram.getMemory();
        int hits = 0;
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            pw.println("pattern " + hex + " (" + pat.length + " bytes)");
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
                        if (data[i + j] != pat[j]) { match = false; break; }
                    }
                    if (match) {
                        pw.println(block.getStart().add(i) + " in block " + block.getName());
                        hits++;
                    }
                }
            }
            pw.println("total hits: " + hits);
        }
        println("Wrote " + hits + " hit(s) to " + outPath);
    }
}
