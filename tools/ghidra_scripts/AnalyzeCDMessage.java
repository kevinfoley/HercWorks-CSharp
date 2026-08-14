// @author
// @category Search
// @keybinding
// @menupath Search.Find CD Message References
// @toolbar

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;

public class AnalyzeCDMessage extends GhidraScript {
    @Override
    public void run() throws Exception {
        // Address of "Please insert ESII CD and restart" string
        Address stringAddr = currentProgram.getImageBase().add(0x6B4BD);

        println("=== CD Message Analysis ===");
        println("String location: " + stringAddr + " (0x6B4BD)");
        println("String content: \"Please insert ESII CD and restart\"");
        println();

        // Find all references to this string
        ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(stringAddr);

        if (!refs.hasNext()) {
            println("No direct references found to string address.");
            println("Searching for references in nearby code...");
        }

        int refCount = 0;
        while (refs.hasNext()) {
            Reference ref = refs.next();
            Address refAddr = ref.getFromAddress();
            refCount++;

            println("\n--- Reference #" + refCount + " ---");
            println("Referenced from: " + refAddr);
            println("Reference type: " + ref.getReferenceType());

            // Show the instruction at the reference
            Instruction instr = currentProgram.getListing().getInstructionAt(refAddr);
            if (instr != null) {
                println("Instruction: " + instr.toString());
                println("Mnemonic: " + instr.getMnemonicString());

                // Show surrounding context (5 instructions before and after)
                println("\n  Context (5 instructions before):");
                Instruction ctx = instr;
                for (int i = 0; i < 5; i++) {
                    ctx = currentProgram.getListing().getInstructionBefore(ctx.getAddress());
                    if (ctx != null) {
                        println("    " + ctx.getAddress() + ": " + ctx.toString());
                    }
                }

                println("\n  Context (5 instructions after):");
                ctx = instr;
                for (int i = 0; i < 5; i++) {
                    ctx = currentProgram.getListing().getInstructionAfter(ctx.getAddress());
                    if (ctx != null) {
                        println("    " + ctx.getAddress() + ": " + ctx.toString());
                    }
                }
            }

            // Try to find the function containing this reference
            var func = currentProgram.getFunctionManager().getFunctionContaining(refAddr);
            if (func != null) {
                println("\nContaining function: " + func.getName() + " (" + func.getEntryPoint() + ")");
            }
        }

        if (refCount == 0) {
            println("\nNo references found - the string may be embedded in code or data.");
        } else {
            println("\n\nTotal references found: " + refCount);
        }
    }
}
