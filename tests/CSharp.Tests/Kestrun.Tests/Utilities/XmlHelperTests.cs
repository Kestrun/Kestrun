using Kestrun.Utilities;
using System.Collections;
using System.Xml.Linq;
using Xunit;

namespace KestrunTests.Utilities;

public class XmlHelperTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void ToXml_Null_ReturnsNilElement()
    {
        var elem = XmlHelper.ToXml("Value", null);
        Assert.Equal("Value", elem.Name);
        Assert.Equal("true", elem.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"))?.Value);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToXml_Primitive_ReturnsElementWithValue()
    {
        var elem = XmlHelper.ToXml("Number", 42);
        Assert.Equal("42", elem.Value);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToXml_Dictionary_ReturnsNestedElements()
    {
        var dict = new Hashtable { { "A", 1 }, { "B", 2 } };
        var elem = XmlHelper.ToXml("Dict", dict);
        Assert.Equal("1", elem.Element("A")?.Value);
        Assert.Equal("2", elem.Element("B")?.Value);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToXml_List_ReturnsItemElements()
    {
        var list = new List<int> { 1, 2 };
        var elem = XmlHelper.ToXml("List", list);
        Assert.Collection(elem.Elements(), e => Assert.Equal("1", e.Value), e => Assert.Equal("2", e.Value));
    }

    private class Sample
    {
        public string Name { get; set; } = "Foo";
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToXml_Object_UsesProperties()
    {
        var sample = new Sample();
        var elem = XmlHelper.ToXml("Sample", sample);
        Assert.Equal("Foo", elem.Element("Name")?.Value);
    }

    private sealed class ProductWithXmlMetadata
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string[] Items { get; set; } = [];

        public static Hashtable XmlMetadata { get; } = new()
        {
            ["ClassName"] = "Product",
            ["Properties"] = new Hashtable
            {
                ["Id"] = new Hashtable
                {
                    ["Name"] = "id",
                    ["Attribute"] = true,
                },
                ["Name"] = new Hashtable
                {
                    ["Name"] = "ProductName",
                },
                ["Price"] = new Hashtable
                {
                    ["Name"] = "Price",
                    ["Namespace"] = "http://example.com/pricing",
                    ["Prefix"] = "price",
                },
                ["Items"] = new Hashtable
                {
                    ["Name"] = "Item",
                    ["Wrapped"] = true,
                },
            },
        };
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToXml_HonorsXmlMetadata_ForRootAttributeNamespaceAndWrappedArray()
    {
        var product = new ProductWithXmlMetadata
        {
            Id = 22,
            Name = "Sample Product 22",
            Price = 30.99m,
            Items = ["Item1", "Item2", "Item3"],
        };

        // Root is passed as "Response" by default in KestrunResponse.WriteXmlResponse.
        // XmlHelper should prefer the model ClassName when XmlMetadata is present.
        var elem = XmlHelper.ToXml("Response", product);

        Assert.Equal("Product", elem.Name.LocalName);
        Assert.Equal("22", elem.Attribute("id")?.Value);
        Assert.Equal("Sample Product 22", elem.Element("ProductName")?.Value);

        // Namespace + prefix should be applied to <price:Price xmlns:price="...">...
        var priceNs = XNamespace.Get("http://example.com/pricing");
        var priceElem = elem.Element(priceNs + "Price");
        Assert.NotNull(priceElem);

        var xml = elem.ToString(SaveOptions.DisableFormatting);
        Assert.Contains("price:Price", xml);
        Assert.Contains("xmlns:price=\"http://example.com/pricing\"", xml);

        // Wrapped array should appear as <Item><Item>..</Item></Item>
        var wrapper = elem.Element("Item");
        Assert.NotNull(wrapper);
        Assert.Equal(new[] { "Item1", "Item2", "Item3" }, [.. wrapper!.Elements("Item").Select(e => e.Value)]);
    }


    [Fact]
    [Trait("Category", "Utilities")]
    public void NullElement_WithXsiNil_ReturnsNullValue()
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var xml = new XElement("Value", new XAttribute(xsi + "nil", "true"));
        var ht = XmlHelper.ToHashtable(xml);
        Assert.True(ht.ContainsKey("Value"));
        Assert.Null(ht["Value"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void SimpleValueElement_ReturnsScalar()
    {
        var xml = new XElement("Age", "42");
        var ht = XmlHelper.ToHashtable(xml);
        Assert.Equal("42", ht["Age"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ElementWithWhitespaceOnlyValue_HasNoKey()
    {
        var xml = new XElement("Empty", "   ");
        var ht = XmlHelper.ToHashtable(xml);
        Assert.False(ht.ContainsKey("Empty"));
        Assert.Empty(ht); // no attributes, no value
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ElementWithAttributeAndValue_HasAttributeAndValue()
    {
        var xml = new XElement("Node", new XAttribute("attr", "x"), "text");
        var ht = XmlHelper.ToHashtable(xml);
        Assert.Equal("x", ht["@attr"]);
        Assert.Equal("text", ht["Node"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ElementWithAttributesAndChildren_BuildsNestedHashtable()
    {
        var xml = new XElement("Person",
            new XAttribute("id", "123"),
            new XElement("Name", "Bob"),
            new XElement("Age", "42"));

        var ht = XmlHelper.ToHashtable(xml);

        Assert.Equal("123", ht["@id"]);
        var person = Assert.IsType<Hashtable>(ht["Person"]);
        Assert.Equal("Bob", person["Name"]);
        Assert.Equal("42", person["Age"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RepeatedChildElements_BecomesList()
    {
        var xml = new XElement("Items",
            new XElement("Item", "A"),
            new XElement("Item", "B"));

        var ht = XmlHelper.ToHashtable(xml);
        var items = Assert.IsType<Hashtable>(ht["Items"]);
        Assert.True(items["Item"] is List<object?>);
        var list = Assert.IsType<List<object?>>(items["Item"]);
        Assert.Equal(new[] { "A", "B" }, list);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RepeatedChildElements_WithMiddleNil_IncludesNullInList()
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var xml = new XElement("Items",
            new XElement("Item", "A"),
            new XElement("Item", new XAttribute(xsi + "nil", "true")),
            new XElement("Item", "C"));

        var ht = XmlHelper.ToHashtable(xml);
        var items = Assert.IsType<Hashtable>(ht["Items"]);
        var list = Assert.IsType<List<object?>>(items["Item"]);
        Assert.Equal(3, list.Count);
        Assert.Equal("A", list[0]);
        Assert.Null(list[1]);
        Assert.Equal("C", list[2]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void NestedStructure_ParsedRecursively()
    {
        var xml = new XElement("Outer",
            new XElement("Inner",
                new XElement("Value", "1")));

        var ht = XmlHelper.ToHashtable(xml);
        var outer = Assert.IsType<Hashtable>(ht["Outer"]);
        var inner = Assert.IsType<Hashtable>(outer["Inner"]);
        Assert.Equal("1", inner["Value"]);
    }
}
