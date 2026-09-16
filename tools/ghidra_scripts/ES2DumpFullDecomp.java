import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileOptions;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.util.task.ConsoleTaskMonitor;
import java.io.BufferedWriter;
import java.io.FileWriter;
import java.io.PrintWriter;

// Full decompilation of every function in the current program.
// args[0] = output path.  args[1] (optional) = per-function timeout seconds (default 60).
public class ES2DumpFullDecomp extends GhidraScript {
    @Override
    public void run() throws Exception {
        String outPath = getScriptArgs()[0];
        int timeout = getScriptArgs().length > 1 ? Integer.parseInt(getScriptArgs()[1]) : 60;

        DecompInterface decomp = new DecompInterface();
        DecompileOptions opts = new DecompileOptions();
        decomp.setOptions(opts);
        decomp.toggleCCode(true);
        decomp.toggleSyntaxTree(true);
        decomp.setSimplificationStyle("decompile");
        if (!decomp.openProgram(currentProgram)) {
            println("SCRIPT-ERROR could not open program in decompiler: " + decomp.getLastMessage());
            return;
        }

        int ok = 0, failed = 0;
        ConsoleTaskMonitor tm = new ConsoleTaskMonitor();
        try (PrintWriter pw = new PrintWriter(new BufferedWriter(new FileWriter(outPath), 1 << 20))) {
            pw.println("/* Full decompilation of " + currentProgram.getName() + " */");
            pw.println();
            FunctionIterator it = currentProgram.getFunctionManager().getFunctions(true);
            while (it.hasNext()) {
                monitor.checkCancelled();
                Function f = it.next();
                pw.println("/* ===================================================================");
                pw.println("   " + f.getName() + " @ " + f.getEntryPoint()
                        + (f.isThunk() ? "  [thunk]" : "") + (f.isExternal() ? "  [external]" : ""));
                pw.println("   =================================================================== */");
                if (f.isExternal()) {
                    pw.println("// external function, no body");
                    pw.println();
                    continue;
                }
                DecompileResults res = null;
                try {
                    res = decomp.decompileFunction(f, timeout, tm);
                } catch (Exception e) {
                    pw.println("// decompile threw: " + e);
                }
                if (res != null && res.decompileCompleted()) {
                    pw.println(res.getDecompiledFunction().getC());
                    ok++;
                } else {
                    pw.println("// DECOMPILE FAILED: " + (res != null ? res.getErrorMessage() : "no result"));
                    failed++;
                }
                pw.println();
                if ((ok + failed) % 200 == 0) {
                    pw.flush();
                    println("progress: " + (ok + failed) + " functions");
                }
            }
        }
        decomp.dispose();
        println("SCRIPT-OK decompiled " + ok + " functions (" + failed + " failed) to " + outPath);
    }
}
