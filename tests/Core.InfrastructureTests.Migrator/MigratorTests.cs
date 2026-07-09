using Core.InfrastructureTests.Migrator.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Core.InfrastructureTests.Migrator;

[Collection("Migrator")]
public class MigratorTests : IAsyncLifetime
{
    private readonly MigratorTestFixture _fixture;
    private readonly string _scriptPath;

    public MigratorTests(MigratorTestFixture fixture)
    {
        _fixture = fixture;
        _scriptPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_scriptPath);
    }

    public async Task InitializeAsync()
    {
        await DropMigrationHistoryTableAsync();
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_scriptPath))
            Directory.Delete(_scriptPath, recursive: true);

        await DropMigrationHistoryTableAsync();
    }

    [Fact]
    public async Task ExecutePendingMigrationsAsync_ShouldRunScriptsAndRecordThem()
    {
        // Arrange
        WriteSqlFile("001_script.sql",
            @"CREATE TABLE IF NOT EXISTS ""TestTable"" (""Id"" SERIAL PRIMARY KEY, ""Name"" TEXT);");

        var migrator = _fixture.CreateMigrator(_scriptPath);

        // Act
        await migrator.ExecutePendingMigrationsAsync();

        // Assert
        var executed = await GetExecutedScriptNames();
        executed.Should().ContainSingle()
            .Which.Should().Be("001_script.sql");

        (await TableExists("TestTable")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecutePendingMigrationsAsync_ShouldSkipAlreadyExecutedScripts()
    {
        // Arrange
        WriteSqlFile("001_first.sql",
            @"CREATE TABLE IF NOT EXISTS ""SkipTest"" (""Id"" SERIAL PRIMARY KEY);");

        var migrator = _fixture.CreateMigrator(_scriptPath);
        await migrator.ExecutePendingMigrationsAsync();

        WriteSqlFile("002_second.sql",
            @"ALTER TABLE ""SkipTest"" ADD COLUMN ""Name"" TEXT;");

        // Act
        await migrator.ExecutePendingMigrationsAsync();

        // Assert
        var executed = await GetExecutedScriptNames();
        executed.Should().BeEquivalentTo("001_first.sql", "002_second.sql");
    }

    [Fact]
    public async Task ExecutePendingMigrationsAsync_ShouldRollbackOnInvalidSql()
    {
        // Arrange
        WriteSqlFile("001_invalid.sql", "INVALID;");

        var migrator = _fixture.CreateMigrator(_scriptPath);

        // Act
        var act = () => migrator.ExecutePendingMigrationsAsync();

        // Assert
        await act.Should().ThrowAsync<PostgresException>();

        var executed = await GetExecutedScriptNames();
        executed.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutePendingMigrationsAsync_ShouldThrowForMissingDirectory()
    {
        // Arrange
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var migrator = _fixture.CreateMigrator(missingPath);

        // Act
        var act = () => migrator.ExecutePendingMigrationsAsync();

        // Assert
        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task ExecutePendingMigrationsAsync_ShouldExecuteScriptsInAlphabeticalOrder()
    {
        // Arrange
        WriteSqlFile("003_third.sql",
            @"INSERT INTO ""OrderTest"" (""Name"") VALUES ('third');");
        WriteSqlFile("001_first.sql",
            @"CREATE TABLE ""OrderTest"" (""Id"" SERIAL PRIMARY KEY, ""Name"" TEXT);");
        WriteSqlFile("002_second.sql",
            @"INSERT INTO ""OrderTest"" (""Name"") VALUES ('second');");

        var migrator = _fixture.CreateMigrator(_scriptPath);

        // Act
        await migrator.ExecutePendingMigrationsAsync();

        // Assert — if order was wrong, inserts would fail because table wouldnt exist yet
        var names = await QueryColumn(
            @"SELECT ""Name"" FROM ""OrderTest"" ORDER BY ""Id""");
        names.Should().ContainInOrder("second", "third");
    }

    private void WriteSqlFile(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_scriptPath, fileName), content);
    }

    private async Task<HashSet<string>> GetExecutedScriptNames()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT ""ScriptName"" FROM public.""MigrationHistory""", conn);

        var result = new HashSet<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));

        return result;
    }

    private async Task<bool> TableExists(string tableName)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = @name)", conn);
        cmd.Parameters.AddWithValue("@name", tableName);

        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<List<string>> QueryColumn(string sql)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var results = new List<string>();
        while (await reader.ReadAsync())
            results.Add(reader.GetFieldValue<string>(0));

        return results;
    }

    private async Task DropMigrationHistoryTableAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"DROP TABLE IF EXISTS public.""MigrationHistory""", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
