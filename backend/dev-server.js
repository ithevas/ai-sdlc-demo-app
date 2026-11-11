const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 5000;
const WWWROOT = path.join(__dirname, 'src', 'InvoiceApp.Api', 'wwwroot');

// In-memory seed state matching appsettings and InMemoryInvoiceRepository
const users = [
  { username: 'demo.manager', password: 'ChangeMe123!', role: 'Manager' },
  { username: 'demo.manager2', password: 'ChangeMe123!', role: 'Manager' },
  { username: 'demo.director', password: 'ChangeMe123!', role: 'Director' },
  { username: 'demo.director2', password: 'ChangeMe123!', role: 'Director' },
  { username: 'demo.approver', password: 'ChangeMe123!', role: 'Approver' },
  { username: 'demo.admin', password: 'ChangeMe123!', role: 'Admin' }
];

let invoices = [
  { id: 'INV-1001', customerName: 'Lakeside School District', amount: 4250.00, status: 'PendingApproval' },
  { id: 'INV-1002', customerName: 'Hilltop Academy', amount: 45000.00, status: 'PendingApproval' },
  { id: 'INV-1003', customerName: 'Riverside Charter', amount: 120000.00, status: 'PendingApproval' },
  { id: 'INV-1004', customerName: 'Apex Enterprise Corp', amount: 750000.00, status: 'PendingApproval' },
  { id: 'INV-1005', customerName: 'Maple Grove Prep', amount: 310.00, status: 'Draft' }
];

let nextNumber = 1006;
const sessions = new Map(); // token -> { username, role }

function sendJson(res, statusCode, data) {
  res.writeHead(statusCode, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify(data));
}

function parseBody(req) {
  return new Promise((resolve) => {
    let body = '';
    req.on('data', chunk => body += chunk);
    req.on('end', () => {
      try {
        resolve(body ? JSON.parse(body) : {});
      } catch {
        resolve({});
      }
    });
  });
}

function getSession(req) {
  const auth = req.headers['authorization'] || '';
  if (auth.startsWith('Bearer ')) {
    return sessions.get(auth.slice(7));
  }
  return null;
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);
  const pathname = url.pathname;

  // Static files
  if (!pathname.startsWith('/api')) {
    let filePath = path.join(WWWROOT, pathname === '/' ? 'index.html' : pathname);
    if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
      const ext = path.extname(filePath);
      const mimeTypes = {
        '.html': 'text/html',
        '.js': 'text/javascript',
        '.css': 'text/css'
      };
      res.writeHead(200, { 'Content-Type': mimeTypes[ext] || 'application/octet-stream' });
      return fs.createReadStream(filePath).pipe(res);
    }
    res.writeHead(404);
    return res.end('Not found');
  }

  // API Endpoints
  // POST /api/auth/login
  if (pathname === '/api/auth/login' && req.method === 'POST') {
    const { username, password } = await parseBody(req);
    const user = users.find(u => u.username === username && u.password === password);
    if (!user) {
      return sendJson(res, 401, { error: 'Invalid username or password.' });
    }
    const token = 'token-' + Math.random().toString(36).slice(2);
    sessions.set(token, { username: user.username, role: user.role });
    return sendJson(res, 200, { token, username: user.username, role: user.role });
  }

  // POST /api/auth/logout
  if (pathname === '/api/auth/logout' && req.method === 'POST') {
    const auth = req.headers['authorization'] || '';
    if (auth.startsWith('Bearer ')) {
      sessions.delete(auth.slice(7));
    }
    res.writeHead(204);
    return res.end();
  }

  // Require auth for remaining APIs
  const session = getSession(req);
  if (!session) {
    return sendJson(res, 401, { error: 'Unauthorized.' });
  }

  // GET /api/invoices
  if (pathname === '/api/invoices' && req.method === 'GET') {
    return sendJson(res, 200, invoices);
  }

  // POST /api/invoices
  if (pathname === '/api/invoices' && req.method === 'POST') {
    const { customerName, amount } = await parseBody(req);
    const newInvoice = {
      id: `INV-${nextNumber++}`,
      customerName: customerName || 'Unknown',
      amount: parseFloat(amount) || 0,
      status: 'Draft',
      createdAt: new Date().toISOString()
    };
    invoices.push(newInvoice);
    return sendJson(res, 201, newInvoice);
  }

  // POST /api/invoices/:id/submit
  const submitMatch = pathname.match(/^\/api\/invoices\/([^/]+)\/submit$/);
  if (submitMatch && req.method === 'POST') {
    const inv = invoices.find(i => i.id === submitMatch[1]);
    if (!inv) return sendJson(res, 404, { error: 'Invoice not found.' });
    if (inv.status !== 'Draft') return sendJson(res, 409, { error: 'Only drafts can be submitted.' });
    inv.status = 'PendingApproval';
    return sendJson(res, 200, inv);
  }

  // POST /api/approvals/:id/approve
  const approveMatch = pathname.match(/^\/api\/approvals\/([^/]+)\/approve$/);
  if (approveMatch && req.method === 'POST') {
    const inv = invoices.find(i => i.id === approveMatch[1]);
    if (!inv) return sendJson(res, 404, { error: 'Invoice not found.' });
    if (inv.status !== 'PendingApproval') return sendJson(res, 409, { error: 'Only pending invoices can be approved.' });

    const isManager = session.role.toLowerCase() === 'manager';
    const isDirector = session.role.toLowerCase() === 'director';

    if (!isManager && !isDirector) {
      return sendJson(res, 409, { error: `Role '${session.role}' is not authorized to approve invoices.` });
    }

    // Rule 1: Manager <= 50,000
    if (inv.amount <= 50000) {
      inv.status = 'Approved';
      inv.decidedBy = session.username;
      inv.decidedAt = new Date().toISOString();
      return sendJson(res, 200, inv);
    }

    // Rule 2: Director <= 500,000
    if (inv.amount <= 500000) {
      if (isManager) {
        return sendJson(res, 409, { error: `Manager cannot approve invoice ${inv.id} exceeding $50,000. Requires a Director.` });
      }
      inv.status = 'Approved';
      inv.decidedBy = session.username;
      inv.decidedAt = new Date().toISOString();
      return sendJson(res, 200, inv);
    }

    // Rule 3: > 500,000 requires two distinct approvers
    if (!inv.firstApprover) {
      inv.firstApprover = session.username;
      inv.firstApprovedAt = new Date().toISOString();
      // Remains PendingApproval
      return sendJson(res, 200, inv);
    }

    // Rule 4: Distinct approvers
    if (inv.firstApprover.toLowerCase() === session.username.toLowerCase()) {
      return sendJson(res, 409, { error: `The same user ('${session.username}') cannot provide both approvals for invoice ${inv.id}.` });
    }

    // Rule 3 (second approver must be Director)
    if (!isDirector) {
      return sendJson(res, 409, { error: `The second approver for invoice ${inv.id} exceeding $500,000 must be a Director.` });
    }

    inv.status = 'Approved';
    inv.decidedBy = session.username;
    inv.decidedAt = new Date().toISOString();
    return sendJson(res, 200, inv);
  }

  // POST /api/approvals/:id/reject
  const rejectMatch = pathname.match(/^\/api\/approvals\/([^/]+)\/reject$/);
  if (rejectMatch && req.method === 'POST') {
    const { reason } = await parseBody(req);
    if (!reason || !reason.trim()) {
      return sendJson(res, 400, { error: 'A reason is required to reject an invoice.' });
    }
    const inv = invoices.find(i => i.id === rejectMatch[1]);
    if (!inv) return sendJson(res, 404, { error: 'Invoice not found.' });
    if (inv.status !== 'PendingApproval') return sendJson(res, 409, { error: 'Only pending invoices can be decided.' });

    inv.status = 'Rejected';
    inv.decidedBy = session.username;
    inv.decidedAt = new Date().toISOString();
    inv.decisionNote = reason;
    return sendJson(res, 200, inv);
  }

  res.writeHead(404);
  res.end('Not found');
});

server.listen(PORT, () => {
  console.log(`Invoice Approval App running at http://localhost:${PORT}`);
  console.log(`Demo accounts:`);
  console.log(`  Manager:  demo.manager / ChangeMe123!`);
  console.log(`  Director: demo.director / ChangeMe123!`);
  console.log(`  Admin:    demo.admin / ChangeMe123!`);
});
