import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.util.task.ConsoleTaskMonitor;
import java.io.PrintWriter;
import java.io.FileWriter;

// Decompiles the function containing a given address. args[0] = address (hex), args[1] = output path.
public class ES2DecompileContaining extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address addr = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        String outPath = getScriptArgs()[1];
        Function f = currentProgram.getFunctionManager().getFunctionContaining(addr);
        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            if (f == null) {
                pw.println("no function contains " + addr);
            } else {
                pw.println("=== " + f.getName() + " @ " + f.getEntryPoint() + " ===");
                DecompileResults res = decomp.decompileFunction(f, 60, new ConsoleTaskMonitor());
                if (res != null && res.decompileCompleted()) {
                    pw.println(res.getDecompiledFunction().getC());
                } else {
                    pw.println("decompile failed");
                }
            }
        }
        println("wrote decompile to " + outPath);
    }
}
