#!/bin/sh
# Applies all three Ghidra knowledge files to the ES2Recon project, in the one order that works on
# a database that has never had them applied before.
#
#   ES2ApplyVtables      builds the vtable types
#   ES2ApplyStructures   builds the object structs (which reference the vtable types) and types the
#                        function parameters listed in known_structs.json
#   ES2ApplySymbolNames  renames functions, writes plate comments, applies signatures -- and a
#                        signature may name a struct type, which is why it goes last
#
# Only a database with no /ES2 types yet actually cares about the order: once the types exist they
# persist in the project, and from then on any of the three can be run on its own, in any order, as
# often as you like. All three are idempotent. Run this after a fresh import and auto-analysis;
# after that, run whichever one you need directly.
#
# Usage: sh tools/scripts/ghidra_apply_all.sh
set -e

GHIDRA="/e/ES2Stuff/tools/ghidra_12.1.2_PUBLIC/support/analyzeHeadless.bat"
PROJECT="E:\\ES2Stuff\\tools\\ghidra_project"
SCRIPTS="E:\\ES2Stuff\\tools\\ghidra_scripts"

for step in "ES2ApplyVtables known_vtables.json" \
            "ES2ApplyStructures known_structs.json" \
            "ES2ApplySymbolNames known_symbols.json"; do
    script=${step% *}
    json=${step#* }
    echo "=== $script"
    "$GHIDRA" "$PROJECT" ES2Recon -process DBSIM.EXE -noanalysis \
        -scriptPath "$SCRIPTS" -postScript "$script" "$SCRIPTS\\$json" 2>&1 \
        | grep -E "ERROR|WARN|$script\.java>" || true
done

echo
echo "Each line above ending 'errors=0' is a clean apply. A signature that fails to resolve a"
echo "struct type means the structures step did not run -- re-run this script."
