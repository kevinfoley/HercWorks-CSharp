import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionManager;
import ghidra.program.model.listing.Parameter;
import ghidra.program.model.symbol.SourceType;
import java.io.FileReader;
import java.io.PrintWriter;

// args[0] = path to known_symbols.json
// args[1] = path to write the signature dump (TSV)
//
// Read-only companion to ES2ApplySymbolNames: instead of pushing names INTO the Ghidra database,
// this pulls committed function prototypes OUT of it, for the addresses known_symbols.json already
// tracks. Nothing in the program is modified.
//
// For every "function" entry whose "binary" matches the current program, emits one TSV row:
//   address  tier  paramCount  namedParamCount  prototype
//
// "tier" records how the prototype got there, which is the whole point of the dump. ES2CommitAllParams
// mass-commits decompiler-inferred prototypes with SourceType.ANALYSIS, so the database is full of
// plausible-looking signatures nobody ever checked. Only USER_DEFINED/IMPORTED reflects a human
// decision:
//   verified  - signature source is USER_DEFINED/IMPORTED, or at least one parameter is
//   analysis  - decompiler-inferred (ES2CommitAllParams and friends)
//   default   - Ghidra never got past its own defaults
// namedParamCount counts parameters whose name is not Ghidra's own "param_N"/"unnamedN"/"argN"
// filler, so a verified-but-unnamed prototype can be told apart from a genuinely annotated one.
public class ES2DumpSignatures extends GhidraScript {

    private static boolean isHuman(SourceType s) {
        return s == SourceType.USER_DEFINED || s == SourceType.IMPORTED;
    }

    private static boolean isFillerName(String n) {
        if (n == null || n.isEmpty()) return true;
        if (n.equals("this")) return true;
        if (n.startsWith("in_")) return true;
        String stem = null;
        if (n.startsWith("param_")) stem = n.substring(6);
        else if (n.startsWith("unnamed")) stem = n.substring(7);
        else if (n.startsWith("arg_")) stem = n.substring(4);
        else if (n.startsWith("arg")) stem = n.substring(3);
        if (stem == null) return false;
        if (stem.isEmpty()) return true;
        for (int i = 0; i < stem.length(); i++) {
            if (!Character.isDigit(stem.charAt(i))) return false;
        }
        return true;
    }

    @Override
    public void run() throws Exception {
        String[] scriptArgs = getScriptArgs();
        if (scriptArgs.length < 2) {
            println("Usage: ES2DumpSignatures <known_symbols.json path> <output tsv path>");
            return;
        }

        JsonObject root;
        try (FileReader fr = new FileReader(scriptArgs[0])) {
            root = JsonParser.parseReader(fr).getAsJsonObject();
        }
        JsonArray entries = root.getAsJsonArray("entries");

        String progName = currentProgram.getName().toUpperCase();
        FunctionManager fm = currentProgram.getFunctionManager();

        int verified = 0, analysis = 0, deflt = 0, noTarget = 0, notFunction = 0, otherBinary = 0;

        try (PrintWriter out = new PrintWriter(scriptArgs[1], "UTF-8")) {
            out.println("# address\ttier\tparamCount\tnamedParamCount\tprototype");
            for (JsonElement el : entries) {
                JsonObject entry = el.getAsJsonObject();
                if (!progName.contains(entry.get("binary").getAsString().toUpperCase())) {
                    otherBinary++;
                    continue;
                }
                if (!"function".equals(entry.get("type").getAsString())) {
                    notFunction++;
                    continue;
                }

                String addrHex = entry.get("address").getAsString();
                Address addr;
                try {
                    addr = currentProgram.getAddressFactory().getAddress(addrHex);
                } catch (Exception e) {
                    println("ERROR: bad address '" + addrHex + "': " + e.getMessage());
                    continue;
                }

                Function f = fm.getFunctionAt(addr);
                if (f == null) {
                    println("WARN: no function at " + addrHex + " -- skipping");
                    noTarget++;
                    continue;
                }

                Parameter[] params = f.getParameters();
                boolean human = isHuman(f.getSignatureSource());
                int named = 0;
                for (Parameter p : params) {
                    if (isHuman(p.getSource())) human = true;
                    if (!isFillerName(p.getName())) named++;
                }

                String tier;
                if (human) {
                    tier = "verified";
                    verified++;
                } else if (f.getSignatureSource() == SourceType.ANALYSIS) {
                    tier = "analysis";
                    analysis++;
                } else {
                    tier = "default";
                    deflt++;
                }

                String proto = f.getSignature().getPrototypeString(true).replace('\t', ' ').trim();
                out.println(addrHex + "\t" + tier + "\t" + params.length + "\t" + named + "\t" + proto);
            }
        }

        println(String.format(
            "ES2DumpSignatures [%s]: verified=%d analysis=%d default=%d skipped(no function)=%d "
                + "skipped(data entries)=%d skipped(other binary)=%d",
            progName, verified, analysis, deflt, noTarget, notFunction, otherBinary));
    }
}
