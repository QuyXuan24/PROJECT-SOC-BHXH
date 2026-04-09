import { guardPage, logout } from '/js/auth-guard.js';
import { getApplicationsForEmployee, processApplication } from '/services/employeeApi.js';
import { getPaymentRequestsForStaff, reviewPaymentRequest } from '/services/paymentApi.js';
import { getToken } from '/services/tokenService.js';
import { formatDate, formatDateOnly, formatGender, formatPhone, formatBhxhCode, formatCurrency } from '/js/formatters.js';

if (!guardPage(['Employee', 'Staff'], '/pages/login.html')) {
    throw new Error('Unauthorized');
}

const token = getToken();
const tableBody = document.getElementById('employeeApplicationBody');
const detailModalElement = document.getElementById('detailModal');
const detailModal = detailModalElement ? new bootstrap.Modal(detailModalElement) : null;
const searchInput = document.getElementById('searchInput');
const filterStatus = document.getElementById('filterStatus');
const btnApplyFilter = document.getElementById('btnApplyFilter');
const btnClearFilter = document.getElementById('btnClearFilter');
const btnReloadData = document.getElementById('btnReloadData');
const paymentSearchInput = document.getElementById('paymentSearchInput');
const paymentFilterStatus = document.getElementById('paymentFilterStatus');
const btnReloadPayments = document.getElementById('btnReloadPayments');
const btnClearPaymentFilter = document.getElementById('btnClearPaymentFilter');
const statPending = document.getElementById('statPending');
const statApproved = document.getElementById('statApproved');
const statRejected = document.getElementById('statRejected');
const notificationContainer = document.getElementById('notificationContainer');
const userInfo = document.getElementById('userInfo');

let allRecords = [];
let allPaymentRequests = [];
let autoRefreshInterval = null;

document.getElementById('btnLogout')?.addEventListener('click', () => logout('/pages/login.html'));
btnApplyFilter?.addEventListener('click', () => filterAndDisplay());
btnClearFilter?.addEventListener('click', () => {
    searchInput.value = '';
    filterStatus.value = '';
    filterAndDisplay();
});
btnReloadData?.addEventListener('click', () => loadPageData());
paymentSearchInput?.addEventListener('input', () => filterAndDisplayPayments());
paymentFilterStatus?.addEventListener('change', () => filterAndDisplayPayments());
btnReloadPayments?.addEventListener('click', () => loadPaymentRequests());
btnClearPaymentFilter?.addEventListener('click', () => {
    paymentSearchInput.value = '';
    paymentFilterStatus.value = '';
    filterAndDisplayPayments();
});

// Auto refresh every 5 minutes
autoRefreshInterval = setInterval(() => loadPageData(), 300000);

// Load user info, applications and payment requests
loadUserInfo();
loadPageData();

async function loadUserInfo() {
    const userStr = localStorage.getItem('user');
    if (userStr) {
        const user = JSON.parse(userStr);
        userInfo.textContent = `Xin chào: ${user.username || 'Nhân viên'}`;
    }
}

async function loadApplications() {
    tableBody.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-3"><i class="fas fa-spinner fa-spin me-2"></i>Đang tải...</td></tr>';
    
    try {
        const records = await getApplicationsForEmployee(token, {});
        allRecords = records || [];
        updateStats();
        filterAndDisplay();
    } catch (error) {
        tableBody.innerHTML = `<tr><td colspan="7" class="text-center text-danger py-3">${escapeHtml(error.message || 'Không thể tải dữ liệu.')}</td></tr>`;
    }
}

async function loadPaymentRequests() {
    const paymentTableBody = document.getElementById('paymentRequestBody');
    if (!paymentTableBody) return;

    paymentTableBody.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-3"><i class="fas fa-spinner fa-spin me-2"></i>Đang tải yêu cầu thanh toán...</td></tr>';

    try {
        const requests = await getPaymentRequestsForStaff(token, {});
        allPaymentRequests = requests || [];
        filterAndDisplayPayments();
    } catch (error) {
        paymentTableBody.innerHTML = `<tr><td colspan="7" class="text-center text-danger py-3">${escapeHtml(error.message || 'Không thể tải danh sách thanh toán.')}</td></tr>`;
    }
}

async function loadPageData() {
    await Promise.all([loadApplications(), loadPaymentRequests()]);
}

function updateStats() {
    const pending = allRecords.filter(r => r.status === 'Pending').length;
    const approved = allRecords.filter(r => r.status === 'Approved').length;
    const rejected = allRecords.filter(r => r.status === 'Rejected').length;
    
    statPending.textContent = pending;
    statApproved.textContent = approved;
    statRejected.textContent = rejected;
}

function filterAndDisplay() {
    const searchTerm = searchInput.value.toLowerCase().trim();
    const statusFilter = filterStatus.value.trim();
    
    let filtered = allRecords;
    
    if (statusFilter) {
        filtered = filtered.filter(r => r.status === statusFilter);
    }
    
    if (searchTerm) {
        filtered = filtered.filter(r => 
            (r.fullName || '').toLowerCase().includes(searchTerm) ||
            String(r.id).includes(searchTerm) ||
            (r.cccd || '').includes(searchTerm)
        );
    }
    
    renderApplications(filtered);
}

function filterAndDisplayPayments() {
    const searchTerm = paymentSearchInput.value.toLowerCase().trim();
    const statusFilter = paymentFilterStatus.value.trim();
    const paymentTableBody = document.getElementById('paymentRequestBody');

    if (!paymentTableBody) return;

    let filtered = allPaymentRequests;
    if (statusFilter) {
        filtered = filtered.filter(r => r.status === statusFilter);
    }

    if (searchTerm) {
        filtered = filtered.filter(r =>
            (r.paymentCode || '').toLowerCase().includes(searchTerm) ||
            (r.bhxhCode || '').includes(searchTerm) ||
            (r.processedBy || '').toLowerCase().includes(searchTerm) ||
            (r.userName || '').toLowerCase().includes(searchTerm) ||
            String(r.id).includes(searchTerm)
        );
    }

    renderPaymentRequests(filtered);
}

function renderApplications(records = []) {
    if (!records.length) {
        tableBody.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-3">Không có hồ sơ nào.</td></tr>';
        return;
    }

    tableBody.innerHTML = '';
    records.forEach((record) => {
        const canProcess = record.status === 'Pending';
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>#${escapeHtml(String(record.id))}</td>
            <td><strong>${escapeHtml(record.fullName || '-')}</strong></td>
            <td><span class="badge ${statusBadgeClass(record.status)}">${escapeHtml(record.status || '-')}</span></td>
            <td>${escapeHtml(formatDate(record.createdAt))}</td>
            <td>${escapeHtml(formatDate(record.updatedAt))}</td>
            <td>${escapeHtml(record.processedBy || '-')}</td>
            <td class="text-center">
                <div class="btn-group btn-group-sm" role="group">
                    <button class="btn btn-outline-info action-btn" data-action="view" data-id="${record.id}" title="Xem chi tiết">
                        <i class="fas fa-eye"></i>
                    </button>
                    ${canProcess ? `
                        <button class="btn btn-outline-success action-btn" data-action="approve" data-id="${record.id}" title="Duyệt hồ sơ">
                            <i class="fas fa-check"></i>
                        </button>
                        <button class="btn btn-outline-danger action-btn" data-action="reject" data-id="${record.id}" title="Từ chối hồ sơ">
                            <i class="fas fa-times"></i>
                        </button>
                    ` : ''}
                    <button class="btn btn-outline-warning action-btn" data-action="lock" data-id="${record.id}" title="Khóa hồ sơ">
                        <i class="fas fa-lock"></i>
                    </button>
                </div>
            </td>
        `;
        tableBody.appendChild(row);
    });

    bindActions();
}

function renderPaymentRequests(records = []) {
    const paymentTableBody = document.getElementById('paymentRequestBody');
    if (!paymentTableBody) return;

    if (!records.length) {
        paymentTableBody.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-3">Không có yêu cầu thanh toán nào.</td></tr>';
        return;
    }

    paymentTableBody.innerHTML = '';
    records.forEach((request) => {
        const canApprove = request.status === 'Pending';
        const canReject = request.status === 'Pending' || request.status === 'Cancelled';
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>#${escapeHtml(String(request.id))}</td>
            <td>${escapeHtml(request.userName || (request.userId ? `Người dùng #${request.userId}` : request.paymentCode || '-'))}</td>
            <td>${escapeHtml(formatBhxhCode(request.bhxhCode || '-'))}</td>
            <td>${escapeHtml(formatCurrency(request.amount))} ${escapeHtml(request.currency || '')}</td>
            <td><span class="badge ${statusBadgeClass(request.status)}">${escapeHtml(request.status || '-')}</span></td>
            <td>${escapeHtml(formatDate(request.createdAt))}</td>
            <td class="text-center">
                <div class="btn-group btn-group-sm" role="group">
                    ${canApprove ? `
                        <button class="btn btn-outline-success action-btn" data-action="confirm-payment" data-id="${request.id}" title="Xác nhận thanh toán">
                            <i class="fas fa-check"></i>
                        </button>
                    ` : ''}
                    ${canReject ? `
                        <button class="btn btn-outline-danger action-btn" data-action="reject-payment" data-id="${request.id}" title="Từ chối yêu cầu thanh toán">
                            <i class="fas fa-times"></i>
                        </button>
                    ` : ''}
                </div>
            </td>
        `;
        paymentTableBody.appendChild(row);
    });

    paymentTableBody.querySelectorAll('button[data-action]').forEach((button) => {
        button.addEventListener('click', async () => {
            const action = button.dataset.action;
            const paymentId = Number(button.dataset.id);
            if (!paymentId) return;

            if (action === 'confirm-payment') {
                await reviewPaymentRequestAction(paymentId, 'Approved');
            }
            if (action === 'reject-payment') {
                await reviewPaymentRequestAction(paymentId, 'Rejected');
            }
        });
    });
}

function bindActions() {
    tableBody.querySelectorAll('button[data-action]').forEach((button) => {
        button.addEventListener('click', async () => {
            const action = button.dataset.action;
            const recordId = Number(button.dataset.id);
            const record = allRecords.find(r => r.id === recordId);

            if (!record) return;

            switch (action) {
                case 'view':
                    showDetailModal(record);
                    break;
                case 'approve':
                    await approveRecord(recordId);
                    break;
                case 'reject':
                    await rejectRecord(recordId);
                    break;
                case 'lock':
                    await lockRecord(recordId);
                    break;
            }
        });
    });
}

function showDetailModal(record) {
    const detailMeta = document.getElementById('detailMeta');
    const detailProfile = document.getElementById('detailProfile');
    const detailTimeline = document.getElementById('detailTimeline');

    detailMeta.innerHTML = `
        <div class="row">
            <div class="col-md-6">
                <div><strong>Mã hồ sơ:</strong> #${escapeHtml(String(record.id))}</div>
                <div><strong>Trạng thái:</strong> <span class="badge ${statusBadgeClass(record.status)}">${escapeHtml(record.status)}</span></div>
            </div>
            <div class="col-md-6">
                <div><strong>Người nộp:</strong> ${escapeHtml(record.fullName || '-')}</div>
                <div><strong>Ngày nộp:</strong> ${escapeHtml(formatDate(record.createdAt))}</div>
            </div>
        </div>
    `;

    detailProfile.innerHTML = `
        <div class="col-md-6">
            <div class="border rounded p-2 h-100">
                <div class="text-muted small">Họ và tên</div>
                <div class="fw-semibold">${escapeHtml(record.fullName || '-')}</div>
            </div>
        </div>
        <div class="col-md-6">
            <div class="border rounded p-2 h-100">
                <div class="text-muted small">CCCD</div>
                <div class="fw-semibold">${escapeHtml(record.cccd || '-')}</div>
            </div>
        </div>
        <div class="col-md-6">
            <div class="border rounded p-2 h-100">
                <div class="text-muted small">Ngày sinh</div>
                <div class="fw-semibold">${escapeHtml(formatDateOnly(record.dateOfBirth))}</div>
            </div>
        </div>
        <div class="col-md-6">
            <div class="border rounded p-2 h-100">
                <div class="text-muted small">Giới tính</div>
                <div class="fw-semibold">${escapeHtml(formatGender(record.gender))}</div>
            </div>
        </div>
        <div class="col-md-6">
            <div class="border rounded p-2 h-100">
                <div class="text-muted small">Điện thoại</div>
                <div class="fw-semibold">${escapeHtml(formatPhone(record.phoneNumber))}</div>
            </div>
        </div>
        <div class="col-md-6">
            <div class="border rounded p-2 h-100">
                <div class="text-muted small">Mã BHXH</div>
                <div class="fw-semibold">${escapeHtml(formatBhxhCode(record.bhxhCode))}</div>
            </div>
        </div>
    `;

    detailTimeline.innerHTML = '<li class="list-group-item text-muted"><small>Không có lịch sử xử lý.</small></li>';

    detailModal.show();
}

async function approveRecord(recordId) {
    try {
        await processApplication(token, recordId, 'Approved', 'Hồ sơ hợp lệ.');
        showNotification('✅ Đã duyệt hồ sơ #' + recordId, 'success');
        await loadApplications();
    } catch (error) {
        showNotification('❌ ' + (error.message || 'Không thể duyệt hồ sơ.'), 'danger');
    }
}

async function rejectRecord(recordId) {
    const note = prompt('Nhập lý do từ chối hồ sơ:', '');
    if (!note) return;

    try {
        await processApplication(token, recordId, 'Rejected', note);
        showNotification('❌ Đã từ chối hồ sơ #' + recordId, 'warning');
        await loadPageData();
    } catch (error) {
        showNotification('❌ ' + (error.message || 'Không thể từ chối hồ sơ.'), 'danger');
    }
}

async function lockRecord(recordId) {
    const reason = prompt('Nhập lý do khóa hồ sơ (nghi ngờ, cần kiểm tra):', '');
    if (!reason) return;

    try {
        await processApplication(token, recordId, 'Cancelled', 'Khóa: ' + reason);
        showNotification('🔒 Đã khóa hồ sơ #' + recordId, 'info');
        await loadApplications();
    } catch (error) {
        showNotification('❌ ' + (error.message || 'Không thể khóa hồ sơ.'), 'danger');
    }
}

function showNotification(message, type = 'info') {
    const notif = document.createElement('div');
    notif.className = `alert alert-${type} alert-dismissible fade show`;
    notif.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    notificationContainer.appendChild(notif);

    const alert = new bootstrap.Alert(notif);
    setTimeout(() => alert.close(), 5000);
}

function statusBadgeClass(status) {
    if (status === 'Approved') return 'bg-success';
    if (status === 'Rejected') return 'bg-danger';
    if (status === 'Cancelled') return 'bg-secondary';
    if (status === 'Pending') return 'bg-warning text-dark';
    if (status === 'Confirmed') return 'bg-success';
    return 'bg-info';
}

async function reviewPaymentRequestAction(paymentId, action) {
    let note = '';
    if (action === 'Rejected') {
        note = prompt('Nhập lý do từ chối yêu cầu thanh toán:', '');
        if (!note) return;
    }

    try {
        await reviewPaymentRequest(token, paymentId, action, note);
        showNotification(action === 'Approved'
            ? '✅ Đã xác nhận yêu cầu thanh toán #' + paymentId
            : '❌ Đã từ chối yêu cầu thanh toán #' + paymentId,
            action === 'Approved' ? 'success' : 'warning');
        await loadPaymentRequests();
    } catch (error) {
        showNotification('❌ ' + (error.message || 'Không thể xử lý yêu cầu thanh toán.'), 'danger');
    }
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// Cleanup on unload
window.addEventListener('beforeunload', () => {
    if (autoRefreshInterval) clearInterval(autoRefreshInterval);
});
