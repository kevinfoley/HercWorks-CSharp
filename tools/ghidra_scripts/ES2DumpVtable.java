import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;
import java.io.FileWriter;

// args[0] = vtable base address (hex), args[1] = slot count, args[2] = output path
// Dumps N consecutive 4-byte pointers starting at the vtable base, resolving each to a function name.
public class ES2DumpVtable extends GhidraScript {
    @Override
    public void run() throws Exception {
        Address base = currentProgram.getAddressFactory().getAddress(getScriptArgs()[0]);
        int count = Integer.parseInt(getScriptArgs()[1]);
        String outPath = getScriptArgs()[2];
        Memory mem = currentProgram.getMemory();
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (int i = 0; i < count; i++) {
                Address slotAddr = base.add((long) i * 4);
                long raw;
                try {
                    raw = mem.getInt(slotAddr) & 0xFFFFFFFFL;
                } catch (Exception e) {
                    pw.println(String.format("+0x%x (%s): <unreadable>", i * 4, slotAddr));
                    continue;
                }
                Address target = currentProgram.getAddressFactory().getAddress(Long.toHexString(raw));
                Function f = currentProgram.getFunctionManager().getFunctionAt(target);
                String fname = (f != null) ? f.getName() : "(no function at " + target + ")";
                pw.println(String.format("+0x%x (%s): -> %s @ %s", i * 4, slotAddr, fname, target));
            }
        }
        println("wrote vtable dump to " + outPath);
    }
}
