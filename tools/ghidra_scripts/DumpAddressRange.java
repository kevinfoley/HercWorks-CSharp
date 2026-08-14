// @author
// @category Search
// @keybinding
// @menupath Search.Dump Address Range
// @toolbar

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Instruction;

public class DumpAddressRange extends GhidraScript {
    @Override
    public void run() throws Exception {
        if (getScriptArgs().length < 2) {
            println("Usage: provide start and end addresses as args");
            return;
        }

        String startStr = getScriptArgs()[0];
        String endStr = getScriptArgs()[1];

        Address start = currentProgram.getImageBase().add(Long.parseUnsignedLong(startStr, 16));
        Address end = currentProgram.getImageBase().add(Long.parseUnsignedLong(endStr, 16));

        println("=== Code from " + start + " to " + end + " ===\n");

        Instruction instr = currentProgram.getListing().getInstructionAt(start);
        int count = 0;
        while (instr != null && instr.getAddress().getOffset() <= end.getOffset() && count < 50) {
            println(instr.getAddress() + ": " + instr.toString());
            instr = currentProgram.getListing().getInstructionAfter(instr.getAddress());
            count++;
        }

        println("\nShowed " + count + " instructions");
    }
}
