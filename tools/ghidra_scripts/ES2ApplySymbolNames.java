import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import ghidra.app.cmd.function.ApplyFunctionSignatureCmd;
import ghidra.app.script.GhidraScript;
import ghidra.app.util.parser.FunctionSignatureParser;
import ghidra.program.model.address.Address;
import ghidra.program.model.data.FunctionDefinitionDataType;
import ghidra.program.model.listing.CodeUnit;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionManager;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.symbol.SourceType;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolTable;
import java.io.FileReader;
import java.util.HashSet;
import java.util.Set;

// args[0] = path to known_symbols.json (see that file's _readme for the schema).
//
// Applies confirmed address->meaning findings to the currently-processed program. Reads every
// entry whose "binary" field is a case-insensitive substring of the current program's name (so
// running this with -process "VSHELL.EXE" only touches VSHELL entries, and likewise for DBSIM),
// and for each:
//   - type "function": renames the function at that address (Function.setName, USER_DEFINED) if
//     the entry has a "name", then always writes/refreshes a plate comment with the description.
//   - type "data": creates/refreshes a label at that address if the entry has a "name", then
//     always writes/refreshes a plate comment.
// An entry may also carry a "signature": a full C prototype, which is applied to the function as
// SourceType.USER_DEFINED so it survives as a human-verified prototype rather than blending into
// the decompiler's ANALYSIS-tier guesses. Signatures are functions-only and optional -- see the
// known_symbols.json _readme for the rule on when one may be recorded at all. A signature that
// fails to parse or apply is reported and skipped; it never aborts the rest of the run.
//
// Entries with no "name" (low-confidence findings) only get the plate comment -- never a
// rename/label, so a guess can never masquerade as a confirmed symbol in the database.
//
// Idempotent: safe to re-run after known_symbols.json gains new entries or existing descriptions
// change -- renames/labels are skipped if already correct, and plate comments are always
// overwritten with the current JSON content rather than appended to.
public class ES2ApplySymbolNames extends GhidraScript {
    // Calling conventions that may appear in a known_symbols.json "signature" string.
    private static final String[] CONVENTIONS = {
        "__cdecl", "__stdcall", "__thiscall", "__fastcall", "__watcall" };

    @Override
    public void run() throws Exception {
        String[] scriptArgs = getScriptArgs();
        if (scriptArgs.length < 1) {
            println("Usage: ES2ApplySymbolNames <known_symbols.json path>");
            return;
        }
        String jsonPath = scriptArgs[0];

        JsonObject root;
        try (FileReader fr = new FileReader(jsonPath)) {
            root = JsonParser.parseReader(fr).getAsJsonObject();
        }
        JsonArray entries = root.getAsJsonArray("entries");

        String progName = currentProgram.getName().toUpperCase();
        FunctionManager fm = currentProgram.getFunctionManager();
        SymbolTable st = currentProgram.getSymbolTable();
        Listing listing = currentProgram.getListing();

        int renamed = 0, signatured = 0, labeled = 0, commented = 0, skippedOtherBinary = 0, skippedNoTarget = 0, errors = 0;

        // One address, one entry. Two entries sharing an address make the plate comment depend on
        // file order (the later one wins outright), and if their names differ the run can never
        // reach a fixed point -- every pass renames the target twice, so renamed never falls to 0.
        Set<String> seenAddresses = new HashSet<>();
        int duplicates = 0;
        for (JsonElement el : entries) {
            JsonObject entry = el.getAsJsonObject();
            String key = entry.get("binary").getAsString().toUpperCase() + ":"
                + entry.get("address").getAsString().toLowerCase();
            if (!seenAddresses.add(key)) {
                println("ERROR: duplicate entry for " + key + " -- merge them into one.");
                duplicates++;
            }
        }
        if (duplicates > 0) {
            println("ES2ApplySymbolNames: aborting, " + duplicates + " duplicate address(es) in " + jsonPath);
            return;
        }

        for (JsonElement el : entries) {
            JsonObject entry = el.getAsJsonObject();
            String binary = entry.get("binary").getAsString();
            if (!progName.contains(binary.toUpperCase())) {
                skippedOtherBinary++;
                continue;
            }

            String addrHex = entry.get("address").getAsString();
            String type = entry.get("type").getAsString();
            String confidence = entry.has("confidence") ? entry.get("confidence").getAsString() : "unknown";
            String description = entry.has("description") ? entry.get("description").getAsString() : "";
            String source = entry.has("source") ? entry.get("source").getAsString() : "";
            String name = entry.has("name") ? entry.get("name").getAsString() : null;
            String signature = entry.has("signature") ? entry.get("signature").getAsString() : null;

            Address addr;
            try {
                addr = currentProgram.getAddressFactory().getAddress(addrHex);
            } catch (Exception e) {
                println("ERROR: bad address '" + addrHex + "' (binary=" + binary + "): " + e.getMessage());
                errors++;
                continue;
            }

            StringBuilder comment = new StringBuilder();
            comment.append("[known_symbols.json] confidence=").append(confidence);
            if (name != null) {
                comment.append(" name=").append(name);
            }
            if (!description.isEmpty()) {
                comment.append("\n").append(description);
            }
            if (!source.isEmpty()) {
                comment.append("\nsource: ").append(source);
            }

            try {
                if ("function".equals(type)) {
                    Function f = fm.getFunctionAt(addr);
                    if (f == null) {
                        println("WARN: no function at " + addr + " (" + (name != null ? name : "comment-only") + ") -- skipping");
                        skippedNoTarget++;
                        continue;
                    }
                    if (name != null && !name.equals(f.getName())) {
                        f.setName(name, SourceType.USER_DEFINED);
                        renamed++;
                    }
                    if (signature != null) {
                        try {
                            // FunctionSignatureParser does not accept an inline calling convention:
                            // it reads "int __cdecl" as the return type and fails. Lift the
                            // convention out of the text and set it on the definition instead.
                            String conv = null;
                            String parseText = signature;
                            for (String c : CONVENTIONS) {
                                int idx = signature.indexOf(" " + c + " ");
                                if (idx >= 0) {
                                    conv = c;
                                    parseText = signature.substring(0, idx) + " "
                                        + signature.substring(idx + c.length() + 2);
                                    break;
                                }
                            }
                            FunctionSignatureParser parser =
                                new FunctionSignatureParser(currentProgram.getDataTypeManager(), null);
                            FunctionDefinitionDataType def = parser.parse(f.getSignature(), parseText);
                            if (conv != null) {
                                def.setCallingConvention(conv);
                            }
                            // preserveCallingConvention=false, or the convention just set is ignored
                            // and the function keeps the decompiler's (wrong) __stdcall guess.
                            // forceSetName=false: the rename above already handled the name.
                            ApplyFunctionSignatureCmd cmd = new ApplyFunctionSignatureCmd(
                                addr, def, SourceType.USER_DEFINED, false, false);
                            if (cmd.applyTo(currentProgram, monitor)) {
                                signatured++;
                            } else {
                                println("WARN: signature rejected at " + addr + ": " + cmd.getStatusMsg());
                            }
                        } catch (Exception e) {
                            println("ERROR: bad signature at " + addr + " (" + signature + "): " + e.getMessage());
                            errors++;
                        }
                    }
                    listing.setComment(addr, CodeUnit.PLATE_COMMENT, comment.toString());
                    commented++;
                } else if ("data".equals(type)) {
                    if (signature != null) {
                        println("WARN: entry at " + addr + " is type 'data' but carries a signature -- ignored");
                    }
                    if (name != null) {
                        Symbol existing = st.getPrimarySymbol(addr);
                        if (existing == null || !name.equals(existing.getName())) {
                            Symbol created = st.createLabel(addr, name, SourceType.USER_DEFINED);
                            created.setPrimary();
                            labeled++;
                        }
                    }
                    listing.setComment(addr, CodeUnit.PLATE_COMMENT, comment.toString());
                    commented++;
                } else {
                    println("WARN: unknown entry type '" + type + "' at " + addr);
                    skippedNoTarget++;
                }
            } catch (Exception e) {
                println("ERROR applying entry at " + addr + " (binary=" + binary + "): " + e.getMessage());
                errors++;
            }
        }

        println(String.format(
            "ES2ApplySymbolNames [%s]: renamed=%d signatured=%d labeled=%d commented=%d skipped(other binary)=%d skipped(no target)=%d errors=%d",
            progName, renamed, signatured, labeled, commented, skippedOtherBinary, skippedNoTarget, errors));
    }
}
