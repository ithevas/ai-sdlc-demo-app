using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status, decimal amount = 100m) =>
        new() { Id = id, CustomerName = "Test School", Amount = amount, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Theory]
    [InlineData(100.0)]
    [InlineData(50000.0)]
    public void Approve_ManagerCanApproveInvoicesUpTo50000(double amount)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, (decimal)amount);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "alice.manager", Roles.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("alice.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Theory]
    [InlineData(50000.01)]
    [InlineData(100000.0)]
    public void Approve_ManagerCannotApproveInvoicesOver50000_AndInvoiceStateIsUnchanged(double amount)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, (decimal)amount);
        var service = ServiceFor(invoice);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "alice.manager", Roles.Manager));

        Assert.Contains("Manager cannot approve", ex.Message);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
    }

    [Theory]
    [InlineData(50000.0)]
    [InlineData(50000.01)]
    [InlineData(500000.0)]
    public void Approve_DirectorCanApproveInvoicesUpTo500000(double amount)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, (decimal)amount);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "bob.director", Roles.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("bob.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_InvoicesOver500000_FirstApprovalSetsFirstApproverAndRemainsPending()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 750000m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "alice.manager", Roles.Manager);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal("alice.manager", result.FirstApprover);
        Assert.NotNull(result.FirstApprovedAt);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
    }

    [Fact]
    public void Approve_InvoicesOver500000_SecondApprovalByDirectorMarksApproved()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 750000m);
        var service = ServiceFor(invoice);

        // First approver
        service.Approve("INV-1", "alice.manager", Roles.Manager);

        // Distinct second approver who is Director
        var result = service.Approve("INV-1", "bob.director", Roles.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("alice.manager", result.FirstApprover);
        Assert.Equal("bob.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_InvoicesOver500000_SameUserCannotBeBothApprovers_AndStateIsUnchanged()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 750000m);
        var service = ServiceFor(invoice);

        // First approval by Director
        service.Approve("INV-1", "bob.director", Roles.Director);

        // Same user attempts second approval
        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "bob.director", Roles.Director));

        Assert.Contains("cannot provide both approvals", ex.Message);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal("bob.director", invoice.FirstApprover);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
    }

    [Fact]
    public void Approve_InvoicesOver500000_SecondApproverMustBeDirector_ManagerIsRejected()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 750000m);
        var service = ServiceFor(invoice);

        // First approver
        service.Approve("INV-1", "alice.manager", Roles.Manager);

        // Second approver is distinct, but only a Manager
        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "charlie.manager", Roles.Manager));

        Assert.Contains("must be a Director", ex.Message);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal("alice.manager", invoice.FirstApprover);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
    }

    [Fact]
    public void Approve_UnauthorizedRole_ThrowsInvalidOperationException()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 100m);
        var service = ServiceFor(invoice);

        Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "eve.guest", "Guest"));
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "bob.director", Roles.Director));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() =>
            service.Approve("INV-404", "bob.director", Roles.Director));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() =>
            service.Reject("INV-1", "demo.approver", "  "));
    }
}
