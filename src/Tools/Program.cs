using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

static int PrintUsage(TextWriter writer)
{
	writer.WriteLine("Exports MySQL tables to a JSON file.");
	writer.WriteLine();
	writer.WriteLine("Usage:");
	writer.WriteLine("  dotnet run --project src/Tools -- [--kind workschedules|staff|adminaccountlogs] [--table <name>] [--out <path>] [--connection <connString>]");
	writer.WriteLine();
	writer.WriteLine("Options:");
	writer.WriteLine("  --kind         Export kind: workschedules (default), staff, adminaccountlogs");
	writer.WriteLine("  --table        Table name (default depends on --kind)");
	writer.WriteLine("  --out          Output file path (default: src/Tools/exports/<table>.json under repo root)");
	writer.WriteLine("  --connection   MySQL connection string (default: env ConnectionStrings__MySql, else localhost:3307 root/root kudzierki_main)");
	writer.WriteLine("  --help         Show help");

	return 1;
}

static string? TryGetOption(string[] args, string name)
{
	for (var i = 0; i < args.Length; i++)
	{
		if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))	continue;
		if (i + 1 >= args.Length)	return null;
		return args[i + 1];
	}

	return null;
}

static bool HasFlag(string[] args, string name) => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

static string? TryFindRepoRoot(string startDirectory)
{
	var current = new DirectoryInfo(startDirectory);
	while (current is not null)
	{
		if (File.Exists(Path.Combine(current.FullName, "kudzierki-app.sln")) || Directory.Exists(Path.Combine(current.FullName, ".git")))
		{
			return current.FullName;
		}

		current = current.Parent;
	}

	return null;
}

static string QuoteIdentifier(string identifier)
{
	if (!Regex.IsMatch(identifier, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
	{
		throw new ArgumentException($"Unsafe identifier '{identifier}'. Allowed: letters, digits, underscore.");
	}

	return $"`{identifier}`";
}

static bool ReadBool(MySqlDataReader reader, int ordinal)
{
	if (reader.IsDBNull(ordinal))	return false;
	return reader.GetFieldType(ordinal) == typeof(bool)
		? reader.GetBoolean(ordinal)
		: reader.GetInt32(ordinal) != 0;
}

static decimal ReadDecimal(MySqlDataReader reader, int ordinal)
{
	if (reader.IsDBNull(ordinal))	return 0m;
	return reader.GetFieldType(ordinal) == typeof(decimal)
		? reader.GetDecimal(ordinal)
		: Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}

static async Task WriteJsonAsync<T>(string outPath, IReadOnlyList<T> value)
{
	Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
	await using var stream = File.Create(outPath);
	await JsonSerializer.SerializeAsync(stream, value, new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	});
}

var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

if (HasFlag(cliArgs, "--help") || HasFlag(cliArgs, "-h") || HasFlag(cliArgs, "/?"))
{
	return PrintUsage(Console.Out);
}

var kind = (TryGetOption(cliArgs, "--kind") ?? "workschedules").Trim();
var defaultTable = kind.Equals("staff", StringComparison.OrdinalIgnoreCase)
	? "staff"
	: kind.Equals("adminaccountlogs", StringComparison.OrdinalIgnoreCase)
		? "adminaccountlogs"
		: "workschedules";

var table = TryGetOption(cliArgs, "--table") ?? defaultTable;
var connectionString =
	TryGetOption(cliArgs, "--connection")
	?? Environment.GetEnvironmentVariable("ConnectionStrings__MySql")
	?? "Server=localhost;Port=3307;Database=kudzierki_main;User ID=root;Password=root;";

var repoRoot = TryFindRepoRoot(Directory.GetCurrentDirectory());
var defaultOut = repoRoot is null
	? Path.Combine(Directory.GetCurrentDirectory(), "exports", $"{table}.json")
	: Path.Combine(repoRoot, "src", "Tools", "exports", $"{table}.json");

var outPath = TryGetOption(cliArgs, "--out") ?? defaultOut;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

var quotedTable = QuoteIdentifier(table);

if (kind.Equals("staff", StringComparison.OrdinalIgnoreCase))
{
	var sql = $"SELECT Id, Name, Position, IsActive FROM {quotedTable} ORDER BY Id;";
	var results = new List<StaffRow>(capacity: 256);

	await using (var command = new MySqlCommand(sql, connection))
	await using (var reader = await command.ExecuteReaderAsync())
	{
		var ordId = reader.GetOrdinal("Id");
		var ordName = reader.GetOrdinal("Name");
		var ordPosition = reader.GetOrdinal("Position");
		var ordIsActive = reader.GetOrdinal("IsActive");

		while (await reader.ReadAsync())
		{
			results.Add(new StaffRow(
				Id: reader.IsDBNull(ordId) ? 0 : reader.GetInt32(ordId),
				Name: reader.IsDBNull(ordName) ? string.Empty : reader.GetString(ordName),
				Position: reader.IsDBNull(ordPosition) ? string.Empty : reader.GetString(ordPosition),
				IsActive: ReadBool(reader, ordIsActive)));
		}
	}

	await WriteJsonAsync(outPath, results);
	Console.WriteLine($"Exported {results.Count} row(s) from '{table}' (kind: staff) to '{Path.GetFullPath(outPath)}'.");
	return 0;
}
else if (kind.Equals("adminaccountlogs", StringComparison.OrdinalIgnoreCase))
{
	var sql = $"SELECT Id, `Date`, CashBalanse, TerminalIncome, `Comment` FROM {quotedTable} ORDER BY `Date`, Id;";
	var results = new List<AdminAccountLogRow>(capacity: 1024);

	await using (var command = new MySqlCommand(sql, connection))
	await using (var reader = await command.ExecuteReaderAsync())
	{
		var ordId = reader.GetOrdinal("Id");
		var ordDate = reader.GetOrdinal("Date");
		var ordCashBalanse = reader.GetOrdinal("CashBalanse");
		var ordTerminalIncome = reader.GetOrdinal("TerminalIncome");
		var ordComment = reader.GetOrdinal("Comment");

		while (await reader.ReadAsync())
		{
			var dateValue = reader.IsDBNull(ordDate)
				? string.Empty
				: reader.GetDateTime(ordDate).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff", CultureInfo.InvariantCulture);

			results.Add(new AdminAccountLogRow(
				Id: reader.IsDBNull(ordId) ? 0 : reader.GetInt32(ordId),
				Date: dateValue,
				CashBalanse: ReadDecimal(reader, ordCashBalanse),
				TerminalIncome: ReadDecimal(reader, ordTerminalIncome),
				Comment: reader.IsDBNull(ordComment) ? string.Empty : reader.GetString(ordComment)));
		}
	}

	await WriteJsonAsync(outPath, results);
	Console.WriteLine($"Exported {results.Count} row(s) from '{table}' (kind: adminaccountlogs) to '{Path.GetFullPath(outPath)}'.");
	return 0;
}
else
{
	var sql = $"SELECT Id, `Date`, AdminId, IsWorkingDay, ShiftRatio, IsCleaningDay FROM {quotedTable} ORDER BY `Date`, Id;";
	var results = new List<WorkScheduleRow>(capacity: 1024);

	await using (var command = new MySqlCommand(sql, connection))
	await using (var reader = await command.ExecuteReaderAsync())
	{
		var ordId = reader.GetOrdinal("Id");
		var ordDate = reader.GetOrdinal("Date");
		var ordAdminId = reader.GetOrdinal("AdminId");
		var ordIsWorkingDay = reader.GetOrdinal("IsWorkingDay");
		var ordShiftRatio = reader.GetOrdinal("ShiftRatio");
		var ordIsCleaningDay = reader.GetOrdinal("IsCleaningDay");

		while (await reader.ReadAsync())
		{
			var dateValue = reader.IsDBNull(ordDate)
				? string.Empty
				: reader.GetDateTime(ordDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			results.Add(new WorkScheduleRow(
				Id: reader.IsDBNull(ordId) ? 0 : reader.GetInt32(ordId),
				Date: dateValue,
				AdminId: reader.IsDBNull(ordAdminId) ? 0 : reader.GetInt32(ordAdminId),
				IsWorkingDay: ReadBool(reader, ordIsWorkingDay),
				ShiftRatio: ReadDecimal(reader, ordShiftRatio),
				IsCleaningDay: ReadBool(reader, ordIsCleaningDay)));
		}
	}

	await WriteJsonAsync(outPath, results);
	Console.WriteLine($"Exported {results.Count} row(s) from '{table}' (kind: workschedules) to '{Path.GetFullPath(outPath)}'.");
	return 0;
}

internal sealed record WorkScheduleRow(
	int Id,
	string Date,
	int AdminId,
	bool IsWorkingDay,
	decimal ShiftRatio,
	bool IsCleaningDay);

internal sealed record StaffRow(
	int Id,
	string Name,
	string Position,
	bool IsActive);

internal sealed record AdminAccountLogRow(
	int Id,
	string Date,
	decimal CashBalanse,
	decimal TerminalIncome,
	string Comment);
