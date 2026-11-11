import { Page, Locator } from '@playwright/test';

export class ApprovalsPage {
  readonly page: Page;
  readonly approvalsTab: Locator;
  readonly invoicesTab: Locator;
  readonly decisionErrorAlert: Locator;

  constructor(page: Page) {
    this.page = page;
    this.approvalsTab = page.getByRole('tab', { name: 'Approvals' });
    this.invoicesTab = page.getByRole('tab', { name: 'Invoices' });
    this.decisionErrorAlert = page.getByRole('alert').filter({ hasText: /Manager cannot approve|must be a Director|exceeding/i });
  }

  async openApprovals() {
    await this.approvalsTab.click();
  }

  async openInvoices() {
    await this.invoicesTab.click();
  }

  async approveInvoice(invoiceId: string) {
    const approveButton = this.page.getByRole('button', { name: `Approve invoice ${invoiceId}` });
    await approveButton.click();
  }

  getDecisionError(): Locator {
    return this.page.locator('#decision-error');
  }
}
