using System.Collections;
using System.Reflection;

namespace HercWorks.UI;

/// <summary>
/// Turns any parsed object graph (a DataFile subclass from HercWorks.Core, and everything it
/// references — nested classes, arrays, dictionaries) into a read-only TreeView, for the VOL
/// browser's Content panel (below the fixed-height Metadata list in MainForm).
/// Generic/reflection-based rather than hand-built per file type, since there are ~30 different
/// parsed shapes and growing. Every node is expanded by default (via TreeView.ExpandAll()) so the
/// whole parsed structure is visible without manual clicking — can get long for a deeply nested
/// or large-array file, but that's the tradeoff for not having to expand node-by-node.
///
/// Leaf-vs-expand decision: a value is shown as a single leaf line (via its own ToString()) if
/// it's a primitive/enum/string, a byte[] (shown as a length + hex preview instead of one leaf
/// per byte), or a type that overrides object.ToString() — this last rule is what makes the
/// Java-enum-style lookup classes (WeaponLUT, HercLUT, MissileType, etc., most of which override
/// ToString() to return their display Name) render as a clean single value instead of expanding
/// into their Id/Name/etc. fields. Everything else (plain data classes that don't override
/// ToString(), which is every DataFile-derived class and its nested Entry-style structs) gets
/// expanded into its public instance properties recursively.
/// </summary>
public static class ContentTreeRenderer {
	private const int MaxDepth = 20;
	private const int MaxCollectionItems = 500;
	private const int BytePreviewLength = 24;

	// Base DataFile plumbing already shown in the browser's Metadata list — skip to keep the
	// Content panel focused on the file's actual parsed data.
	private static readonly HashSet<string> SkippedPropertyNames = new(StringComparer.OrdinalIgnoreCase) {
		"RawBytes", "Header", "FileSize", "GameDirPath", "FilePath"
	};

	public static void Populate(TreeView tree, string rootLabel, object root) {
		tree.Nodes.Clear();
		var node = BuildNode(rootLabel, root, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
		tree.Nodes.Add(node);
		tree.ExpandAll();

		// ExpandAll() tends to leave the scroll position wherever the last-expanded node ended
		// up (often the bottom of a large tree) — force it back to showing the root at the top.
		node.EnsureVisible();
	}

	private static TreeNode BuildNode(string label, object? value, int depth, HashSet<object> ancestors) {
		if (value == null) {
			return new TreeNode($"{label}: null");
		}

		if (depth > MaxDepth) {
			return new TreeNode($"{label}: ... (max depth reached)");
		}

		if (value is byte[] byteArr) {
			return new TreeNode($"{label}: byte[{byteArr.Length}] {SummarizeBytes(byteArr)}");
		}

		var type = value.GetType();

		if (IsSimpleLeaf(type) || OverridesToString(type)) {
			return new TreeNode($"{label}: {value}");
		}

		if (!ancestors.Add(value)) {
			return new TreeNode($"{label}: (already shown above an ancestor node — circular reference)");
		}

		try {
			if (value is IDictionary dict) {
				var node = new TreeNode($"{label} ({dict.Count} entries)");
				foreach (DictionaryEntry kv in dict) {
					node.Nodes.Add(BuildNode(kv.Key?.ToString() ?? "null", kv.Value, depth + 1, ancestors));
				}
				return node;
			}

			if (value is IEnumerable enumerable) {
				var node = new TreeNode(label);
				int i = 0;
				foreach (var item in enumerable) {
					if (i >= MaxCollectionItems) {
						node.Nodes.Add(new TreeNode($"... (truncated after {MaxCollectionItems} items)"));
						break;
					}
					node.Nodes.Add(BuildNode($"[{i}]", item, depth + 1, ancestors));
					i++;
				}
				node.Text = $"{label} ({i} item{(i == 1 ? "" : "s")})";
				return node;
			}

			var objNode = new TreeNode(label);
			foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
				if (prop.GetIndexParameters().Length > 0 || SkippedPropertyNames.Contains(prop.Name)) {
					continue;
				}

				object? propValue;
				try {
					propValue = prop.GetValue(value);
				} catch (Exception ex) {
					objNode.Nodes.Add(new TreeNode($"{prop.Name}: (error reading property: {ex.Message})"));
					continue;
				}
				objNode.Nodes.Add(BuildNode(prop.Name, propValue, depth + 1, ancestors));
			}
			return objNode;
		} finally {
			ancestors.Remove(value);
		}
	}

	private static bool IsSimpleLeaf(Type type) {
		return type.IsEnum
			|| type == typeof(string)
			|| type == typeof(bool)
			|| type == typeof(byte) || type == typeof(sbyte)
			|| type == typeof(short) || type == typeof(ushort)
			|| type == typeof(int) || type == typeof(uint)
			|| type == typeof(long) || type == typeof(ulong)
			|| type == typeof(float) || type == typeof(double) || type == typeof(decimal)
			|| type == typeof(char)
			|| type == typeof(DateTime) || type == typeof(Guid);
	}

	/// <summary>
	/// True if this type provides its own ToString() (a Java-enum-style lookup class like
	/// WeaponLUT, or a struct like System.Drawing.Point/Color) rather than inheriting the
	/// unhelpful default from object/ValueType.
	/// </summary>
	private static bool OverridesToString(Type type) {
		var method = type.GetMethod("ToString", Type.EmptyTypes);
		return method != null && method.DeclaringType != typeof(object) && method.DeclaringType != typeof(ValueType);
	}

	private static string SummarizeBytes(byte[] bytes) {
		if (bytes.Length == 0) {
			return "(empty)";
		}

		int previewLen = Math.Min(bytes.Length, BytePreviewLength);
		string hex = Convert.ToHexString(bytes, 0, previewLen);
		string spaced = string.Join(' ', Enumerable.Range(0, previewLen).Select(i => hex.Substring(i * 2, 2)));

		return bytes.Length > previewLen ? $"{spaced} ..." : spaced;
	}
}
