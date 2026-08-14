// @author
// @category Search
// @keybinding
// @menupath Search.Find CD Xrefs
// @toolbar

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;

public class FindCDXrefs extends GhidraScript {

    private void dumpXrefs(String label, long va) throws Exception {
        Address addr = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(va);
        println("\n\n===== " + label + " @ " + addr + " =====");

        ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(addr);
        int count = 0;
        while (refs.hasNext()) {
            Reference ref = refs.next();
            Address from = ref.getFromAddress();
            count++;
            println("-- xref #" + count + " from " + from + " (" + ref.getReferenceType() + ") --");

            var func = currentProgram.getFunctionManager().getFunctionContaining(from);
            if (func != null) {
                println("   in function: " + func.getName() + " @ " + func.getEntryPoint());
            }

            Instruction instr = currentProgram.getListing().getInstructionAt(from);
            if (instr != null) {
                println("   Context before:");
                Instruction ctx = instr;
                for (int i = 0; i < 8; i++) {
                    ctx = currentProgram.getListing().getInstructionBefore(ctx.getAddress());
                    if (ctx == null) break;
                }
                // walk forward from earliest to print in order
                java.util.List<String> lines = new java.util.ArrayList<>();
                Instruction walk = ctx != null ? ctx : instr;
                for (int i = 0; i < 20 && walk != null; i++) {
                    String marker = walk.getAddress().equals(from) ? " <=== XREF" : "";
                    lines.add("     " + walk.getAddress() + ": " + walk.toString() + marker);
                    walk = currentProgram.getListing().getInstructionAfter(walk.getAddress());
                    if (walk != null && walk.getAddress().subtract(from) > 40) break;
                }
                for (String l : lines) println(l);
            }
        }
        if (count == 0) {
            println("   (no code xrefs found)");
        }
    }

    @Override
    public void run() throws Exception {
        long base = currentProgram.getImageBase().getOffset();
        println("Image base: 0x" + Long.toHexString(base));

        // VAs computed from PE section headers (CODE rva=0x1000/raw=0x600, DATA rva=0x6C000/raw=0x6B000)
        dumpXrefs("CD message copy 1 (file 0x6B4BD)", base + 0x46C4BDL - 0x400000L);
        dumpXrefs("CD message copy 2 (file 0x70430)", base + 0x471430L - 0x400000L);
        dumpXrefs("-eggplant copy 1 (file 0x6B22B)", base + 0x46C22BL - 0x400000L);
        dumpXrefs("-eggplant copy 2 (file 0x7020A)", base + 0x47120AL - 0x400000L);
        dumpXrefs("dummy (file 0x70204)", base + 0x471204L - 0x400000L);
    }
}
