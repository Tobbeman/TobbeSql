using System.Diagnostics;
using TobbeSQL.Execution;
using TobbeSQL.Parser;
using TobbeSQL.Storage;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TobbeSQL <sql-command>");
    Console.Error.WriteLine("       TobbeSQL .tables");
    return 1;
}

var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "data");
var catalog = new Catalog(dbPath);
var input = args[0];
int exitCode;
var watch = Stopwatch.StartNew();
if (input == ".tables")
{
    foreach (var name in catalog.TableNames)
    {
        Console.WriteLine(name);
    }
    return 0;
}

try
{
    var tokens = new Tokenizer().Tokenize(input);
    var statement = new SqlParser().Parse(tokens);
    var executor = new QueryExecutor(catalog);
    var result = executor.Execute(statement);

    if (result.Message is not null)
    {
        Console.WriteLine(result.Message);
    }
    else if (result.Columns.Count > 0)
    {
        Console.WriteLine(string.Join("\t", result.Columns));
        foreach (var row in result.Rows)
        {
            Console.WriteLine(string.Join("\t", row));
        }
    }
    else if (result.AffectedRows > 0)
    {
        Console.WriteLine($"{result.AffectedRows} row(s) affected.");
    }

    exitCode = 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    exitCode = 1;
}

Console.WriteLine($"Elapsed: {watch.Elapsed}");
return exitCode;
