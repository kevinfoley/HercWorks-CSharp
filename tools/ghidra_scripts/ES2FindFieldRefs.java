import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.scalar.Scalar;
import java.io.BufferedReader;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.List;

// Finds every instruction whose operands carry a given scalar -- used to locate the readers and
// writers of a struct field by its displacement, which is the only handle on a field that no
// symbol names. args[0] = spec file. Spec: line 1 = output path, remaining lines = decimal or
// 0x-prefixed values.
public class ES2FindFieldRefs extends GhidraScript {
    @Override
    public void run() throws Exception {
        String specPath = getScriptArgs()[0];
        String outPath;
        List<Long> wanted = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(specPath))) {
            outPath = br.readLine();
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (line.isEmpty()) continue;
                wanted.add(line.startsWith("0x") ? Long.parseLong(line.substring(2), 16)
                                                 : Long.parseLong(line));
            }
        }
        try (PrintWriter pw = new PrintWriter(new FileWriter(outPath))) {
            for (Long w : wanted) {
                pw.println("=== scalar 0x" + Long.toHexString(w) + " ===");
                InstructionIterator it = currentProgram.getListing().getInstructions(true);
                int n = 0;
                while (it.hasNext()) {
                    Instruction ins = it.next();
                    boolean hit = false;
                    for (int i = 0; i < ins.getNumOperands() && !hit; i++) {
                        for (Object o : ins.getOpObjects(i)) {
                            if (o instanceof Scalar && ((Scalar) o).getUnsignedValue() == w) {
                                hit = true;
                                break;
                            }
                        }
                    }
                    if (!hit) continue;
                    Address a = ins.getAddress();
                    Function f = getFunctionContaining(a);
                    pw.println(String.format("%s  %-40s  %s", a, ins.toString(),
                            f == null ? "-" : f.getName() + "@" + f.getEntryPoint()));
                    n++;
                }
                pw.println("(" + n + " sites)");
                pw.println();
            }
        }
        println("wrote field refs to " + outPath);
    }
}
