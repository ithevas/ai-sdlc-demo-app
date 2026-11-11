import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { ApprovalsPage } from '../pages/ApprovalsPage';

test.describe('Role-Based Approval Limits', () => {
  test('rejects over-limit approval attempt by Manager in the UI and leaves invoice unchanged', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const approvalsPage = new ApprovalsPage(page);

    // 1. Sign in as Manager
    await loginPage.goto();
    await loginPage.login('demo.manager', 'ChangeMe123!');

    // 2. Navigate to Pending Approvals
    await approvalsPage.openApprovals();

    // 3. Attempt to approve invoice INV-1003 ($120,000 - exceeds Manager $50,000 threshold)
    await approvalsPage.approveInvoice('INV-1003');

    // 4. Assert clear error message is displayed in role=alert / decision-error
    const errorAlert = approvalsPage.getDecisionError();
    await expect(errorAlert).toBeVisible();
    await expect(errorAlert).toContainText('Manager cannot approve');

    // 5. Switch back to Invoices list and verify INV-1003 remains in 'Pending Approval' status
    await approvalsPage.openInvoices();
    const invoiceRow = page.getByRole('row', { name: /INV-1003/i });
    await expect(invoiceRow).toBeVisible();
    await expect(invoiceRow).toContainText('Pending Approval');
    await expect(invoiceRow).not.toContainText('Approved');
  });
});
