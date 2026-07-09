using Npgsql;

namespace Core.Migrator;

public class Migrator(string connectionString, string scriptPath)
{
    public async Task ExecutePendingMigrationsAsync()
    {
        if (!Directory.Exists(scriptPath))
            throw new DirectoryNotFoundException($"Migration script directory not found: {scriptPath}");

        await EnsureMigrationTableAsync();

        var executedScripts = await GetExecutedScriptNamesAsync();
        var sqlFiles = Directory.GetFiles(scriptPath, "*.sql")
            .OrderBy(f => f)
            .ToList();

        await using var sqlConnection = new NpgsqlConnection(connectionString);
        await sqlConnection.OpenAsync();

        foreach (var file in sqlFiles)
        {
            var fileName = Path.GetFileName(file);
            if (executedScripts.Contains(fileName)) continue;

            Console.WriteLine($"Running {fileName}...");

            var script = await File.ReadAllTextAsync(file);

            await using var transaction = await sqlConnection.BeginTransactionAsync();

            try
            {
                using (var sqlCommand = new NpgsqlCommand(script, sqlConnection, transaction))
                {
                    await sqlCommand.ExecuteNonQueryAsync();
                }

                using (var command = new NpgsqlCommand(
                           @"INSERT INTO public.""MigrationHistory"" (""ScriptName"") VALUES (@name)",
                           sqlConnection, transaction))
                {
                    command.Parameters.AddWithValue("@name", fileName);
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                Console.WriteLine($"{fileName} executed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error executing {fileName}: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine("All pending migrations processed.");
    }

    private async Task EnsureMigrationTableAsync()
    {
        await using var sqlConnection = new NpgsqlConnection(connectionString);
        await sqlConnection.OpenAsync();

        var cmdText = @"
            CREATE TABLE IF NOT EXISTS public.""MigrationHistory"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""ScriptName"" VARCHAR(255) NOT NULL UNIQUE,
                ""ExecutedOn"" TIMESTAMP NOT NULL DEFAULT NOW()
            );
        ";
        await using var cmd = new NpgsqlCommand(cmdText, sqlConnection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<string>> GetExecutedScriptNamesAsync()
    {
        var result = new HashSet<string>();
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"SELECT ""ScriptName"" FROM public.""MigrationHistory""", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }
}