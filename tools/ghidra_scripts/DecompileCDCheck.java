// @author
// @category Search
// @keybinding
// @menupath Search.Decompile CD Check
// @toolbar

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.util.task.ConsoleTaskMonitor;

public class DecompileCDCheck extends GhidraScript {

    private void dumpWriteXrefs(long va) throws Exception {
        Address addr = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(va);
        println("\n\n===== Xrefs to " + addr + " =====");
        ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(addr);
        int count = 0;
        while (refs.hasNext()) {
            Reference ref = refs.next();
            count++;
            Address from = ref.getFromAddress();
            Function f = currentProgram.getFunctionManager().getFunctionContaining(from);
            println("-- #" + count + " from " + from + " (" + ref.getReferenceType() + ") in " +
                (f != null ? f.getName() + "@" + f.getEntryPoint() : "?"));
            var instr = currentProgram.getListing().getInstructionAt(from);
            if (instr != null) {
                println("     " + instr.toString());
            }
        }
        if (count == 0) println("   (none)");
    }

    @Override
    public void run() throws Exception {
        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);

        long[] targets = new long[] { 0x0040d327L };

        for (long t : targets) {
            Address addr = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(t);
            Function func = currentProgram.getFunctionManager().getFunctionAt(addr);
            println("\n\n===== Decompiling " + addr + " (" + (func != null ? func.getName() : "no function defined, disassembling instead") + ") =====");
            if (func == null) {
                // try to find containing function or just disassemble a chunk
                func = currentProgram.getFunctionManager().getFunctionContaining(addr);
            }
            if (func == null) {
                println("No function at/containing " + addr + " - dumping raw instructions instead");
                var instr = currentProgram.getListing().getInstructionAt(addr);
                for (int i = 0; i < 40 && instr != null; i++) {
                    println("  " + instr.getAddress() + ": " + instr.toString());
                    instr = currentProgram.getListing().getInstructionAfter(instr.getAddress());
                }
                continue;
            }
            DecompileResults res = decomp.decompileFunction(func, 60, new ConsoleTaskMonitor());
            if (res.decompileCompleted()) {
                println(res.getDecompiledFunction().getC());
            } else {
                println("Decompile failed: " + res.getErrorMessage());
            }
        }
    }
}
