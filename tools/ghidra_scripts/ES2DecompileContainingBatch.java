import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.util.task.ConsoleTaskMonitor;
import java.io.BufferedReader;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

// Like ES2DecompileContaining, but for several addresses in one program load. Dedupes so a shared
// containing function is only decompiled once even if several addresses land in it.
// args[0] = spec file path. Spec file: line 1 = output path, remaining lines = addresses (hex).
public class ES2DecompileContainingBatch extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        String outPath;
        List<String> addrs = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            outPath = br.readLine();
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (!line.isEmpty()) addrs.add(line);
            }
        }

        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        Set<Function> seen = new LinkedHashSet<>();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (String addrStr : addrs) {
                Address addr = currentProgram.getAddressFactory().getAddress(addrStr);
                Function f = currentProgram.getFunctionManager().getFunctionContaining(addr);
                if (f == null) {
                    pw.println("=== " + addrStr + ": no function contains this address ===");
                    continue;
                }
                if (!seen.add(f)) continue;
                pw.println("=== " + f.getName() + " @ " + f.getEntryPoint() + " (contains " + addrStr + ") ===");
                DecompileResults res = decomp.decompileFunction(f, 60, new ConsoleTaskMonitor());
                if (res != null && res.decompileCompleted()) {
                    pw.println(res.getDecompiledFunction().getC());
                } else {
                    pw.println("decompile failed");
                }
            }
        }
        decomp.dispose();
        println("wrote batch decompile to " + outPath);
    }
}
