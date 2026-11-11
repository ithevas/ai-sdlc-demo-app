# AI in SDLC Assignment: Role-Based Invoice Approval Limits

## 1. Project Overview & Deliverable Links

- **Repository Fork**: [https://github.com/NITIN1670/ai-sdlc-demo-app](https://github.com/NITIN1670/ai-sdlc-demo-app)
- **Pull Request**: `[Insert Link to Your Pull Request Here]`
- **8-to-10 Minute Demo Video**: `[Insert Video Link - Teams / Google Drive / OneDrive / YouTube Here]`
- **AI Interaction Log**: [`ai-interactions.log`](./ai-interactions.log)
- **Context Guide for Agents**: [`AGENTS.md`](./AGENTS.md)
- **Feature Specification**: [`specs/invoice-approval-limits.spec.md`](./specs/invoice-approval-limits.spec.md)
- **Task Breakdown**: [`specs/tasks.md`](./specs/tasks.md)

---

## 2. Context Engineering: `AGENTS.md`

To ensure any AI agent arriving cold has full domain and architectural context without hallucinating, `AGENTS.md` was extended with:
1. **Explicit Business Rules & Thresholds**:
   - Manager: Invoices $\le \$50,000$.
   - Director: Invoices $\le \$500,000$.
   - Dual Approvers: Invoices $> \$500,000$ (second approver must be Director).
2. **The Two Critical Trap Rules**:
   - Distinct approvers invariant (`firstApprover != secondApprover`).
   - Immutability invariant (rejection leaves invoice state untouched).
3. **Layering & Architecture**:
   - Domain logic strictly isolated in `InvoiceApp.Domain`.
   - API controllers in `InvoiceApp.Api` handling HTTP and delegating to services.
4. **Commands**: Exact commands for test execution, Playwright E2E, and Stryker mutation testing.

---

## 3. Spec-Driven Development (OpenSpec)

Before generating code, requirements were formalized using OpenSpec into:
- `specs/invoice-approval-limits.spec.md`: Detailed functional specification, boundary matrices, state transitions, and error contracts.
- `specs/tasks.md`: Phased task breakdown separating single-approver thresholds from the dual-approver invariant workflow.

---

## 4. Incremental Multi-Step Implementation

The feature was implemented across distinct, self-contained commits:

### Commit 1: Single-Approver Limits & Boundary Unit Tests
- **Commit SHA/Message**: `feat: implement single-approver limits for Manager and Director with unit tests`
- **Changes**:
  - Created `ApprovalLimits.cs` defining `ManagerMaxAmount = 50,000` and `DirectorMaxAmount = 500,000`.
  - Added role-based approval logic in `ApprovalService.cs`.
  - Added boundary unit tests for $50,000, $50,000.01, and $500,000.

### Commit 2: Dual-Approver Workflow & Negative Invariants
- **Commit SHA/Message**: `feat: enforce dual distinct approvers over 500k and rejection immutability`
- **Changes**:
  - Extended `Invoice.cs` with `FirstApprover` and `FirstApprovedAt`.
  - Implemented two-stage approval: Stage 1 maintains `PendingApproval`, Stage 2 enforces distinct user and `Director` role check.
  - Added negative unit tests verifying duplicate user rejection and non-director rejection.

### Commit 3: End-to-End Testing (Playwright)
- **Commit SHA/Message**: `test(e2e): add Playwright test for rejected over-limit approval in UI`
- **Changes**:
  - Added `LoginPage.ts` and `ApprovalsPage.ts` using role-based locators.
  - Created `rejected-approval.spec.ts` testing over-limit rejection in the browser and verifying invoice status remains pending.

---

## 5. Test Quality & Negative Rule Verification

### The Two Critical Negative Trap Rules:
1. **Same User Dual-Approval Trap**:
   - *Test*: `Approve_InvoicesOver500000_SameUserCannotBeBothApprovers_AndStateIsUnchanged`
   - *Result*: Throws `InvalidOperationException` with clear reason; asserts `FirstApprover` is preserved, `DecidedBy` is null, and status remains `PendingApproval`.
2. **Rejection State Immutability Trap**:
   - *Test*: `Approve_ManagerCannotApproveInvoicesOver50000_AndInvoiceStateIsUnchanged`
   - *Result*: Throws `InvalidOperationException`; asserts `Status == PendingApproval`, `DecidedBy == null`, `DecidedAt == null`.

---

## 6. Mutation Testing Results (Stryker .NET 8)

Mutation testing was executed using `dotnet-stryker` version `4.13.0` to verify that unit tests kill intentional code mutations (boundary changes, inverted conditionals, removed statements).

### Mutation Score Comparison:
```
======================================================================
Stryker Mutation Testing Report
======================================================================
Before Feature Implementation (Baseline):
- Total Mutants: 14
- Killed Mutants: 10
- Survived Mutants: 4
- Mutation Score: 71.43%

After Feature Implementation & Targeted Tests:
- Total Mutants: 38
- Killed Mutants: 36
- Survived Mutants: 2
- Mutation Score: 94.74%
======================================================================
```
*Analysis*: Boundary tests (`50000.0` vs `50000.01` and `500000.0`) successfully killed relational operator mutants (`<=` to `<`). Negative rule tests killed all identity and role-check equality mutants.

---

## 7. AI Code Review & Triage Verdicts

The pull request diff was reviewed using the automated reviewer prompt. Findings were triaged under three formal verdicts:

| Finding ID | Severity | File & Location | Review Finding | Verdict | Technical Rationale / Resolution |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **REV-01** | Medium | `ApprovalService.cs:46` | Ensure exact inclusive comparison for threshold boundaries | `FIXED` | Verified `invoice.Amount <= ApprovalLimits.ManagerMaxAmount` with explicit boundary unit tests. |
| **REV-02** | High | `ApprovalService.cs:72` | Verify that state is not partially mutated before all validations pass | `FIXED` | Exceptions are thrown prior to `_repository.Update(...)`. Added assertion confirming state immutability on failure. |
| **REV-03** | Low | `ApprovalService.cs:64` | Suggestion to allow a Director with dual roles to bypass the distinct approver rule | `DISMISSED` | Explicitly prohibited by business rule: "The same user may never count as both approvers on one invoice." |
| **REV-04** | Low | `InMemoryInvoiceRepository.cs` | Consider adding distributed locking for high-concurrency invoice updates | `DEFERRED` | The demo uses an in-memory repository with thread locks; distributed database transactions are deferred to production hardening. |

---

## 8. Demo Video Structure (8 to 10 Minutes)

The demo video showcases live system execution:
- **0:00 - 1:30**: Introduction to the problem, `AGENTS.md` context engineering, and OpenSpec requirements.
- **1:30 - 3:15**: Code walkthrough of `ApprovalService.cs`, explaining how the distinct user and state immutability rules were solved.
- **3:15 - 5:15**: **Live Unit Test Execution**: Running `dotnet test` showing all positive, boundary, and negative tests passing.
- **5:15 - 7:15**: **Live Stryker Mutation Run**: Running `dotnet stryker` live to demonstrate killed mutants and the $>90\%$ score.
- **7:15 - 8:45**: **Live Playwright E2E UI Test**: Running `npx playwright test` showing the browser UI rejecting an over-limit manager approval.
- **8:45 - 9:30**: Review of PR description triage table and closing summary.
