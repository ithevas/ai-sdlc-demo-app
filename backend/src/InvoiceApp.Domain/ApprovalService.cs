namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// Implements role-based approval limits and multi-stage approval for high-value invoices.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, string approver, string role)
    {
        if (string.IsNullOrWhiteSpace(approver))
        {
            throw new ArgumentException("Approver username is required.", nameof(approver));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Approver role is required.", nameof(role));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        bool isManager = string.Equals(role, Roles.Manager, StringComparison.OrdinalIgnoreCase);
        bool isDirector = string.Equals(role, Roles.Director, StringComparison.OrdinalIgnoreCase);

        if (!isManager && !isDirector)
        {
            throw new InvalidOperationException($"Role '{role}' is not authorized to approve invoices.");
        }

        // Rule 1: A Manager may approve invoices up to 50,000.
        // Rule 2: A Director may approve invoices up to 500,000.
        if (invoice.Amount <= ApprovalLimits.ManagerMaxAmount)
        {
            MarkApproved(invoice, approver);
            _repository.Update(invoice);
            return invoice;
        }

        if (invoice.Amount <= ApprovalLimits.DirectorMaxAmount)
        {
            if (isManager)
            {
                throw new InvalidOperationException(
                    $"Manager cannot approve invoice {invoice.Id} exceeding {ApprovalLimits.ManagerMaxAmount:C0}. Requires a Director.");
            }

            MarkApproved(invoice, approver);
            _repository.Update(invoice);
            return invoice;
        }

        // Rule 3: Anything above 500,000 needs two distinct approvers, and the second approver must be a director.
        // Stage 1: First approval
        if (string.IsNullOrEmpty(invoice.FirstApprover))
        {
            invoice.FirstApprover = approver;
            invoice.FirstApprovedAt = DateTime.UtcNow;
            _repository.Update(invoice);
            return invoice;
        }

        // Stage 2: Second approval
        // Rule 4: The same user may never count as both approvers on one invoice.
        if (string.Equals(invoice.FirstApprover, approver, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The same user ('{approver}') cannot provide both approvals for invoice {invoice.Id}.");
        }

        // Second approver must be a director.
        if (!isDirector)
        {
            throw new InvalidOperationException(
                $"The second approver for invoice {invoice.Id} exceeding {ApprovalLimits.DirectorMaxAmount:C0} must be a Director.");
        }

        MarkApproved(invoice, approver);
        _repository.Update(invoice);
        return invoice;
    }

    /// <summary>
    /// Backward-compatible overload defaulting to Director role.
    /// </summary>
    public Invoice Approve(string invoiceId, string approver) =>
        Approve(invoiceId, approver, Roles.Director);

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private static void MarkApproved(Invoice invoice, string approver)
    {
        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
