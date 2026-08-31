using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Tests;

public sealed class OutputFileNamesTests
{
    [Fact]
    public void ValidateFileName_AcceptsBasenameWithExtension()
    {
        Assert.Equal("customers.csv", OutputFileNames.ValidateFileName("customers.csv", "targetFile"));
    }

    [Theory]
    [InlineData("folder/customers.csv")]
    [InlineData("folder\\customers.csv")]
    [InlineData("..\\customers.csv")]
    [InlineData("customers")]
    public void ValidateFileName_RejectsInvalid(string value)
    {
        Assert.Throws<ArgumentException>(() => OutputFileNames.ValidateFileName(value, "targetFile"));
    }

    [Fact]
    public void DefaultDeletedFile_UsesDeletedBeforeExtension()
    {
        Assert.Equal("customers.deleted.csv", OutputFileNames.DefaultDeletedFile("customers.csv"));
    }

    [Fact]
    public void InsertBeforeExtension_InsertsToken()
    {
        Assert.Equal(
            "customers_20260101120000_1_2.csv",
            OutputFileNames.InsertBeforeExtension("customers.csv", "20260101120000_1_2"));
    }
}
