import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import java.io.BufferedReader;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.List;

// Like ES2FindAddressRefs, but for several target addresses in one program load.
// args[0] = spec file path. Spec file: line 1 = output path, remaining lines = target addresses (hex).
public class ES2FindAddressRefsBatch extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        String outPath;
        List<String> addrs = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            outPath = br.readLine();
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (!line.isEmpty()) addrs.add(line);
            }
        }

        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (String addrStr : addrs) {
                Address target = currentProgram.getAddressFactory().getAddress(addrStr);
                pw.println("=== refs to " + addrStr + " ===");
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
        }
        println("wrote batch address refs to " + outPath);
    }
}
