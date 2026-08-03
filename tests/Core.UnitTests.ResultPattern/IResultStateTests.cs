using Core.ResultPattern;
using FluentAssertions;
using Xunit;

namespace Core.UnitTests.ResultPattern;

public class IResultStateTests
{
    [Fact]
    public void Result_Success_ShouldExposeSuccessState()
    {
        IResultState state = Result.Success;

        state.IsSuccess.Should().BeTrue();
        state.Code.Should().Be(ResultCode.Ok);
        state.Error.Should().BeNull();
    }

    [Fact]
    public void Result_NotFound_ShouldExposeFailureState()
    {
        IResultState state = Result.NotFound("missing");

        state.IsSuccess.Should().BeFalse();
        state.Code.Should().Be(ResultCode.NotFound);
        state.Error.Should().Be("missing");
    }

    [Fact]
    public void Result_InternalError_ShouldExposeFailureState()
    {
        IResultState state = Result.InternalError();

        state.IsSuccess.Should().BeFalse();
        state.Code.Should().Be(ResultCode.InternalError);
        state.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public void GenericResult_Success_ShouldExposeSuccessState()
    {
        IResultState state = Result<int>.Success(5);

        state.IsSuccess.Should().BeTrue();
        state.Code.Should().Be(ResultCode.Ok);
        state.Error.Should().BeNull();
    }

    [Fact]
    public void GenericResult_Conflict_ShouldExposeFailureState()
    {
        IResultState state = Result<int>.Conflict("duplicate");

        state.IsSuccess.Should().BeFalse();
        state.Code.Should().Be(ResultCode.Conflict);
        state.Error.Should().Be("duplicate");
    }
}