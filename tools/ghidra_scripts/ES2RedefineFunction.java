import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSet;
import ghidra.program.model.listing.Function;
import ghidra.util.task.ConsoleTaskMonitor;
import java.io.PrintWriter;
import java.io.FileWriter;

// Clears any existing code units/function in [clearStart, trueEntry), then disassembles and
// creates a function starting exactly at trueEntry, and decompiles it.
// args[0] = clearStart (hex), args[1] = trueEntry (hex), args[2] = output path.
public class ES2RedefineFunction extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address clearStart = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        Address trueEntry = currentProgram.getAddressFactory().getAddress(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            Function existing = currentProgram.getFunctionManager().getFunctionContaining(clearStart);
            if (existing != null) {
                pw.println("removing existing function " + existing.getName() + " @ " + existing.getEntryPoint()
                        + " body=" + existing.getBody());
                currentProgram.getFunctionManager().removeFunction(existing.getEntryPoint());
            }
            Address clearEnd = trueEntry.add(0x200);
            currentProgram.getListing().clearCodeUnits(clearStart, clearEnd, false);
            disassemble(trueEntry);
            createFunction(trueEntry, null);
            Function f = currentProgram.getFunctionManager().getFunctionContaining(trueEntry);
            if (f == null) {
                pw.println("still no function at " + trueEntry);
            } else {
                pw.println("=== " + f.getName() + " @ " + f.getEntryPoint() + " ===");
                DecompInterface decomp = new DecompInterface();
                decomp.openProgram(currentProgram);
                DecompileResults res = decomp.decompileFunction(f, 60, new ConsoleTaskMonitor());
                if (res != null && res.decompileCompleted()) {
                    pw.println(res.getDecompiledFunction().getC());
                } else {
                    pw.println("decompile failed: " + (res != null ? res.getErrorMessage() : "null result"));
                }
            }
        }
        println("wrote result to " + outPath);
    }
}
