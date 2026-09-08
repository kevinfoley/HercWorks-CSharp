import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Data;
import ghidra.program.model.symbol.Symbol;
import java.io.FileWriter;
import java.io.PrintWriter;

// Reports how a range is TYPED, as opposed to ES2DumpRange, which reports the bytes underneath.
// Prints the defined data at each address given -- its label, data type and length -- then each
// component with its field name, type and value. Use it to check that a structure apply
// (ES2ApplyVtables, and the object structures that follow it) really took.
// args[0] = output path, args[1..] = addresses (hex).
public class ES2DumpDataAt extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter pw = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                Address addr = currentProgram.getAddressFactory().getAddress(args[i]);
                Data data = getDataAt(addr);
                if (data == null) {
                    pw.println(args[i] + ": <no defined data>");
                    continue;
                }
                Symbol sym = currentProgram.getSymbolTable().getPrimarySymbol(addr);
                pw.println(args[i] + ": " + (sym != null ? sym.getName() : "<unlabelled>")
                    + " : " + data.getDataType().getName()
                    + " (" + data.getLength() + " bytes, "
                    + data.getNumComponents() + " components)");
                for (int c = 0; c < data.getNumComponents(); c++) {
                    Data comp = data.getComponent(c);
                    pw.println(String.format("  +0x%-4x %-24s %-28s %s",
                        comp.getParentOffset(),
                        comp.getFieldName(),
                        comp.getDataType().getName(),
                        comp.getDefaultValueRepresentation()));
                }
                pw.println();
            }
        }
        println("wrote data dump to " + args[0]);
    }
}
