namespace DriveCrawl.Tests.TestSupport;

/// <summary>
/// Tests tagged with this trait pin behaviour that looks like a bug. They stay green on purpose, so a fix
/// shows up as a deliberate test change. Run them with: dotnet test --filter "Behaviour=SuspectedBug"
/// </summary>
public static class TestTraits
{
    public const string Behaviour = "Behaviour";
    public const string SuspectedBug = "SuspectedBug";
}
