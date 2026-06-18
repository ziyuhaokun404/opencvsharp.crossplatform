using System.Collections.Generic;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Domain.Operators;

public sealed record ImageOperatorDescriptor(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<OperatorParameter> Parameters,
    bool SupportsClamp)
{
    public OperatorParameter? PrimaryParameter => Parameters.Count > 0 ? Parameters[0] : null;

    public OperatorParameter? SecondaryParameter => Parameters.Count > 1 ? Parameters[1] : null;
}
