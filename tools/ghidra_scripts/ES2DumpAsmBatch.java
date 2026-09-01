import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import java.io.FileWriter;
import java.io.PrintWriter;

// args[0] = function entry addresses (hex, no 0x prefix) separated by "+" -- NOT commas, which
//           cmd.exe silently splits into separate arguments before Ghidra ever sees them
// args[1] = max instructions per function
// args[2] = output path
//
// Batch form of ES2DumpAsm: dumps whole-function disassembly for many functions in a single
// headless run instead of paying JVM+project startup per function. Stops each function at its
// own body end (or maxInsn, whichever comes first) rather than running into the next one.
public class ES2DumpAsmBatch extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] addrs = getScriptArgs()[0].split("[+,]");
        int maxInsn = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];

        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (String a : addrs) {
                String hex = a.trim();
                if (hex.isEmpty()) continue;
                Address entry = currentProgram.getAddressFactory().getAddress(hex);
                Function f = currentProgram.getFunctionManager().getFunctionContaining(entry);
                Address bodyEnd = (f != null) ? f.getBody().getMaxAddress() : null;

                pw.println("========================================");
                pw.println("== " + hex + "  " + (f != null ? f.getName() : "<no function>")
                    + (f != null ? "  body=" + f.getBody().getMinAddress() + ".." + bodyEnd : ""));
                pw.println("========================================");

                InstructionIterator it = currentProgram.getListing().getInstructions(entry, true);
                int count = 0;
                while (it.hasNext() && count < maxInsn) {
                    Instruction insn = it.next();
                    if (bodyEnd != null && insn.getAddress().compareTo(bodyEnd) > 0) break;
                    pw.println(insn.getAddress() + ": " + insn);
                    count++;
                }
                pw.println();
            }
        }
        println("ES2DumpAsmBatch: wrote " + addrs.length + " functions to " + outPath);
    }
}
