import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;
import java.io.FileWriter;
import java.io.BufferedReader;
import java.io.FileReader;

// Batch version of ES2DumpString: reads several null-terminated ASCII strings in one run.
// args[0] = spec file path. Line 1 = output path. Remaining lines = "<addressHex> <maxLen>" or
// "P<addressHex> <maxLen>" where the P prefix means: read a 4-byte little-endian pointer at
// addressHex first, then dump the string at THAT dereferenced address instead.
public class ES2DumpStrings extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        Memory mem = currentProgram.getMemory();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            String outPath = br.readLine().trim();
            try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
                String line;
                while ((line = br.readLine()) != null) {
                    line = line.trim();
                    if (line.isEmpty()) continue;
                    String[] parts = line.split("\\s+");
                    String addrSpec = parts[0];
                    boolean deref = addrSpec.startsWith("P");
                    if (deref) addrSpec = addrSpec.substring(1);
                    Address addr = currentProgram.getAddressFactory().getAddress(addrSpec);
                    int maxLen = Integer.parseInt(parts[1]);
                    if (deref) {
                        int ptrVal = mem.getInt(addr);
                        pw.println(parts[0] + " -> ptr 0x" + Integer.toHexString(ptrVal));
                        addr = currentProgram.getAddressFactory().getAddress(Integer.toHexString(ptrVal));
                    }
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < maxLen; i++) {
                        byte b = mem.getByte(addr.add(i));
                        if (b == 0) break;
                        sb.append((char) (b & 0xff));
                    }
                    pw.println(parts[0] + ": " + sb.toString());
                }
                println("wrote strings to " + outPath);
            }
        }
    }
}
