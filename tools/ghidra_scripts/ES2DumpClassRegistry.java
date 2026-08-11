import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;
import java.io.FileWriter;

// args[0] = bucket-count global address (hex), args[1] = bucket-array base address (hex), args[2] = output path
// Walks a ClassItem-style registry: bucket array of (int* entries, int count) pairs (8 bytes each),
// each bucket's entries are 12 bytes: {int id, funcptr loadFn, int extra}.
public class ES2DumpClassRegistry extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address bucketCountAddr = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        Address bucketArrayBase = currentProgram.getAddressFactory().getAddress(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        Memory mem = currentProgram.getMemory();
        int bucketCount = mem.getInt(bucketCountAddr);
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            pw.println("bucketCount=" + bucketCount);
            for (int i = 0; i < bucketCount; i++) {
                Address bucketAddr = bucketArrayBase.add((long) i * 8);
                long ptrRaw = mem.getInt(bucketAddr) & 0xFFFFFFFFL;
                int cnt = mem.getInt(bucketAddr.add(4));
                pw.println(String.format("bucket %d @ %s: entries=%s count=%d", i, bucketAddr, Long.toHexString(ptrRaw), cnt));
                if (ptrRaw == 0) continue;
                Address entriesBase = currentProgram.getAddressFactory().getAddress(Long.toHexString(ptrRaw));
                for (int j = 0; j < cnt; j++) {
                    Address entryAddr = entriesBase.add((long) j * 12);
                    int id;
                    long loadFnRaw;
                    int extra;
                    try {
                        id = mem.getInt(entryAddr);
                        loadFnRaw = mem.getInt(entryAddr.add(4)) & 0xFFFFFFFFL;
                        extra = mem.getInt(entryAddr.add(8));
                    } catch (Exception e) {
                        pw.println(String.format("  [%d] @ %s: <unreadable>", j, entryAddr));
                        continue;
                    }
                    Address loadFnAddr = currentProgram.getAddressFactory().getAddress(Long.toHexString(loadFnRaw));
                    Function f = currentProgram.getFunctionManager().getFunctionAt(loadFnAddr);
                    String fname = (f != null) ? f.getName() : "(no function)";
                    // id often shown elsewhere as a big-endian dword; render both raw hex and byte-swapped ascii-ish view
                    int swapped = Integer.reverseBytes(id);
                    String asciiGuess = bytesToAscii(swapped);
                    pw.println(String.format("  [%d] @ %s: id=0x%08x (be=0x%08x '%s') loadFn=%s @ %s extra=0x%x",
                            j, entryAddr, id, swapped, asciiGuess, fname, loadFnAddr, extra));
                }
            }
        }
        println("wrote class registry dump to " + outPath);
    }

    private String bytesToAscii(int v) {
        StringBuilder sb = new StringBuilder();
        for (int shift = 24; shift >= 0; shift -= 8) {
            int b = (v >> shift) & 0xFF;
            sb.append((b >= 0x20 && b < 0x7F) ? (char) b : '.');
        }
        return sb.toString();
    }
}
