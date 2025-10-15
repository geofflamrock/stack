namespace Stack.Persistence;

public static class StackConstants
{
    public const string StackMarkerText = "stack-pr-list";
    public const string StackMarkerStart = $"<!-- {StackMarkerText} -->";
    public const string StackMarkerEnd = $"<!-- /{StackMarkerText} -->";
    public const string StackMarkerDescription = $"<!-- The contents of the section between the {StackConstants.StackMarkerText} markers will be replaced with list of pull requests in the stack when there is more than one pull request. Move this section around as you would like or delete it to not include the list of pull requests.  -->";
}
