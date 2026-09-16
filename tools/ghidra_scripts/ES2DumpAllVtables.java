import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.data.DataType;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.DataIterator;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.model.symbol.Symbol;
import java.io.BufferedWriter;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

// Dumps every vtable-shaped structure in the current program.
//   Section 1: data whose applied type or symbol name marks it as a vtable (the /ES2 types
//              applied from known_vtables.json, and any MSVC ??_7 vftable symbols).
//   Section 2: heuristic sweep -- every 4-aligned run of >= MINSLOTS dwords in a non-executable
//              initialized block where each dword is the entry point of a defined function.
// args[0] = output path.  args[1] (optional) = minimum run length (default 3).
public class ES2DumpAllVtables extends GhidraScript {

    private Listing listing;
    private Memory mem;

    @Override
    public void run() throws Exception {
        String outPath = getScriptArgs()[0];
        int minSlots = getScriptArgs().length > 1 ? Integer.parseInt(getScriptArgs()[1]) : 3;
        listing = currentProgram.getListing();
        mem = currentProgram.getMemory();

        Set<Address> reported = new LinkedHashSet<>();
        int named = 0, heuristic = 0;

        try (PrintWriter pw = new PrintWriter(new BufferedWriter(new FileWriter(outPath), 1 << 18))) {
            pw.println("Vtables in " + currentProgram.getName());
            pw.println("image base " + currentProgram.getImageBase());
            pw.println();
            pw.println("================================================================");
            pw.println("SECTION 1 -- named / typed vtables");
            pw.println("================================================================");
            pw.println();

            DataIterator di = listing.getDefinedData(true);
            while (di.hasNext()) {
                monitor.checkCancelled();
                Data d = di.next();
                DataType dt = d.getDataType();
                String tn = dt != null ? dt.getName() : "";
                Symbol sym = currentProgram.getSymbolTable().getPrimarySymbol(d.getAddress());
                String sn = sym != null ? sym.getName() : "";
                boolean isVt = looksLikeVtableName(tn) || looksLikeVtableName(sn);
                if (!isVt) continue;
                int slots = Math.max(1, d.getLength() / 4);
                dumpTable(pw, (sn.isEmpty() ? tn : sn) + " [" + tn + "]", d.getAddress(), slots);
                reported.add(d.getAddress());
                named++;
            }
            if (named == 0) pw.println("(none)");

            pw.println();
            pw.println("================================================================");
            pw.println("SECTION 2 -- heuristic function-pointer tables (>= " + minSlots + " slots)");
            pw.println("================================================================");
            pw.println();

            for (MemoryBlock block : mem.getBlocks()) {
                if (!block.isInitialized() || block.isExecute()) continue;
                Address start = block.getStart();
                long len = block.getSize();
                long off = 0;
                // align to 4
                while ((start.add(off).getOffset() & 3) != 0 && off < len) off++;
                while (off + 4 <= len) {
                    monitor.checkCancelled();
                    Address a = start.add(off);
                    int run = 0;
                    while (off + (long) (run + 1) * 4 <= len && isFunctionPointer(start.add(off + (long) run * 4))) {
                        run++;
                    }
                    if (run >= minSlots) {
                        if (!reported.contains(a)) {
                            dumpTable(pw, "table_" + a, a, run);
                            heuristic++;
                        }
                        off += (long) run * 4;
                    } else {
                        off += 4;
                    }
                }
            }
            if (heuristic == 0) pw.println("(none)");

            pw.println();
            pw.println("named tables: " + named + "   heuristic tables: " + heuristic);
        }
        println("SCRIPT-OK wrote " + named + " named and " + heuristic + " heuristic vtables to " + outPath);
    }

    private boolean looksLikeVtableName(String n) {
        if (n == null) return false;
        String l = n.toLowerCase();
        return l.contains("vtbl") || l.contains("vtable") || l.contains("vftable") || l.startsWith("??_7");
    }

    private boolean isFunctionPointer(Address slot) {
        try {
            long raw = mem.getInt(slot) & 0xFFFFFFFFL;
            if (raw == 0) return false;
            Address t = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(raw);
            return listing.getFunctionAt(t) != null;
        } catch (Exception e) {
            return false;
        }
    }

    private void dumpTable(PrintWriter pw, String label, Address base, int slots) {
        pw.println("=== " + label + " @ " + base + "  (" + slots + " slots) ===");
        List<String> xrefs = new ArrayList<>();
        ReferenceIterator ri = currentProgram.getReferenceManager().getReferencesTo(base);
        while (ri.hasNext() && xrefs.size() < 12) {
            Reference r = ri.next();
            Function cf = listing.getFunctionContaining(r.getFromAddress());
            xrefs.add(r.getFromAddress() + (cf != null ? " (" + cf.getName() + ")" : ""));
        }
        pw.println("  installed by: " + (xrefs.isEmpty() ? "<no xrefs>" : String.join(", ", xrefs)));
        for (int i = 0; i < slots; i++) {
            Address slotAddr = base.add((long) i * 4);
            long raw;
            try {
                raw = mem.getInt(slotAddr) & 0xFFFFFFFFL;
            } catch (Exception e) {
                pw.println(String.format("  +0x%03x (%s): <unreadable>", i * 4, slotAddr));
                continue;
            }
            Address target = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(raw);
            Function f = listing.getFunctionAt(target);
            String fname = (f != null) ? f.getName() : "(no function at " + target + ")";
            String fieldName = "";
            Data d = listing.getDataContaining(slotAddr);
            if (d != null) {
                Data comp = d.getComponentContaining((int) (slotAddr.getOffset() - d.getAddress().getOffset()));
                if (comp != null && comp.getFieldName() != null) fieldName = comp.getFieldName() + " = ";
            }
            pw.println(String.format("  +0x%03x (%s): %s%s @ %s", i * 4, slotAddr, fieldName, fname, target));
        }
        pw.println();
    }
}
