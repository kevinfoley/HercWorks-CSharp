// Maps a raw file-offset range in the imported PE onto Ghidra addresses, then reports what's
// there: containing function (if code) with a decompile, or containing data symbol/defined data
// (if data), plus a raw hex dump of the mapped memory range. Also prints raw file bytes for the
// same offset range straight from FileBytes, so the file-offset interpretation itself can be
// sanity-checked against the mapped-address interpretation.
//
// args[0] = output path
// args[1] = start file offset (decimal or 0x-hex)
// args[2] = end file offset (decimal or 0x-hex, inclusive)
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.mem.*;
import ghidra.program.database.mem.FileBytes;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolTable;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.util.task.ConsoleTaskMonitor;

import java.io.PrintWriter;
import java.util.List;

public class ES2FileOffsetRange extends GhidraScript {
    private static long parseNum(String s) {
        s = s.trim();
        if (s.startsWith("0x") || s.startsWith("0X")) {
            return Long.parseLong(s.substring(2), 16);
        }
        return Long.parseLong(s);
    }

    @Override
    public void run() throws Exception {
        String outPath = getScriptArgs()[0];
        long startOff = parseNum(getScriptArgs()[1]);
        long endOff = getScriptArgs().length > 2 ? parseNum(getScriptArgs()[2]) : startOff;

        try (PrintWriter out = new PrintWriter(outPath)) {
            out.println("Program: " + currentProgram.getName());
            out.println("Image base: " + currentProgram.getImageBase());
            out.println("File offset range: " + startOff + " (0x" + Long.toHexString(startOff) + ") - "
                    + endOff + " (0x" + Long.toHexString(endOff) + ")");
            out.println();

            Memory mem = currentProgram.getMemory();

            // Raw file bytes straight from FileBytes (the original imported file), no address mapping.
            List<FileBytes> fbList = mem.getAllFileBytes();
            out.println("=== FileBytes objects (" + fbList.size() + ") ===");
            for (FileBytes fb : fbList) {
                out.println("  " + fb.getFilename() + " size=" + fb.getSize());
            }
            out.println();

            for (FileBytes fb : fbList) {
                long size = fb.getSize();
                if (startOff >= 0 && startOff < size) {
                    out.println("=== Raw bytes from FileBytes '" + fb.getFilename() + "' at offset "
                            + startOff + "-" + endOff + " ===");
                    StringBuilder hex = new StringBuilder();
                    StringBuilder asc = new StringBuilder();
                    for (long o = startOff; o <= endOff && o < size; o++) {
                        int b = fb.getOriginalByte(o) & 0xff;
                        hex.append(String.format("%02X ", b));
                        asc.append(b >= 0x20 && b < 0x7f ? (char) b : '.');
                    }
                    out.println(hex.toString());
                    out.println(asc.toString());
                    out.println();
                }
            }

            // Try mapping the file offset range onto loaded memory addresses.
            out.println("=== Address mapping (Memory.locateAddressesForFileOffset) ===");
            for (long o = startOff; o <= endOff; o++) {
                try {
                    java.util.List<Address> addrs = mem.locateAddressesForFileOffset(o);
                    if (addrs != null && !addrs.isEmpty()) {
                        for (Address a : addrs) {
                            out.println("fileOffset " + o + " (0x" + Long.toHexString(o) + ") -> " + a);
                        }
                    }
                } catch (Throwable t) {
                    out.println("fileOffset " + o + ": mapping error " + t);
                }
            }
            out.println();

            // For each distinct mapped start address, show containing function/data + hex dump.
            java.util.List<Address> starts = mem.locateAddressesForFileOffset(startOff);
            if (starts != null) {
                for (Address addr : starts) {
                    out.println("=== Detail at mapped address " + addr + " ===");
                    Address endAddr = addr.add(Math.max(0, endOff - startOff));

                    FunctionManager fm = currentProgram.getFunctionManager();
                    Function fn = fm.getFunctionContaining(addr);
                    if (fn != null) {
                        out.println("Containing function: " + fn.getName() + " @ " + fn.getEntryPoint());
                        DecompInterface decomp = new DecompInterface();
                        decomp.openProgram(currentProgram);
                        DecompileResults res = decomp.decompileFunction(fn, 60, new ConsoleTaskMonitor());
                        if (res != null && res.decompileCompleted()) {
                            out.println("--- Decompiled ---");
                            out.println(res.getDecompiledFunction().getC());
                        } else {
                            out.println("Decompile failed: " + (res != null ? res.getErrorMessage() : "null result"));
                        }
                    } else {
                        out.println("No containing function (likely data).");

                        SymbolTable st = currentProgram.getSymbolTable();
                        Symbol[] syms = st.getSymbols(addr);
                        for (Symbol s : syms) {
                            out.println("Symbol here: " + s.getName() + " type=" + s.getSymbolType());
                        }

                        Data data = currentProgram.getListing().getDataContaining(addr);
                        if (data != null) {
                            out.println("Containing defined data: " + data.getAddress() + " type=" + data.getDataType().getName()
                                    + " len=" + data.getLength());
                            out.println(data.toString());
                        }

                        // Raw hex from mapped memory too, for comparison against the FileBytes dump above.
                        out.println("--- Raw memory hex ---");
                        StringBuilder hex = new StringBuilder();
                        Address cur = addr;
                        while (cur.compareTo(endAddr) <= 0) {
                            try {
                                hex.append(String.format("%02X ", mem.getByte(cur) & 0xff));
                            } catch (Exception e) {
                                hex.append("?? ");
                            }
                            cur = cur.add(1);
                        }
                        out.println(hex.toString());

                        // Also list references TO this address range (who reads/writes this data).
                        out.println("--- References TO this address ---");
                        for (var ref : currentProgram.getReferenceManager().getReferencesTo(addr)) {
                            Function callerFn = fm.getFunctionContaining(ref.getFromAddress());
                            out.println("  from " + ref.getFromAddress() + " (" + (callerFn != null ? callerFn.getName() : "?") + ") type=" + ref.getReferenceType());
                        }
                    }
                    out.println();
                }
            } else {
                out.println("No address mapping found for start offset.");
            }
        }
    }
}
