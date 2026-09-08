import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;

// Creates a Function at each given address without clearing or re-disassembling anything around it.
// For code Ghidra disassembled but never promoted to a function -- typically a vtable slot whose
// only reference is the table itself, so nothing calls it directly and auto-analysis never saw an
// entry point. ES2ApplySymbolNames skips such an address ("no function at ..."), so this runs first.
//
// Unlike ES2RedefineFunction this destroys nothing: an address that already has a function is
// reported and left alone, and no code units are cleared. args = addresses (hex).
public class ES2DefineFunctionAt extends GhidraScript {
    @Override
    public void run() throws Exception {
        for (String arg : getScriptArgs()) {
            Address a = currentProgram.getAddressFactory().getAddress(arg);
            if (a == null || !currentProgram.getMemory().contains(a)) {
                println("ERROR: " + arg + " is not in this program's memory.");
                continue;
            }
            Function existing = currentProgram.getFunctionManager().getFunctionAt(a);
            if (existing != null) {
                println("exists " + arg + " -> " + existing.getName());
                continue;
            }
            if (getInstructionAt(a) == null) {
                disassemble(a);
            }
            Function f = createFunction(a, null);
            println((f == null ? "FAILED " : "created ") + arg
                + (f == null ? "" : " -> " + f.getName() + " body=" + f.getBody().getNumAddresses() + " bytes"));
        }
    }
}
