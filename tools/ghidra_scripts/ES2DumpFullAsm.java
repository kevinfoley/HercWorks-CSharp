import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.CodeUnit;
import ghidra.program.model.listing.CodeUnitIterator;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.Symbol;
import java.io.BufferedWriter;
import java.io.FileWriter;
import java.io.PrintWriter;

// Full disassembly of the current program.
// Runs of undefined bytes (bytes Ghidra never typed as code or data) are collapsed into a single
// "; <N> undefined bytes" line instead of one line each, which is what makes the file readable --
// a retail EXE's .data is overwhelmingly untyped and dwarfs the actual code otherwise. A run is
// broken by any symbol, comment or defined unit, so nothing that carries information is collapsed.
//
// args[0] = output path.
// args[1] = optional "-keepundef" to emit every undefined byte on its own line (the old behaviour).
public class ES2DumpFullAsm extends GhidraScript {

    private PrintWriter pw;
    private Address runStart;
    private Address runEnd;
    private long runBytes;
    private long collapsedRuns;
    private long collapsedBytes;

    @Override
    public void run() throws Exception {
        String outPath = getScriptArgs()[0];
        boolean keepUndef = getScriptArgs().length > 1 && getScriptArgs()[1].equalsIgnoreCase("-keepundef");
        Listing listing = currentProgram.getListing();
        long insnCount = 0;
        long dataCount = 0;

        try (PrintWriter out = new PrintWriter(new BufferedWriter(new FileWriter(outPath), 1 << 20))) {
            pw = out;
            pw.println("; Full disassembly of " + currentProgram.getName());
            pw.println("; image base " + currentProgram.getImageBase());
            if (!keepUndef) pw.println("; runs of undefined bytes are collapsed to one line each");
            pw.println();

            for (MemoryBlock block : currentProgram.getMemory().getBlocks()) {
                flushRun();
                pw.println();
                pw.println(";==============================================================");
                pw.printf ("; BLOCK %s  %s - %s  (%s%s%s)%n",
                        block.getName(), block.getStart(), block.getEnd(),
                        block.isRead() ? "r" : "-", block.isWrite() ? "w" : "-",
                        block.isExecute() ? "x" : "-");
                pw.println(";==============================================================");
                if (!block.isInitialized()) {
                    pw.println("; <uninitialized block, no contents>");
                    continue;
                }

                CodeUnitIterator it = listing.getCodeUnits(
                        new ghidra.program.model.address.AddressSet(block.getStart(), block.getEnd()), true);
                Function currentFunc = null;
                while (it.hasNext()) {
                    monitor.checkCancelled();
                    CodeUnit cu = it.next();
                    Address addr = cu.getMinAddress();

                    Symbol[] syms = currentProgram.getSymbolTable().getSymbols(addr);
                    String preCmt = cu.getComment(CodeUnit.PRE_COMMENT);
                    String eolCmt = cu.getComment(CodeUnit.EOL_COMMENT);
                    boolean bare = syms.length == 0 && preCmt == null && eolCmt == null;
                    boolean undefined = (cu instanceof Data) && !((Data) cu).isDefined();

                    if (!keepUndef && undefined && bare) {
                        if (runStart == null) {
                            runStart = addr;
                            runBytes = 0;
                        }
                        runEnd = cu.getMaxAddress();
                        runBytes += cu.getLength();
                        continue;
                    }
                    flushRun();

                    Function f = listing.getFunctionAt(addr);
                    if (f != null && f != currentFunc) {
                        currentFunc = f;
                        pw.println();
                        pw.println("; --------------------------------------------------------");
                        pw.println("; FUNCTION " + f.getName() + " @ " + f.getEntryPoint());
                        pw.println("; " + f.getPrototypeString(true, false));
                        pw.println("; --------------------------------------------------------");
                    }

                    for (Symbol s : syms) {
                        if (s.isPrimary() && (f == null || !s.getName().equals(f.getName()))) {
                            pw.println(s.getName() + ":");
                        }
                    }

                    if (preCmt != null) {
                        for (String line : preCmt.split("\n")) pw.println("    ; " + line);
                    }

                    StringBuilder bytes = new StringBuilder();
                    try {
                        byte[] b = cu.getBytes();
                        int n = Math.min(b.length, 12);
                        for (int i = 0; i < n; i++) bytes.append(String.format("%02x", b[i]));
                        if (b.length > n) bytes.append("..");
                    } catch (Exception e) {
                        bytes.append("??");
                    }

                    if (cu instanceof Instruction) {
                        insnCount++;
                        Instruction insn = (Instruction) cu;
                        StringBuilder line = new StringBuilder();
                        line.append(String.format("%-10s %-26s %s", addr.toString(), bytes.toString(), insn.toString()));
                        for (Reference r : insn.getReferencesFrom()) {
                            if (r.getReferenceType().isCall() || r.getReferenceType().isJump()) {
                                Function tf = listing.getFunctionAt(r.getToAddress());
                                if (tf != null) {
                                    line.append("   ; -> ").append(tf.getName());
                                    break;
                                }
                            }
                        }
                        pw.println(line);
                    } else {
                        dataCount++;
                        Data d = listing.getDataAt(addr);
                        pw.println(String.format("%-10s %-26s %-12s %s", addr.toString(), bytes.toString(),
                                cu.getMnemonicString(),
                                d != null ? String.valueOf(d.getDefaultValueRepresentation()) : ""));
                    }

                    if (eolCmt != null) pw.println("    ; " + eolCmt.replace("\n", " "));
                }
                flushRun();
            }
            flushRun();
        }
        pw = null;
        println("SCRIPT-OK wrote " + insnCount + " instructions and " + dataCount + " data units to " + outPath
                + "; collapsed " + collapsedBytes + " undefined bytes into " + collapsedRuns + " lines");
    }

    private void flushRun() {
        if (runStart == null) return;
        collapsedRuns++;
        collapsedBytes += runBytes;
        pw.println(String.format("%-10s ; ... %d undefined bytes, through %s", runStart, runBytes, runEnd));
        runStart = null;
        runEnd = null;
        runBytes = 0;
    }
}
