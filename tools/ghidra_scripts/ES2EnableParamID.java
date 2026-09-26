import ghidra.app.script.GhidraScript;
import ghidra.framework.options.Options;
import ghidra.program.model.listing.Program;
import java.util.HashMap;
import java.util.Map;

public class ES2EnableParamID extends GhidraScript {
    @Override
    public void run() throws Exception {
        Map<String, String> opts = new HashMap<>();
        opts.put("Decompiler Parameter ID", "true");
        setAnalysisOptions(currentProgram, opts);

        // setAnalysisOptions drops a value the option does not accept without raising,
        // so read every one back.
        Options analysis = currentProgram.getOptions(Program.ANALYSIS_PROPERTIES);
        for (Map.Entry<String, String> e : opts.entrySet()) {
            String actual = analysis.getValueAsString(e.getKey());
            if (!e.getValue().equals(actual)) {
                throw new IllegalStateException(
                    e.getKey() + " is " + actual + ", expected " + e.getValue());
            }
        }
        println("Enabled Decompiler Parameter ID");
    }
}
