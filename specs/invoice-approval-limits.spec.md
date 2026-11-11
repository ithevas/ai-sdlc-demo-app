# Feature Specification: Role-Based Invoice Approval Limits

## Overview
This specification defines role-based approval limits and multi-stage approval requirements for the Invoice Approval Service in `ai-sdlc-demo-app`.

## Business Rules

### 1. Single Approver Thresholds
- **Manager Approval Limit**: Invoices with amount $\le \$50,000$ can be approved by a user with role `Manager` or higher (`Director`).
- **Director Approval Limit**: Invoices with amount $\le \$500,000$ can be approved by a user with role `Director`.
- **Manager Over-limit Rejection**: If a user with role `Manager` attempts to approve an invoice with amount $> \$50,000$, the request MUST be rejected with a clear error reason. The invoice state MUST remain unchanged.

### 2. High-Value Invoices (> $500,000) Dual-Approval Rule
- Invoices with amount $> \$500,000$ require **two distinct approvers**.
- **First Approver**: Can be a `Manager` or `Director`. When the first approver approves:
  - The invoice records `FirstApprover` and `FirstApprovedAt`.
  - The invoice status MUST remain `PendingApproval` awaiting the second approval.
- **Second Approver**:
  - The second approver MUST have the role `Director`.
  - The second approver MUST NOT be the same user as the first approver (`secondApprover != firstApprover`).
  - When the valid second approval is registered, the invoice status changes to `Approved`, recording `DecidedBy = secondApprover` and `DecidedAt = DateTime.UtcNow`.

### 3. Invariants & Negative Rules
- **No Self-Dual-Approval**: The same user may NEVER count as both approvers on one invoice. Attempting to do so returns an error and does NOT alter the invoice.
- **State Immutability on Rejection**: Any rejected approval attempt (over limit, duplicate approver, unauthorized role) returns a clear descriptive reason, and NOTHING about the invoice changes (no status change, no audit corruption).
