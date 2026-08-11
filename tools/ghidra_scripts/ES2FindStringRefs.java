import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.DataIterator;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import java.io.BufferedReader;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.List;

// args[0] = spec file path. Spec file: line 1 = output path, remaining lines = keywords (one per line).
public class ES2FindStringRefs extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        String outPath;
        List<String> keywords = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            outPath = br.readLine();
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (!line.isEmpty()) keywords.add(line.toLowerCase());
            }
        }

        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            DataIterator di = currentProgram.getListing().getDefinedData(true);
            while (di.hasNext()) {
                Data d = di.next();
                if (!d.hasStringValue()) continue;
                String val;
                try { val = d.getDefaultValueRepresentation(); } catch (Exception e) { continue; }
                String lower = val.toLowerCase();
                boolean matched = false;
                for (String kw : keywords) {
                    if (lower.contains(kw)) { matched = true; break; }
                }
                if (!matched) continue;
                pw.println("STRING @ " + d.getAddress() + ": " + val);
                ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(d.getAddress());
                while (refs.hasNext()) {
                    Reference r = refs.next();
                    Address from = r.getFromAddress();
                    Function f = currentProgram.getFunctionManager().getFunctionContaining(from);
                    if (f != null) {
                        pw.println("  used in function: " + f.getName() + " @ " + f.getEntryPoint());
                    } else {
                        pw.println("  used at: " + from + " (no function)");
                    }
                }
            }
        }
        println("Wrote string xref list to " + outPath);
    }
}
