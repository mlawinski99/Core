using Core.CQRS;
using Core.IntegrationTests.Shared;
using Core.IntegrationTests.Shared.Fixtures;
using Core.IntegrationTests.Shared.Infrastructure;
using Core.IntegrationTests.Shared.Infrastructure.TestEntities;
using Core.ResultPattern;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.InfrastructureTests.DataAccessTypes;

[Collection("DataAccessTypesTest")]
public class UnitOfWorkTransactionTests(IntegrationTestFixture fixture)
    : IntegrationTestBase<IntegrationTestFixture>(fixture)
{
    [Fact]
    public async Task Dispatch_WhenOuterCommandSucceeds_ShouldPersistBoth()
    {
        // Arrange
        var commandB = new TestCommandB(new TestEntity { Name = "B" });
        var commandA = new TestCommandA(new TestEntity { Name = "A" }, Nested: commandB);

        // Act
        var result = await Dispatcher.Dispatch(commandA);

        // Assert
        var entityA = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandA.Entity.Id);
        var entityB = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandB.Entity.Id);

        result.IsSuccess.Should().BeTrue();
        entityA.Should().NotBeNull();
        entityB.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispatch_WhenDeeplyNestedCommandFails_ShouldRollBackWholeChain()
    {
        var commandC = new TestCommandB(new TestEntity { Name = "C" }, Succeed: false);
        var commandB = new TestCommandB(new TestEntity { Name = "B" }, Nested: commandC);
        var commandA = new TestCommandA(new TestEntity { Name = "A" }, Nested: commandB);

        // Act
        var result = await Dispatcher.Dispatch(commandA);

        // Assert
        var entityA = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandA.Entity.Id);
        var entityB = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandB.Entity.Id);
        var entityC = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandC.Entity.Id);

        result.IsSuccess.Should().BeFalse();
        entityA.Should().BeNull();
        entityB.Should().BeNull();
        entityC.Should().BeNull();
    }

    [Fact]
    public async Task Dispatch_WhenOuterCommandFails_ShouldRollBackNestedCommand()
    {
        // Arrange
        var commandB = new TestCommandB(new TestEntity { Name = "B" });
        var commandA = new TestCommandA(new TestEntity { Name = "A" }, Succeed: false, Nested: commandB);

        // Act
        await Dispatcher.Dispatch(commandA);

        // Assert - B opened no transaction of its own, so A's rollback discards B's write too
        var entityA = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandA.Entity.Id);
        var entityB = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandB.Entity.Id);

        entityA.Should().BeNull();
        entityB.Should().BeNull();
    }

    [Fact]
    public async Task Dispatch_WhenNonTransactionalCommandRunsUnderTransaction_ShouldRollBackWholeChain()
    {
        // Arrange
        var nonTransactional = new NonTransactionalTestCommand(new TestEntity { Name = "NonTransactional" });
        var commandA = new TestCommandA(new TestEntity { Name = "A" }, NestedNonTransactional: nonTransactional);

        // Act
        var result = await Dispatcher.Dispatch(commandA);

        // Assert
        var entityA = await Db.TestEntities.SingleOrDefaultAsync(x => x.Id == commandA.Entity.Id);
        var entityNonTransactional = await Db.TestEntities
            .SingleOrDefaultAsync(x => x.Id == nonTransactional.Entity.Id);

        result.Code.Should().Be(ResultCode.InternalError);
        entityNonTransactional.Should().BeNull();
        entityA.Should().BeNull();
    }
}

public record TestCommandA(
    TestEntity Entity,
    bool Succeed = true,
    TestCommandB? Nested = null,
    NonTransactionalTestCommand? NestedNonTransactional = null) : ICommand<Result>;

public record NonTransactionalTestCommand(TestEntity Entity) : INonTransactionalCommand<Result>;

public class NonTransactionalTestCommandHandler(TestDbContext db)
    : ICommandHandler<NonTransactionalTestCommand, Result>
{
    public async Task<Result> Handle(NonTransactionalTestCommand command, CancellationToken cancellationToken)
    {
        db.TestEntities.Add(command.Entity);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}

public class TestCommandAHandler(TestDbContext db, IRequestDispatcher dispatcher)
    : ICommandHandler<TestCommandA, Result>
{
    public async Task<Result> Handle(TestCommandA command, CancellationToken cancellationToken)
    {
        if (command.Nested is not null)
        {
            await dispatcher.Dispatch(command.Nested, cancellationToken);
        }

        if (command.NestedNonTransactional is not null)
        {
            await dispatcher.Dispatch(command.NestedNonTransactional, cancellationToken);
        }

        db.TestEntities.Add(command.Entity);
        await db.SaveChangesAsync(cancellationToken);

        return command.Succeed ? Result.Success : Result.Conflict("error");
    }
}

public record TestCommandB(TestEntity Entity, bool Succeed = true, TestCommandB? Nested = null)
    : ICommand<Result>;

public class TestCommandBHandler(TestDbContext db, IRequestDispatcher dispatcher)
    : ICommandHandler<TestCommandB, Result>
{
    public async Task<Result> Handle(TestCommandB command, CancellationToken cancellationToken)
    {
        if (command.Nested is not null)
        {
            await dispatcher.Dispatch(command.Nested, cancellationToken);
        }

        db.TestEntities.Add(command.Entity);
        await db.SaveChangesAsync(cancellationToken);

        return command.Succeed ? Result.Success : Result.Conflict("error");
    }
}
