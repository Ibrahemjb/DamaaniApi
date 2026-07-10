using DammaniAPI.Features;
using DammaniAPI.Features.ServiceRequests;
using Xunit;

namespace DammaniAPI.Tests;

public class ChangeStatusValidatorTests
{
    private static ChangeStatus.Command Valid() => new()
    {
        RequestId = Guid.NewGuid().ToString(),
        ToStatus = ServiceRequestStatuses.Reviewing
    };

    [Fact]
    public void AcceptsValidTargetStatus()
    {
        Assert.True(new ChangeStatus.CommandValidator().Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("exploded")]
    public void RejectsClosedAndUnknownTargets(string status)
    {
        var command = Valid();
        command.ToStatus = status;

        var result = new ChangeStatus.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.ToStatus));
    }

    [Fact]
    public void RejectsOversizedNote()
    {
        var command = Valid();
        command.Note = new string('x', 501);

        var result = new ChangeStatus.CommandValidator().Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Note));
    }
}

public class CloseRequestValidatorTests
{
    [Fact]
    public void RequiresWhitelistedOutcome()
    {
        var valid = new CloseRequest.Command
        {
            RequestId = Guid.NewGuid().ToString(),
            Outcome = CloseOutcomes.Repaired
        };
        var invalid = new CloseRequest.Command
        {
            RequestId = Guid.NewGuid().ToString(),
            Outcome = "lost_in_space"
        };

        Assert.True(new CloseRequest.CommandValidator().Validate(valid).IsValid);
        Assert.Contains(new CloseRequest.CommandValidator().Validate(invalid).Errors,
            e => e.PropertyName == nameof(invalid.Outcome));
    }
}

public class AddNoteValidatorTests
{
    [Fact]
    public void RequiresNoteBetweenOneAndTwoThousandChars()
    {
        var empty = new AddNote.Command { RequestId = Guid.NewGuid().ToString(), Note = "" };
        var valid = new AddNote.Command { RequestId = Guid.NewGuid().ToString(), Note = "Checked inverter fuse." };
        var oversized = new AddNote.Command { RequestId = Guid.NewGuid().ToString(), Note = new string('n', 2001) };

        Assert.Contains(new AddNote.CommandValidator().Validate(empty).Errors, e => e.PropertyName == nameof(empty.Note));
        Assert.True(new AddNote.CommandValidator().Validate(valid).IsValid);
        Assert.Contains(new AddNote.CommandValidator().Validate(oversized).Errors, e => e.PropertyName == nameof(oversized.Note));
    }
}

public class CreateInternalValidatorTests
{
    private static CreateInternal.Command Valid() => new()
    {
        WarrantyId = Guid.NewGuid().ToString(),
        CustomerName = "Ahmad Saleh",
        CustomerPhone = "+970 59 221 4810",
        ProblemType = ProblemTypes.NotWorking,
        Description = "Customer called about inverter fault."
    };

    [Fact]
    public void AcceptsValidCommand()
    {
        Assert.True(new CreateInternal.CommandValidator().Validate(Valid()).IsValid);
    }

    [Fact]
    public void RejectsUnknownProblemType()
    {
        var command = Valid();
        command.ProblemType = "mystery";

        Assert.Contains(new CreateInternal.CommandValidator().Validate(command).Errors,
            e => e.PropertyName == nameof(command.ProblemType));
    }
}

public class ServiceRequestClosedGuardTests
{
    [Theory]
    [InlineData("closed", true)]
    [InlineData("new", false)]
    [InlineData("reviewing", false)]
    public void IsClosed_RecognizesClosedStatus(string status, bool expected)
    {
        Assert.Equal(expected, ServiceRequestAccess.IsClosed(status));
    }
}

public class ServiceRequestNumberHelperTests
{
    [Fact]
    public void FormatsGlobalMonthlySequence()
    {
        Assert.Equal("SR-2607-0001", ServiceRequestNumberHelper.FormatRequestNumber("SR-2607-", 1));
        Assert.Equal("SR-2607-0042", ServiceRequestNumberHelper.FormatRequestNumber("SR-2607-", 42));
    }
}
