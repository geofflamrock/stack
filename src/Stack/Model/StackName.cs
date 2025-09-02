using Vogen;

namespace Stack.Model;

[ValueObject<string>(stringComparers: StringComparersGeneration.Generate)]
public partial class StackName;
