import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.util.task.ConsoleTaskMonitor;
import java.io.BufferedReader;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.List;

// Decompiles every non-thunk, non-external function and greps the decompiled C text for
// literal substrings (e.g. "0x222" for a struct offset). Use to find code that touches a
// known field offset when no data-address xref exists (offsets are relative to a register
// argument, not a fixed address, so ES2FindAddressRefs doesn't apply).
//
// args[0] = spec file path. Spec file: line 1 = output path, remaining lines = substrings to search for (one per line).
public class ES2FindImmediateRefs extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        String outPath;
        List<String> needles = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            outPath = br.readLine();
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (!line.isEmpty()) needles.add(line);
            }
        }

        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        ConsoleTaskMonitor tm = new ConsoleTaskMonitor();

        int scanned = 0, hits = 0;
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
                if (f.isThunk() || f.isExternal()) continue;
                scanned++;
                DecompileResults res = decomp.decompileFunction(f, 30, tm);
                if (res == null || !res.decompileCompleted()) continue;
                String c = res.getDecompiledFunction().getC();
                boolean matched = false;
                StringBuilder sb = new StringBuilder();
                for (String ln : c.split("\n")) {
                    for (String needle : needles) {
                        if (ln.contains(needle)) {
                            sb.append("    ").append(ln.trim()).append("\n");
                            matched = true;
                            break;
                        }
                    }
                }
                if (matched) {
                    hits++;
                    pw.println("=== " + f.getName() + " @ " + f.getEntryPoint() + " (size " + f.getBody().getNumAddresses() + ") ===");
                    pw.print(sb.toString());
                }
            }
        }
        decomp.dispose();
        println("scanned " + scanned + " functions, " + hits + " matched, wrote to " + outPath);
    }
}
