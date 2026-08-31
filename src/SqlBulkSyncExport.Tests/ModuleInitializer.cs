using System.Runtime.CompilerServices;

namespace SqlBulkSyncExport.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyDiffPlex.Initialize();
        VerifierSettings.UseStrictJson();
    }
}
