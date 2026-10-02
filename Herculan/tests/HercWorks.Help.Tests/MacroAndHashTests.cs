using Xunit;

namespace HercWorks.Help.Tests;

public class MacroAndHashTests {
	[Fact]
	public void NestedQuotesStayOneArgument() {
		var calls = HelpMacro.Parse("CB(`btn_quick',`Keys',`JI(`',`Quick_Reference')')");
		var call = Assert.Single(calls!);
		Assert.Equal("CB", call.Name);
		Assert.Equal(["btn_quick", "Keys", "JI(`',`Quick_Reference')"], call.Arguments);
	}

	[Fact]
	public void SeveralCallsAndBareArguments() {
		var calls = HelpMacro.Parse("FocusWindow(`main');CW(`overview')")!;
		Assert.Equal(["FocusWindow", "CW"], calls.Select(c => c.Name));
		Assert.Equal(["8355711"], HelpMacro.Parse("SPC(8355711)")![0].Arguments);
		Assert.Empty(HelpMacro.Parse("BrowseButtons()")![0].Arguments);
	}

	[Theory]
	[InlineData("JI(`',`x'")]
	[InlineData("JI(`',`x')junk")]
	[InlineData("(`x')")]
	[InlineData("JI(`unclosed)")]
	public void MalformedMacrosDoNotParse(string macro) => Assert.Null(HelpMacro.Parse(macro));

	[Fact]
	public void HashIsCaseInsensitiveAndRejectsUnknownCharacters() {
		Assert.True(ContextHash.TryCompute("quick_reference", out uint lower));
		Assert.True(ContextHash.TryCompute("Quick_Reference", out uint mixed));
		Assert.Equal(lower, mixed);
		Assert.False(ContextHash.TryCompute("a.b", out _));
		Assert.True(ContextHash.TryCompute("HUD_popup_Rotation_Indicator", out uint hud));
		Assert.Equal(0xd4deecc3u, hud);
	}
}
