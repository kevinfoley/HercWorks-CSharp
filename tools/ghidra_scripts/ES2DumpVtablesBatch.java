import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.Memory;
import java.io.BufferedReader;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.PrintWriter;

// args[0] = spec file path. Spec file: line 1 = output path, remaining lines = "label addrHex slotCount".
public class ES2DumpVtablesBatch extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        String outPath;
        java.util.List<String[]> jobs = new java.util.ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            outPath = br.readLine();
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (!line.isEmpty()) jobs.add(line.split("\\s+"));
            }
        }
        Memory mem = currentProgram.getMemory();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (String[] job : jobs) {
                String label = job[0];
                Address base = currentProgram.getAddressFactory().getAddress(job[1]);
                int count = Integer.parseInt(job[2]);
                pw.println("=== " + label + " @ " + base + " ===");
                for (int i = 0; i < count; i++) {
                    Address slotAddr = base.add((long) i * 4);
                    long raw;
                    try {
                        raw = mem.getInt(slotAddr) & 0xFFFFFFFFL;
                    } catch (Exception e) {
                        pw.println(String.format("  +0x%x (%s): <unreadable>", i * 4, slotAddr));
                        continue;
                    }
                    Address target = currentProgram.getAddressFactory().getAddress(Long.toHexString(raw));
                    Function f = currentProgram.getFunctionManager().getFunctionAt(target);
                    String fname = (f != null) ? f.getName() : "(no function at " + target + ")";
                    pw.println(String.format("  +0x%x (%s): -> %s @ %s", i * 4, slotAddr, fname, target));
                }
                pw.println();
            }
        }
        println("wrote batch vtable dump to " + outPath);
    }
}
