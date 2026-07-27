using System.Text;

namespace TobbeSQL.Storage;

/// <summary>
/// Binary row format:
///   [2B total length] [column1 bytes] [column2 bytes] ...
///   Integer: 4 bytes. Varchar: [2B string byte length] [UTF-8 bytes].
/// </summary>
public class RowSerializer
{
    public byte[] Serialize(Schema schema, object[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(new byte[2]);
        var length = 0;
        for (var i = 0; i < schema.Columns.Count; i++)
        {
            var column = schema.Columns[i];
            var value = values[i];
            switch (column.Type)
            {
                case ColumnType.Integer:
                    writer.Write(BitConverter.GetBytes((int)value));
                    length += 4;
                    break;
                case ColumnType.Varchar:
                    var valueBytes = Encoding.UTF8.GetBytes((string)value);
                    writer.Write(BitConverter.GetBytes((short)valueBytes.Length));
                    writer.Write(valueBytes);
                    length += 2 + valueBytes.Length;
                    break;
                default:
                    throw new Exception($"Serializer do not support type: {column.Type}");
            }
        }

        writer.Seek(0, SeekOrigin.Begin);
        writer.Write((short)length);
        return stream.ToArray();
    }

    public object[] Deserialize(Schema schema, byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var totalLength = reader.ReadInt16();
        var values = new List<object>();
        foreach (var column in schema.Columns)
        {
            switch (column.Type)
            {
                case ColumnType.Integer:
                    values.Add(reader.ReadInt32());
                    break;
                case ColumnType.Varchar:
                    var valueLength = reader.ReadInt16();
                    values.Add(Encoding.UTF8.GetString(reader.ReadBytes(valueLength)));
                    break;
                default:
                    throw new Exception($"Serializer do not support type: {column.Type}");
            }
        }

        return [.. values];
    }
}
