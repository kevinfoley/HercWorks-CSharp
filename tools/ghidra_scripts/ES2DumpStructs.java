import ghidra.app.script.GhidraScript;
import ghidra.program.model.data.Category;
import ghidra.program.model.data.CategoryPath;
import ghidra.program.model.data.DataType;
import ghidra.program.model.data.DataTypeComponent;
import ghidra.program.model.data.DataTypeManager;
import ghidra.program.model.data.FunctionDefinition;
import ghidra.program.model.data.Pointer;
import ghidra.program.model.data.Structure;
import java.io.BufferedWriter;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.List;

// Dumps the object structures under the /ES2 data-type category of the current program -- the
// layouts applied from known_structs.json. The vtable structs ES2ApplyVtables puts in the same
// category (every field a pointer to a function definition) are skipped; ES2DumpAllVtables owns them.
// One block per structure, one line per defined field (undefined filler is skipped).
// args[0] = output path.
public class ES2DumpStructs extends GhidraScript {

    @Override
    public void run() throws Exception {
        String outPath = getScriptArgs()[0];
        DataTypeManager dtm = currentProgram.getDataTypeManager();
        Category root = dtm.getCategory(new CategoryPath("/ES2"));

        List<Structure> structs = new ArrayList<>();
        if (root != null) collect(root, structs);
        structs.sort((a, b) -> a.getName().compareToIgnoreCase(b.getName()));

        try (PrintWriter pw = new PrintWriter(new BufferedWriter(new FileWriter(outPath), 1 << 18))) {
            pw.println("Structures under /ES2 in " + currentProgram.getName());
            pw.println();
            for (Structure s : structs) {
                monitor.checkCancelled();
                pw.println("=== " + s.getName() + "  (size 0x" + Integer.toHexString(s.getLength())
                        + ", " + s.getNumDefinedComponents() + " fields) ===");
                if (s.getDescription() != null && !s.getDescription().isEmpty())
                    pw.println("  " + s.getDescription());
                for (DataTypeComponent c : s.getDefinedComponents()) {
                    String field = c.getFieldName() != null ? c.getFieldName() : "(unnamed)";
                    DataType t = c.getDataType();
                    String line = String.format("  +0x%03x  %-28s %s  [%d]", c.getOffset(), field,
                            t.getDisplayName(), c.getLength());
                    if (c.getComment() != null && !c.getComment().isEmpty())
                        line += "  // " + c.getComment().replace('\n', ' ');
                    pw.println(line);
                }
                pw.println();
            }
            pw.println("structures: " + structs.size());
        }
        println("SCRIPT-OK wrote " + structs.size() + " structures to " + outPath);
    }

    private void collect(Category c, List<Structure> out) {
        for (DataType dt : c.getDataTypes()) {
            if (dt instanceof Structure && !isVtable((Structure) dt)) out.add((Structure) dt);
        }
        for (Category sub : c.getCategories()) collect(sub, out);
    }

    private boolean isVtable(Structure s) {
        DataTypeComponent[] fields = s.getDefinedComponents();
        if (fields.length == 0) return false;
        for (DataTypeComponent c : fields) {
            if (!(c.getDataType() instanceof Pointer p) || !(p.getDataType() instanceof FunctionDefinition))
                return false;
        }
        return true;
    }
}
