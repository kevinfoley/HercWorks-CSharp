import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.pcode.HighFunctionDBUtil;
import ghidra.program.model.pcode.HighFunctionDBUtil.ReturnCommitOption;
import ghidra.program.model.symbol.SourceType;
import ghidra.util.task.ConsoleTaskMonitor;

// args[0] = number of passes (default 2)
public class ES2CommitAllParams extends GhidraScript {
    @Override
    public void run() throws Exception {
        int passes = getScriptArgs().length > 0 ? Integer.parseInt(getScriptArgs()[0]) : 2;
        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        ConsoleTaskMonitor tm = new ConsoleTaskMonitor();

        for (int pass = 1; pass <= passes; pass++) {
            int ok = 0, fail = 0;
            for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
                if (f.isThunk() || f.isExternal()) continue;
                try {
                    DecompileResults res = decomp.decompileFunction(f, 30, tm);
                    if (res != null && res.getHighFunction() != null && res.getHighFunction().getFunctionPrototype() != null) {
                        HighFunctionDBUtil.commitParamsToDatabase(res.getHighFunction(), true,
                            ReturnCommitOption.COMMIT, SourceType.ANALYSIS);
                        ok++;
                    } else {
                        fail++;
                    }
                } catch (Exception e) {
                    fail++;
                }
            }
            println("Pass " + pass + ": committed " + ok + ", failed " + fail);
        }
        decomp.dispose();
    }
}
