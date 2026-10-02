using Xunit;

namespace HercWorks.Help.Tests;

/// <summary>
/// The three retail help files, for the tests that check against them. Each such test passes
/// vacuously when its file is absent, so the suite does not depend on an install.
/// </summary>
internal static class RetailManuals {
	public static readonly string[] Languages = ["ENGLISH", "FRENCH", "GERMAN"];

	/// <summary>Walks up from this assembly looking for <c>ES2/&lt;language&gt;/ES2GUIDE.HLP</c>.</summary>
	public static byte[]? Read(string language) {
		DirectoryInfo? at = new(AppContext.BaseDirectory);
		while (at is not null) {
			string candidate = Path.Combine(at.FullName, "ES2", language, "ES2GUIDE.HLP");
			if (File.Exists(candidate)) {
				return File.ReadAllBytes(candidate);
			}

			at = at.Parent;
		}

		return null;
	}

	public static TheoryData<string> All() {
		var data = new TheoryData<string>();
		foreach (string language in Languages) {
			data.Add(language);
		}

		return data;
	}
}
