using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Storage;

public class RowSerializerTests
{
    private readonly RowSerializer _serializer = new();

    private Schema MakeSchema(params (string name, ColumnType type)[] columns)
    {
        return new Schema(
            "test",
            columns.Select(c => new ColumnDefinition(c.name, c.type)).ToList()
        );
    }

    [Fact]
    public void RoundTrip_IntAndVarchar()
    {
        var schema = MakeSchema(("Id", ColumnType.Integer), ("Name", ColumnType.Varchar));
        var values = new object[] { 42, "hello" };

        var bytes = _serializer.Serialize(schema, values);
        var result = _serializer.Deserialize(schema, bytes);

        Assert.Equal(42, result[0]);
        Assert.Equal("hello", result[1]);
    }

    [Fact]
    public void RoundTrip_EmptyString()
    {
        var schema = MakeSchema(("Name", ColumnType.Varchar));
        var values = new object[] { "" };

        var bytes = _serializer.Serialize(schema, values);
        var result = _serializer.Deserialize(schema, bytes);

        Assert.Equal("", result[0]);
    }

    [Fact]
    public void RoundTrip_NegativeInteger()
    {
        var schema = MakeSchema(("Value", ColumnType.Integer));
        var values = new object[] { -1 };

        var bytes = _serializer.Serialize(schema, values);
        var result = _serializer.Deserialize(schema, bytes);

        Assert.Equal(-1, result[0]);
    }

    [Fact]
    public void RoundTrip_LongString()
    {
        var schema = MakeSchema(("Bio", ColumnType.Varchar));
        var longString = new string('x', 500);
        var values = new object[] { longString };

        var bytes = _serializer.Serialize(schema, values);
        var result = _serializer.Deserialize(schema, bytes);

        Assert.Equal(longString, result[0]);
    }

    [Fact]
    public void RoundTrip_MultipleIntColumns()
    {
        var schema = MakeSchema(
            ("A", ColumnType.Integer),
            ("B", ColumnType.Integer),
            ("C", ColumnType.Integer)
        );
        var values = new object[] { 1, 2, 3 };

        var bytes = _serializer.Serialize(schema, values);
        var result = _serializer.Deserialize(schema, bytes);

        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(3, result[2]);
    }
}
