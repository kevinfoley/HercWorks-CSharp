import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.util.task.ConsoleTaskMonitor;
import java.io.PrintWriter;
import java.io.FileWriter;

// Decompiles every function whose entry point falls in [lo..hi].
// args[0] = low addr (hex), args[1] = high addr (hex, inclusive), args[2] = output path.
public class ES2DecompileRange extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address lo = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        Address hi = currentProgram.getAddressFactory().getAddress(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];

        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        int n = 0;
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            FunctionIterator it = currentProgram.getFunctionManager().getFunctions(true);
            while (it.hasNext()) {
                Function f = it.next();
                Address e = f.getEntryPoint();
                if (e.compareTo(lo) < 0 || e.compareTo(hi) > 0) continue;
                pw.println("=== " + f.getName() + " @ " + e + " ===");
                DecompileResults res = decomp.decompileFunction(f, 60, new ConsoleTaskMonitor());
                if (res != null && res.decompileCompleted()) {
                    pw.println(res.getDecompiledFunction().getC());
                } else {
                    pw.println("decompile failed");
                }
                n++;
            }
        }
        decomp.dispose();
        println("wrote " + n + " functions to " + outPath);
    }
}
