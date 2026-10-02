import ghidra.app.script.GhidraScript;
import ghidra.program.model.data.Category;
import ghidra.program.model.data.CategoryPath;
import ghidra.program.model.data.DataType;
import ghidra.program.model.data.DataTypeManager;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.DataIterator;

// args = one or more vtable shape names, as known_vtables.json's "name" spells them.
//
// Removes what ES2ApplyVtables built for a shape that known_vtables.json no longer defines (one that
// was renamed or dropped): the struct /ES2/<Name> and the slot function definitions in category
// /ES2/<Name>. ES2ApplyVtables resolves by name, so a renamed shape otherwise leaves its old struct
// and category behind, unused.
//
// Refuses a struct that is still laid down anywhere in the listing, so run ES2ApplyVtables first:
// it replaces the old struct at every instance with the renamed one.
public class ES2RemoveVtableType extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] names = getScriptArgs();
        if (names.length < 1) {
            println("Usage: ES2RemoveVtableType <VtableName> [...]");
            return;
        }
        DataTypeManager dtm = currentProgram.getDataTypeManager();
        int removed = 0, refused = 0, missing = 0;

        for (String name : names) {
            DataType struct = dtm.getDataType(new CategoryPath("/ES2"), name);
            if (struct != null) {
                int uses = 0;
                DataIterator it = currentProgram.getListing().getDefinedData(true);
                while (it.hasNext() && !monitor.isCancelled()) {
                    Data d = it.next();
                    if (d.getDataType().getDataTypePath().equals(struct.getDataTypePath())) {
                        uses++;
                        println("  still applied at " + d.getAddress());
                    }
                }
                if (uses > 0) {
                    println("REFUSED: /ES2/" + name + " is applied at " + uses + " address(es)");
                    refused++;
                    continue;
                }
                dtm.remove(struct, monitor);
                println("removed struct /ES2/" + name);
                removed++;
            }
            else {
                println("no struct /ES2/" + name);
                missing++;
            }

            Category parent = dtm.getCategory(new CategoryPath("/ES2"));
            Category slots = parent == null ? null : parent.getCategory(name);
            if (slots != null) {
                int n = 0;
                for (DataType dt : slots.getDataTypes()) {
                    dtm.remove(dt, monitor);
                    n++;
                }
                parent.removeEmptyCategory(name, monitor);
                println("removed category /ES2/" + name + " (" + n + " slot definitions)");
            }
        }
        println("ES2RemoveVtableType: removed=" + removed + " refused=" + refused + " missing=" + missing);
    }
}
