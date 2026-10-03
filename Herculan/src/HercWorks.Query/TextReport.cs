namespace HercWorks.Query;

/// <summary>The plain-text form of each query's result.</summary>
internal static class TextReport {
	public static void Types(TextWriter o, RetailData data, TypeQueryResult result, bool variants) {
		string noun = Roster.Noun(result.Roster);
		string types = string.Join(", ", result.Types.Select((t, i) => Label(t, result.TypeNames[i])));
		o.WriteLine($"Row #{result.Row} ({noun}) type {types}: {result.Missions.Count} of {result.MissionsSearched} missions, " +
			$"{result.Records} records, {result.Placed} placed by a group.");
		if (result.Missions.Count == 0) {
			return;
		}

		o.WriteLine();
		o.WriteLine("Mission   Records  Placed  Deploy-gated  Cond-gated  Sides");
		foreach (var m in result.Missions) {
			o.WriteLine($"{m.Mission,-9} {m.Records,7}  {m.Placed,6}  {m.DeploymentGated,12}  {m.ConditionGated,10}  {string.Join(", ", m.Sides.Select(FlagQuery.Side))}");
		}

		foreach (var m in result.Missions) {
			o.WriteLine();
			o.WriteLine($"{m.Mission}");
			foreach (var hit in m.Hits) {
				var line = new List<string> { hit.Guid == -1 ? $"#{hit.Index} (no GUID)" : $"GUID {hit.Guid}" };
				if (result.Types.Count > 1 || !result.Types.Contains(hit.TypeIndex)) {
					line.Add(Label(hit.TypeIndex, hit.TypeName));
				}

				if (hit.StartingCondition is { } start) {
					line.Add($"start {start}%");
				}

				if (hit.ConditionRef != -1) {
					line.Add(Cond(hit.ConditionRef, hit.Condition, variants));
				}

				if (hit.VariantKey != -1) {
					line.Add($"variant key {hit.VariantKey}: type and payload drawn");
				}

				if (hit.VariantSourceKey is { } key) {
					line.Add($"variant of key {key}, drawn by {(hit.DrawnBy.Count == 0 ? "nothing" : "GUID " + string.Join(", ", hit.DrawnBy))}");
				}

				o.WriteLine("  " + string.Join(", ", line));

				if (hit.PlacedBy.Count == 0) {
					o.WriteLine("    not placed by any group");
				}

				foreach (var g in hit.PlacedBy) {
					var parts = new List<string> { $"placed by group {g.Guid} slot {g.Slot}", FlagQuery.Side(g.Side) };
					if (g.DeploymentGated) {
						parts.Add($"deploys on action {g.DeploymentActionRef}");
					}

					if (g.ConditionRef != -1) {
						parts.Add(Cond(g.ConditionRef, g.Condition, variants));
					}

					if (g.FirstRecord) {
						parts.Add("row #16's first record, whose members the simulator does not read");
					}

					o.WriteLine("    " + string.Join(", ", parts));
				}

				foreach (var s in hit.SameGuid) {
					var parts = new List<string> { $"same GUID, #{s.Index}" };
					parts.Add(s.ConditionRef == -1 ? "unconditioned" : Cond(s.ConditionRef, s.Condition, variants));
					parts.Add(s.TypeIndex == -1 ? "type unchanged" : Label(s.TypeIndex, data.TypeName(result.Roster, s.TypeIndex)));
					if (s.StartingCondition is { } sc) {
						parts.Add($"start {sc}%");
					}

					o.WriteLine("    " + string.Join(", ", parts));
				}
			}
		}
	}

	public static void Flag(TextWriter o, RetailData data, FlagQueryResult result) {
		o.WriteLine($"Campaign flag {result.Flag} across {result.MissionsSearched} missions: " +
			$"{result.Tests.Count} tests, {result.Writes.Count} writes.");

		o.WriteLine();
		o.WriteLine("Tested by");
		if (result.Tests.Count == 0) {
			o.WriteLine("  nothing");
		}

		foreach (var t in result.Tests) {
			o.WriteLine($"  {t.Mission,-9} cond {t.ConditionGuid} (row #1 #{t.Index}): {t.Chain}");
			o.WriteLine(t.Gates.Count == 0
				? "            gates nothing"
				: "            gates " + string.Join(", ", t.Gates.Select(g => g.Label)));
		}

		o.WriteLine();
		o.WriteLine("Written by");
		if (result.Writes.Count == 0) {
			o.WriteLine("  nothing");
		}

		foreach (var w in result.Writes) {
			string condition = w.Condition == null ? "" : $" [{w.Condition}]";
			o.WriteLine($"  {w.Mission,-9} row #{w.Row} {w.Subject}: {w.Effect}{condition}");
		}
	}

	public static void Missions(TextWriter o, RetailData data, IReadOnlyList<MissionSummary> rows) {
		o.WriteLine($"{rows.Count} missions in {data.MissionArchivePath}");
		o.WriteLine();
		o.WriteLine("Mission   Row#1  Row#12  Row#13  Row#14  Row#16  ENG");
		foreach (var r in rows) {
			o.WriteLine($"{r.Mission,-9} {r.Conditions,5}  {r.Mechs,6}  {r.Flyers,6}  {r.Bases,6}  {r.Groups,6}  {(r.TextRecords?.ToString() ?? "none")}");
		}

		o.WriteLine($"{"Total",-9} {rows.Sum(r => r.Conditions),5}  {rows.Sum(r => r.Mechs),6}  {rows.Sum(r => r.Flyers),6}  " +
			$"{rows.Sum(r => r.Bases),6}  {rows.Sum(r => r.Groups),6}  {rows.Sum(r => r.TextRecords ?? 0)}");
	}

	private static string Label(int type, string? name) => name == null ? $"0x{type:x2}" : $"0x{type:x2} {name}";

	private static string Cond(short guid, string? text, bool decode) =>
		decode && text != null ? $"cond {guid} ({text})" : $"cond {guid}";
}
