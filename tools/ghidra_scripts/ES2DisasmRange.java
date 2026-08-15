import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.listing.Listing;
import java.io.PrintWriter;
import java.io.FileWriter;

// Lists existing disassembled instructions in [start, start+len) plus the nearest function
// before/containing start. args[0] = start address (hex), args[1] = length, args[2] = output path.
public class ES2DisasmRange extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address start = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int len = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        Address end = start.add(len);
        Listing listing = currentProgram.getListing();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            Function nearest = listing.getFunctionContaining(start);
            if (nearest == null) {
                pw.println("no containing function");
            } else {
                pw.println("containing function: " + nearest.getName() + " @ " + nearest.getEntryPoint());
            }
            pw.println("--- instructions ---");
            InstructionIterator it = listing.getInstructions(start, true);
            while (it.hasNext()) {
                Instruction ins = it.next();
                if (ins.getAddress().compareTo(end) >= 0) break;
                pw.println(ins.getAddress() + "  " + ins);
            }
        }
        println("wrote disasm range to " + outPath);
    }
}
