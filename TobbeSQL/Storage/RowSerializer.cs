using System.Text;

namespace TobbeSQL.Storage;

/// <summary>
/// Converts rows (arrays of column values) to and from byte arrays.
///
/// Binary format:
///   [2-byte total row length (UInt16)] [column1 bytes] [column2 bytes] ...
///
/// Integer columns: written as 4 bytes using BitConverter.GetBytes((int)value)
/// Varchar columns: [2-byte string length (UInt16)] [UTF-8 encoded bytes]
///
/// The schema is needed for both serialization and deserialization so we know
/// the type of each column and can read the right number of bytes.
/// </summary>
public class RowSerializer
{
    /// <summary>
    /// Serializes the given values into a byte array according to the schema.
    ///
    /// Steps:
    /// 1. Write a 2-byte placeholder for the total row length at the start.
    /// 2. For each column in the schema (in order), write the value:
    ///    - Integer: use BitConverter.GetBytes on the (int) value (4 bytes)
    ///    - Varchar: get the UTF-8 bytes of the (string) value, write a 2-byte
    ///      length prefix (the byte count), then the UTF-8 bytes
    /// 3. Go back and fill in the total row length at the start.
    ///
    /// </summary>
    public byte[] Serialize(Schema schema, object[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(new byte[2]);
        var lenght = 0;
        for (var i = 0; i < schema.Columns.Count; i++)
        {
            var column = schema.Columns[i];
            var value = values[i];
            switch (column.Type)
            {
                case ColumnType.Integer:
                    writer.Write(BitConverter.GetBytes((int)value));
                    lenght += 4;
                    break;
                case ColumnType.Varchar:
                    var valueBytes = Encoding.UTF8.GetBytes((string)value);
                    writer.Write(BitConverter.GetBytes((short)valueBytes.Length));
                    writer.Write(valueBytes);
                    lenght += 2 + valueBytes.Length;
                    break;
                default:
                    throw new Exception($"Serializer do not support type: {column.Type}");
            }
        }

        writer.Seek(0, SeekOrigin.Begin);
        writer.Write((short)lenght);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Deserializes a byte array back into an object[] of column values.
    ///
    /// Steps:
    /// 1. Read the 2-byte total row length (you can use it for validation if you want).
    /// 2. For each column in the schema (in order), read the value:
    ///    - Integer: read 4 bytes, convert with BitConverter.ToInt32
    ///    - Varchar: read 2-byte length prefix, then read that many bytes,
    ///      convert to string with Encoding.UTF8.GetString
    /// 3. Return the values as an object[].
    ///
    /// </summary>
    public object[] Deserialize(Schema schema, byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var totalLenght = reader.ReadInt16();
        var values = new List<object>();
        foreach (var column in schema.Columns)
        {
            switch (column.Type)
            {
                case ColumnType.Integer:
                    values.Add(reader.ReadInt32());
                    break;
                case ColumnType.Varchar:
                    var valueLenght = reader.ReadInt16();
                    values.Add(Encoding.UTF8.GetString(reader.ReadBytes(valueLenght)));
                    break;
                default:
                    throw new Exception($"Serializer do not support type: {column.Type}");
            }
        }

        return [.. values];
    }
}
