# Project Conventions for AI Agents

Invoice approval demo app: ASP.NET Core 8 Web API with a static HTML/JS web UI and in-memory persistence.

---

## 1. Core Domain Rules: Role-Based Approval Limits

All approval business logic belongs in `backend/src/InvoiceApp.Domain/ApprovalService.cs`. Controllers must only delegate to this service.

### Approval Thresholds
| Invoice Amount | Allowed Approver(s) | Notes |
| :--- | :--- | :--- |
| **$\le$ $50,000** | **Manager** or **Director** | Single approval moves invoice to `Approved`. |
| **$\le$ $500,000** | **Director** | Single approval moves invoice to `Approved`. Manager approval must be rejected. |
| **$>$ $500,000** | **Two Distinct Approvers** | Requires 2 separate steps. Second approver MUST be a `Director`. |

### Critical Invariants & Negative Rules
1. **Distinct Approver Invariant**: For invoices exceeding $500,000, **the same user may never count as both approvers on one invoice**. If the second approval attempt is from the first approver, reject immediately with a clear reason.
2. **Second Approver Role Invariant**: The second approver for an invoice exceeding $500,000 **must have the Director role**.
3. **State Immutability on Rejection**: A rejected approval attempt (over-limit, unauthorized role, duplicate approver) MUST return a clear error reason, and **NOTHING about the invoice state changes** (no status changes, no partial approval writes, no timestamps corrupted).

---

## 2. Architecture & Layering

- `backend/src/InvoiceApp.Domain`: Pure business rules. Zero dependencies on ASP.NET Core, HTTP, or databases.
  - Entities: `Invoice.cs`
  - Lifecycle & Policy: `ApprovalService.cs`
  - Repositories: `IInvoiceRepository.cs`
- `backend/src/InvoiceApp.Api`: HTTP boundary, controllers, in-memory repository implementation, and static frontend (`wwwroot/`).
  - Controllers inherit `ApiControllerBase` and wrap calls with `Handle(...)`.
  - Maps exceptions: `KeyNotFoundException` -> 404, `InvalidOperationException` -> 409 Conflict, `ArgumentException` -> 400 Bad Request.
  - Auth: Session token based via `SessionAuthMiddleware` and `[RequireRole(...)]`.
- `backend/tests/InvoiceApp.Tests`: Unit tests using xUnit and `FakeInvoiceRepository`.
  - Every rule, boundary value (e.g. 50,000, 50,001, 500,000, 500,001), and negative path must be covered.
- `e2e`: Playwright end-to-end tests targeting browser interactions.

---

## 3. Development Commands

- **Run the API & UI**:
  ```bash
  dotnet run --project backend/src/InvoiceApp.Api
  ```
  App opens at `http://localhost:5000`.
- **Run Unit Tests**:
  ```bash
  dotnet test backend/InvoiceApp.sln
  ```
- **Run Mutation Testing with Stryker**:
  ```bash
  dotnet tool install -g dotnet-stryker --version 4.13.0
  dotnet stryker
  ```
- **Run Playwright End-to-End Tests**:
  ```bash
  cd e2e
  npm install
  npx playwright test
  ```

---

## 4. Multi-Agent Team Structure & Skills

When collaborating as or with specialized sub-agents:
- **Architect / Spec Agent**: Formulates formal requirements via OpenSpec/Spec Kit, domain schemas, and task breakdown.
- **Backend Developer Agent**: Implements domain logic incrementally (Commit 1: single approver; Commit 2: dual approver & invariants).
- **QA & Mutation Test Agent**: Crafts unit tests targeting boundary edge cases and mutation kills; writes Playwright E2E tests.
- **Reviewer Agent**: Reviews pull request diffs against standards and security invariants.

---

## 5. Review Expectations & PR Triage Standards

All code reviews must output structured findings categorized strictly into one of three verdicts:
- `fixed`: The issue was valid and the code has been corrected.
- `dismissed`: The suggestion is a false positive or intentionally contradicts a business rule, accompanied by an explicit technical rationale.
- `deferred`: The suggestion is valuable but outside the scope of the immediate pull request, with a tracking plan.

---

## 6. Demo Accounts (In-Memory / Local Only)

| Username | Password | Role |
| :--- | :--- | :--- |
| `demo.manager` | `ChangeMe123!` | Manager |
| `demo.manager2` | `ChangeMe123!` | Manager |
| `demo.director` | `ChangeMe123!` | Director |
| `demo.director2` | `ChangeMe123!` | Director |
| `demo.admin` | `ChangeMe123!` | Admin |
| `demo.approver` | `ChangeMe123!` | Approver |

---

## 7. Strict Constraints (Never)

- Never allow the same user to approve an invoice twice.
- Never alter invoice properties when an approval attempt is rejected.
- Never put domain business rules into controllers or API middleware.
- Never commit real credentials or production connection strings.
