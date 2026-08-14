# Find xrefs to CD-message / eggplant / dummy strings
# @category Search

from ghidra.program.model.address import Address

def dump_xrefs(label, va):
    addr = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(va)
    print("\n\n===== %s @ %s =====" % (label, addr))
    refs = currentProgram.getReferenceManager().getReferencesTo(addr)
    count = 0
    for ref in refs:
        count += 1
        frm = ref.getFromAddress()
        print("-- xref #%d from %s (%s) --" % (count, frm, ref.getReferenceType()))
        func = currentProgram.getFunctionManager().getFunctionContaining(frm)
        if func is not None:
            print("   in function: %s @ %s" % (func.getName(), func.getEntryPoint()))
        instr = currentProgram.getListing().getInstructionAt(frm)
        if instr is not None:
            ctx = instr
            for i in range(8):
                prev = currentProgram.getListing().getInstructionBefore(ctx)
                if prev is None:
                    break
                ctx = prev
            walk = ctx
            for i in range(20):
                if walk is None:
                    break
                marker = " <=== XREF" if walk.getAddress().equals(frm) else ""
                print("     %s: %s%s" % (walk.getAddress(), walk.toString(), marker))
                nxt = currentProgram.getListing().getInstructionAfter(walk)
                walk = nxt
    if count == 0:
        print("   (no code xrefs found)")

base = currentProgram.getImageBase().getOffset()
print("Image base: 0x%x" % base)

dump_xrefs("CD message copy 1 (file 0x6B4BD)", base + 0x6C4BD)
dump_xrefs("CD message copy 2 (file 0x70430)", base + 0x71430)
dump_xrefs("-eggplant copy 1 (file 0x6B22B)", base + 0x6C22B)
dump_xrefs("-eggplant copy 2 (file 0x7020A)", base + 0x7120A)
dump_xrefs("dummy (file 0x70204)", base + 0x71204)
