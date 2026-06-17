namespace AspNetDebugDashboard.Suite;

/// <summary>
/// One tool in the AspNet* suite, shown in the shared navigation sidebar.
/// Each tool registers its own via <c>AddSuitePanel</c>; every tool then renders links to all of them.
/// </summary>
/// <param name="Name">Display label, e.g. "Flags".</param>
/// <param name="Route">Base path the tool serves, e.g. "/_flags".</param>
/// <param name="Icon">Inline SVG markup for the sidebar glyph (stroke, currentColor).</param>
/// <param name="Order">Sort order in the sidebar (dashboard 0, then the tools).</param>
public sealed record SuitePanel(string Name, string Route, string Icon, int Order);
