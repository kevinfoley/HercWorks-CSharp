import ghidra.app.script.GhidraScript;
import java.util.HashMap;
import java.util.Map;

public class ES2EnableParamID extends GhidraScript {
    @Override
    public void run() throws Exception {
        Map<String, String> opts = new HashMap<>();
        opts.put("Decompiler Parameter ID", "true");
        opts.put("Decompiler Parameter ID.Prototype Evaluation", "__watcall");
        setAnalysisOptions(currentProgram, opts);
        println("Enabled Decompiler Parameter ID with __watcall evaluation");
    }
}
