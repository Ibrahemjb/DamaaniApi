using DammaniAPI.Features.Customers;
using DammaniAPI.Features.Warranties;
using Xunit;

namespace DammaniAPI.Tests;

public class CustomerDetailTests
{
    [Fact]
    public void UpdateCustomerRejectsEmptyNameAndInvalidPhone()
    {
        var validator = new UpdateCustomer.CommandValidator();
        var result = validator.Validate(new UpdateCustomer.Command
        {
            CustomerId = "c1",
            Name = "",
            Phone = "abc"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCustomer.Command.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCustomer.Command.Phone));
    }

    [Fact]
    public void UpdateCustomerAcceptsValidPayload()
    {
        var validator = new UpdateCustomer.CommandValidator();
        var result = validator.Validate(new UpdateCustomer.Command
        {
            CustomerId = "c1",
            Name = "Ahmad Saleh",
            Phone = "0599123456",
            City = "Ramallah",
            Address = "Main St",
            Notes = "VIP"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NormalizePhoneStripsSeparators()
    {
        Assert.Equal("0599123456", CreateWarranty.CommandHandler.NormalizePhone("0599 123 456"));
        Assert.Equal("0599123456", CreateWarranty.CommandHandler.NormalizePhone("0599-123-456"));
    }
}
