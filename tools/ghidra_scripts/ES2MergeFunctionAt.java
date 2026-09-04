import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.listing.FunctionManager;
import java.io.PrintWriter;
import java.io.FileWriter;
import java.util.ArrayList;
import java.util.List;

// Repairs a function whose entry Ghidra placed past the real prologue, leaving the prologue as a
// tiny separate function. Removes every function whose entry lies in [trueEntry, trueEntry+length),
// clears the code units there, then disassembles and creates one function at trueEntry.
// args[0] = trueEntry (hex), args[1] = byte length to sweep (decimal), args[2] = output path.
public class ES2MergeFunctionAt extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address entry = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int length = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        Address end = entry.add(length - 1);

        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            FunctionManager fm = currentProgram.getFunctionManager();
            List<Address> doomed = new ArrayList<>();
            FunctionIterator it = fm.getFunctions(true);
            while (it.hasNext()) {
                Function f = it.next();
                Address e = f.getEntryPoint();
                if (e.compareTo(entry) >= 0 && e.compareTo(end) <= 0) {
                    pw.println("removing " + f.getName() + " @ " + e + " body=" + f.getBody());
                    doomed.add(e);
                }
            }
            for (Address a : doomed) {
                fm.removeFunction(a);
            }

            currentProgram.getListing().clearCodeUnits(entry, end, false);
            disassemble(entry);
            createFunction(entry, null);

            Function made = fm.getFunctionAt(entry);
            pw.println(made == null
                ? "FAILED: no function at " + entry
                : "created " + made.getName() + " @ " + entry + " body=" + made.getBody());
        }
        println("wrote merge result to " + outPath);
    }
}
