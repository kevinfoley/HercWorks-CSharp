import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import java.io.PrintWriter;
import java.io.FileWriter;

// args[0] = function entry address (hex, no 0x prefix), args[1] = max instructions, args[2] = output path
public class ES2DumpAsm extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address entry = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int maxInsn = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            InstructionIterator it = currentProgram.getListing().getInstructions(entry, true);
            int count = 0;
            while (it.hasNext() && count < maxInsn) {
                Instruction insn = it.next();
                pw.println(insn.getAddress() + ": " + insn);
                count++;
            }
        }
        println("wrote asm to " + outPath);
    }
}
