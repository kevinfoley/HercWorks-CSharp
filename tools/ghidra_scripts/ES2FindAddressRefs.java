import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import java.io.PrintWriter;
import java.io.FileWriter;

// args[0] = target address (hex), args[1] = output path
public class ES2FindAddressRefs extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address target = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        String outPath = getScriptArgs()[1];
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(target);
            while (refs.hasNext()) {
                Reference r = refs.next();
                Address from = r.getFromAddress();
                Function f = currentProgram.getFunctionManager().getFunctionContaining(from);
                if (f != null) {
                    pw.println(from + " in " + f.getName() + " @ " + f.getEntryPoint() + " (" + r.getReferenceType() + ")");
                } else {
                    pw.println(from + " (no function) (" + r.getReferenceType() + ")");
                }
            }
        }
        println("wrote address refs to " + outPath);
    }
}
