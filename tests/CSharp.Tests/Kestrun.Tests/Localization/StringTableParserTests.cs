using Kestrun.Localization;
using Xunit;

namespace KestrunTests.Localization;

public class StringTableParserTests
{
    [Fact]
    [Trait("Category", "Localization")]
    public void ParseFile_Ignores_Comments_And_Blanks_And_Splits_On_First_Equals()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(temp.FullName, "Messages.psd1");
            var content = string.Join(Environment.NewLine, new[]
            {
                "@{",
                "# Comment",
                "// Another comment",
                "",
                "Key = \"Value\"",
                "Key2=\"Value=With=Equals\"",
                "  Key3   =   \"Value with spaces\"  ",
                "Labels = @{",
                "  Save = \"Save\"",
                "  Cancel = \"Cancel\"",
                "}",
                "NoEqualsHere",
                "}",
            });
            File.WriteAllText(path, content);

            var map = StringTableParser.ParseFile(path);

            Assert.Equal("Value", map["Key"]);
            Assert.Equal("Value=With=Equals", map["Key2"]);
            Assert.Equal("Value with spaces", map["Key3"]);
            Assert.Equal("Save", map["Labels.Save"]);
            Assert.Equal("Cancel", map["Labels.Cancel"]);
            Assert.Equal(5, map.Count);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}
