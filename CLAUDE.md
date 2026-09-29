# Core

Reusable .NET 10 libraries (`src/Core.*`) with tests in `tests/`. Rules and patterns below; no code tour.

Older code predates some rules (explicit constructors, public implementations, tests without AAA comments or `_Should` naming, jobs without the chunked pattern). Follow the rules in new and touched code; don't rewrite old code just to conform.

## Workflow

- Discuss changes and get approval in chat before editing files; propose names for new projects/types/members before creating them
- On a design change, update the Linear ticket description before touching code
- Work on the current branch
- Never commit; the user commits
- Check the existing library/package first; no new packages for marginal gain

## C# style

- Primary constructors
- Collection expressions (`[]`, `[.. x]`) for initializers and conversions; `new List<T>()` is fine for an empty `var` local
- UTF-8 literals (`"text"u8`) for byte content
- `sealed` entities with `{ get; set; }` for lib-owned tables; options with `{ get; init; }` (`{ get; set; }` for collections defaulted in `PostConfigure`); records for small value/result types
- Comments: one short line, only for the non-obvious "why"
- Simplest construct that works; no abstraction with a single consumer
- Ambient context (correlation/trace) over parameters threaded through every call

## Library structure

- One project per concern (`Core.Caching`, `Core.Storage`, …)
- New libs keep implementations and provider seams `internal`, with `InternalsVisibleTo` for the test project (as `Core.Storage` does)
- Third-party types (AWS, Redis, Kafka) never leak into public APIs; use own translation types
- One `XxxDependencyInstaller.AddXxx(...)` extension per project; generic `AddXxx<TContext>` when the lib owns tables
- Lib-owned tables: an interface with `DbSet`s implemented by the consumer's DbContext (`IOutbox`, `IFileStore`) + `IEntityTypeConfiguration`
- Services don't call `SaveChanges`; the command's unit of work / transaction decorator commits. Jobs have no unit of work and save per chunk
- Errors: `Result` / `Result<T>` with `ResultCode`; messages as constants in `Errors/ErrorMessages.cs`

## Options

- `const string SectionName`; bind + `.Validate(...)` per rule + `.ValidateOnStart()`
- To compare with another section's value, use `.Validate<IOptions<TOther>>(...)`
- Collections: the configuration binder **appends** configured items to a default array/list, so keep the property empty and apply defaults in `PostConfigure` only when nothing is configured
- Human-friendly units in config (`MaxFileSizeInMb`), converted internally

## Background jobs

Reference: `MarkUploadedFilesJob`, `CleanupStoredFilesJob`.

- `IBackgroundJob` with `[DisallowConcurrentExecution]` and `[Retry(0)]` (the next scheduled run retries); registered with `AddRecurringJob` and a cron from options validated by `CronValidator`
- Load candidates once, process in chunks of `BatchSize`, persist per chunk
- On failure: log what was processed plus the exception, then rethrow; summary info log in `finally`
- Do the external side effect before the DB write when a leftover row is retryable but an orphan would not be (e.g. delete from storage, then the row)

## Tests

- `// Arrange` / `// Act` / `// Assert` comments; no helper methods: query inline, assign to a local, then assert
- Test our own decisions at their boundary, never framework/library behaviour (EF, Hangfire, S3)
- Integration tests use Testcontainers: each collection gets its own containers via `ICollectionFixture<XxxFixture>`; fixtures live in `Core.IntegrationTests.Shared` (except `KafkaFixture`)
- `TestLogger<T>` instead of NSubstitute for loggers of internal types (NSubstitute cannot proxy them)
- Naming: `Method_WithCondition_ShouldOutcome`
