import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import java.io.FileWriter;
import java.io.PrintWriter;

// args[0] = target function entry addresses (hex) separated by "+" (NOT commas -- cmd.exe splits those)
// args[1] = instructions of context to show before and after each call site
// args[2] = max call sites to show per target
// args[3] = output path
//
// For each target, lists call sites and prints the surrounding instructions. Written to settle two
// questions the callee body alone cannot answer: how arguments are set up (which PUSH goes with
// which parameter), and who cleans the stack afterwards -- i.e. __cdecl vs __stdcall, which the
// decompiler's ANALYSIS-tier prototypes get wrong throughout this database.
public class ES2DumpCallSites extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] targets = getScriptArgs()[0].split("[+,]");
        int ctx = Integer.parseInt(getScriptArgs()[1]);
        int maxSites = Integer.parseInt(getScriptArgs()[2]);
        String outPath = getScriptArgs()[3];

        Listing listing = currentProgram.getListing();

        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (String t : targets) {
                String hex = t.trim();
                if (hex.isEmpty()) continue;
                Address target = currentProgram.getAddressFactory().getAddress(hex);
                Function tf = currentProgram.getFunctionManager().getFunctionAt(target);
                pw.println("################################################");
                pw.println("## callers of " + hex + " " + (tf != null ? tf.getName() : "<none>"));
                pw.println("################################################");

                int shown = 0;
                ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(target);
                while (refs.hasNext() && shown < maxSites) {
                    Reference r = refs.next();
                    if (!r.getReferenceType().isCall()) continue;
                    Address site = r.getFromAddress();
                    Function cf = currentProgram.getFunctionManager().getFunctionContaining(site);
                    pw.println("---- call site " + site + " in " + (cf != null ? cf.getName() : "<none>"));

                    Address cur = site;
                    for (int i = 0; i < ctx; i++) {
                        Instruction prev = listing.getInstructionBefore(cur);
                        if (prev == null) break;
                        cur = prev.getAddress();
                    }
                    Instruction insn = listing.getInstructionAt(cur);
                    for (int i = 0; i < ctx * 2 + 1 && insn != null; i++) {
                        String mark = insn.getAddress().equals(site) ? "  <== CALL" : "";
                        pw.println("    " + insn.getAddress() + ": " + insn + mark);
                        insn = insn.getNext();
                    }
                    pw.println();
                    shown++;
                }
                if (shown == 0) pw.println("    (no call references found)\n");
            }
        }
        println("ES2DumpCallSites: done -> " + outPath);
    }
}
