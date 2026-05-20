namespace CamperoDesktop.Models;

public class ModuleNavigationState
{
    public required AppModule Module { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required object Content { get; init; }
}
