using System.Runtime.CompilerServices;

namespace ConcordIO.Tool.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Configure Verify to use a central Snapshots directory
        VerifyDiffPlex.Initialize();
        Verifier.UseProjectRelativeDirectory("Snapshots");
    }
}
