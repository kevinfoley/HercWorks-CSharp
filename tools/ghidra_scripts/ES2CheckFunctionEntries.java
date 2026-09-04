import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import java.io.PrintWriter;
import java.io.FileWriter;

// Reports, for each address given, whether a function starts exactly there and what contains it.
// Use before a rename/signature apply to check that a corrected function boundary really took.
// args[0] = output path, args[1..] = addresses (hex).
public class ES2CheckFunctionEntries extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter pw = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                Address a = currentProgram.getAddressFactory().getAddress(args[i]);
                Function at = currentProgram.getFunctionManager().getFunctionAt(a);
                Function containing = currentProgram.getFunctionManager().getFunctionContaining(a);
                pw.println(args[i]
                    + " at=" + (at == null ? "<none>" : at.getName() + " " + at.getSignature())
                    + " containing=" + (containing == null ? "<none>"
                        : containing.getName() + " @ " + containing.getEntryPoint()));
            }
        }
        println("wrote entry check to " + args[0]);
    }
}
