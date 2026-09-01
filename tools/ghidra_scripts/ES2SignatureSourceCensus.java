import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Parameter;
import ghidra.program.model.symbol.SourceType;
import java.util.Map;
import java.util.TreeMap;

// No arguments. Read-only sanity check / positive control for ES2DumpSignatures: histograms the
// SourceType of every function signature in the program, and separately counts functions carrying
// at least one USER_DEFINED/IMPORTED parameter. If the whole program reports zero human-sourced
// signatures the detection itself is suspect; imported DLL thunks should show up as IMPORTED.
public class ES2SignatureSourceCensus extends GhidraScript {
    @Override
    public void run() throws Exception {
        Map<String, Integer> sigHist = new TreeMap<>();
        Map<String, Integer> paramHist = new TreeMap<>();
        int total = 0, humanParam = 0;

        for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
            total++;
            SourceType s = f.getSignatureSource();
            String key = (s == null) ? "null" : s.toString();
            sigHist.merge(key, 1, Integer::sum);

            boolean anyHuman = false;
            for (Parameter p : f.getParameters()) {
                SourceType ps = p.getSource();
                paramHist.merge(ps == null ? "null" : ps.toString(), 1, Integer::sum);
                if (ps == SourceType.USER_DEFINED || ps == SourceType.IMPORTED) anyHuman = true;
            }
            if (anyHuman) {
                humanParam++;
                if (humanParam <= 25) {
                    println("CENSUS   human-param fn: " + f.getEntryPoint() + " " + f.getName()
                        + (f.isThunk() ? " [thunk]" : "") + (f.isExternal() ? " [external]" : "")
                        + "  " + f.getSignature().getPrototypeString(true));
                }
            }
        }

        println("CENSUS [" + currentProgram.getName() + "] functions=" + total);
        println("CENSUS   signature source: " + sigHist);
        println("CENSUS   parameter source: " + paramHist);
        println("CENSUS   functions with >=1 USER_DEFINED/IMPORTED parameter: " + humanParam);
    }
}
