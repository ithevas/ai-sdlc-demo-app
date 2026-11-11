# Task Breakdown: Role-Based Approval Limits

## Phase 1: Context Engineering & Specification
- [x] Task 1.1: Extend `AGENTS.md` with explicit role rules, threshold limits, immutability constraints, and review standards.
- [x] Task 1.2: Generate OpenSpec functional specification (`specs/invoice-approval-limits.spec.md`).

## Phase 2: Single-Approver Thresholds (Step 1 Implementation)
- [ ] Task 2.1: Define domain constants for limits (`ApprovalLimits.ManagerLimit = 50,000`, `ApprovalLimits.DirectorLimit = 500,000`).
- [ ] Task 2.2: Update `ApprovalService.Approve` to accept approver username and role.
- [ ] Task 2.3: Enforce single-approver limits:
  - Manager can approve $\le \$50,000$.
  - Director can approve $\le \$500,000$.
  - Reject Manager attempts $> \$50,000$ without mutating invoice state.
- [ ] Task 2.4: Add unit tests for single-approver boundaries and unauthorized Manager approval.

## Phase 3: Dual-Approver Workflow & Invariants (Step 2 Implementation)
- [ ] Task 3.1: Extend `Invoice` model with `FirstApprover` and `FirstApprovedAt`.
- [ ] Task 3.2: Implement two-stage approval logic for amounts $> \$500,000$:
  - First approval records approver and maintains `PendingApproval` status.
  - Second approval validates distinct user (`approver != invoice.FirstApprover`).
  - Second approval validates approver has `Director` role.
  - Final approval transitions invoice to `Approved`.
- [ ] Task 3.3: Implement strict failure isolation: ensure rejected attempts return clear reason and make no state changes.
- [ ] Task 3.4: Add unit tests for dual-approver rules and both negative trap scenarios (self-approval attempt, non-director second approver).

## Phase 4: API & Authentication Integration
- [ ] Task 4.1: Update `ApprovalsController` to extract `session.Role` and pass it to `ApprovalService`.
- [ ] Task 4.2: Update `appsettings.json` with demo accounts for Manager and Director roles.
- [ ] Task 4.3: Seed demo invoices with amounts representing boundary thresholds ($45,000, $250,000, $750,000).

## Phase 5: Verification & Quality Assurance
- [ ] Task 5.1: Create Playwright end-to-end test verifying UI rejection of over-limit approval.
- [ ] Task 5.2: Execute unit tests and Stryker mutation testing before & after to prove test kill rate.
- [ ] Task 5.3: Perform AI PR review simulation and triage findings into `fixed`, `dismissed`, and `deferred`.
- [ ] Task 5.4: Generate `ASSIGNMENT.md` and complete demo video recording walkthrough.
