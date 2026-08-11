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

// args[0] = spec file path. Spec file: line 1 = output path, remaining lines = function names (one per line).
public class ES2DecompileNamed extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        String outPath;
        List<String> names = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            outPath = br.readLine();
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (!line.isEmpty()) names.add(line);
            }
        }

        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (String name : names) {
                for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
                    if (f.getName().equals(name)) {
                        DecompileResults res = decomp.decompileFunction(f, 60, new ConsoleTaskMonitor());
                        pw.println("=== " + name + " @ " + f.getEntryPoint() + " ===");
                        if (res != null && res.decompileCompleted()) {
                            pw.println(res.getDecompiledFunction().getC());
                        } else {
                            pw.println("decompile failed");
                        }
                    }
                }
            }
        }
        decomp.dispose();
        println("wrote decompiled functions to " + outPath);
    }
}
