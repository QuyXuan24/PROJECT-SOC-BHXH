import { guardPage, logout } from '/js/auth-guard.js';
import {
    getApplicationTimeline,
    getMyApplications,
    getMyProfile,
    submitInsuranceProfile
} from '/services/userApi.js';
import { getMyPayments } from '/services/paymentApi.js';
import { getToken } from '/services/tokenService.js';
import { formatDate, formatDateOnly, formatDateTime, formatGender, formatPhone, formatBhxhCode, formatCurrency } from '/js/formatters.js';

if (!guardPage(['User'])) {
    throw new Error('Unauthorized');
}

const token = getToken();
const profileForm = document.getElementById('profileForm');
const profileAlert = document.getElementById('profileAlert');
const profileStatus = document.getElementById('profileStatus');
const btnSaveProfile = document.getElementById('btnSaveProfile');
const btnOpenSubmitPage = document.getElementById('btnOpenSubmitPage');
const btnOpenPaymentPage = document.getElementById('btnOpenPaymentPage');
const btnOpenPaymentPageFooter = document.getElementById('btnOpenPaymentPageFooter');
const tableBody = document.getElementById('applicationTableBody');
const paymentSummary = document.getElementById('paymentSummary');
const detailMeta = document.getElementById('detailMeta');
const detailProfileContainer = document.getElementById('detailProfileContainer');
const detailTimeline = document.getElementById('detailTimeline');
const modalElement = document.getElementById('applicationDetailModal');
const detailModal = window.bootstrap?.Modal && modalElement
    ? new window.bootstrap.Modal(modalElement)
    : null;

let currentProfile = null;

document.getElementById('btnLogout')?.addEventListener('click', () => logout('/pages/auth.html'));
btnOpenSubmitPage?.addEventListener('click', () => {
    window.location.href = '/pages/insurance-input.html';
});
btnOpenPaymentPage?.addEventListener('click', () => {
    window.location.href = '/pages/payment.html';
});
btnOpenPaymentPageFooter?.addEventListener('click', () => {
    window.location.href = '/pages/payment.html';
});
profileForm?.addEventListener('submit', onSaveProfile);

loadPageData();

async function loadPageData() {
    await Promise.all([loadProfile(), loadApplications(), loadPaymentSummary()]);
}

async function loadProfile() {
    try {
        currentProfile = await getMyProfile(token);
        fillProfileForm(currentProfile);
        renderProfileStatus(currentProfile);
        hideProfileAlert();
    } catch (error) {
        if (error.status === 404) {
            currentProfile = null;
            profileForm?.reset();
            profileStatus.textContent = 'Chua co ho so. Hay nhap thong tin va bam "Luu thong tin".';
            showProfileAlert('info', error.message || 'Chua co ho so.');
            return;
        }

        showProfileAlert('danger', error.message || 'Không thể tải thông tin cá nhân.');
    }
}

async function loadApplications() {
    try {
        const apps = await getMyApplications(token);
        renderApplications(apps);
    } catch (error) {
        tableBody.innerHTML = `<tr><td colspan="5" class="text-danger">${escapeHtml(error.message || 'Không tải được danh sách hồ sơ.')}</td></tr>`;
    }
}

async function loadPaymentSummary() {
    if (!paymentSummary) {
        return;
    }

    try {
        const payments = await getMyPayments(token);
        renderPaymentSummary(payments || []);
    } catch (error) {
        paymentSummary.innerHTML = `<span class="text-danger">${escapeHtml(error.message || 'Không thể tải trạng thái thanh toán.')}</span>`;
    }
}

function renderPaymentSummary(payments = []) {
    if (!paymentSummary) {
        return;
    }

    if (!payments.length) {
        paymentSummary.innerHTML = '<div class="text-muted">Chưa có yêu cầu thanh toán nào. Nhấn nút đóng phí BHXH để tạo yêu cầu.</div>';
        return;
    }

    const latest = payments[0];
    paymentSummary.innerHTML = `
        <div class="row g-2">
            <div class="col-md-6">
                <div><strong>Mã thanh toán:</strong> ${escapeHtml(latest.paymentCode || '-')}</div>
                <div><strong>Mã BHXH:</strong> ${escapeHtml(formatBhxhCode(latest.bhxhCode))}</div>
                <div><strong>Số tiền:</strong> ${escapeHtml(formatCurrency(latest.amount))}</div>
            </div>
            <div class="col-md-6">
                <div><strong>Trạng thái:</strong> ${formatPaymentStatus(latest.status)}</div>
                <div><strong>Ngày tạo:</strong> ${escapeHtml(formatDateTime(latest.createdAt))}</div>
                <div><strong>Ghi chú:</strong> ${escapeHtml(latest.reviewNote || '-')}</div>
            </div>
        </div>
    `;
}

function formatPaymentStatus(status) {
    if (status === 'Pending') return '<span class="badge bg-warning text-dark">Đang chờ kiểm tra</span>';
    if (status === 'Confirmed') return '<span class="badge bg-success">Đã nộp phí thành công</span>';
    if (status === 'Rejected') return '<span class="badge bg-danger">Bị từ chối</span>';
    return `<span class="badge bg-secondary">${escapeHtml(status)}</span>`;
}

function renderApplications(apps = []) {
    if (!Array.isArray(apps) || apps.length === 0) {
        tableBody.innerHTML = '<tr><td colspan="5" class="text-muted text-center py-3">Chua co ho so nao.</td></tr>';
        return;
    }

    tableBody.innerHTML = '';
    apps.forEach((app) => {
        const id = pickValue(app, 'id', 'Id');
        const status = pickValue(app, 'status', 'Status');
        const submittedAt = pickValue(app, 'submittedAt', 'SubmittedAt');
        const updatedAt = pickValue(app, 'updatedAt', 'UpdatedAt');

        const row = document.createElement('tr');
        row.innerHTML = `
            <td>#${escapeHtml(id)}</td>
            <td><span class="badge ${statusBadgeClass(status)}">${escapeHtml(status || '-')}</span></td>
            <td>${escapeHtml(formatDateTime(submittedAt))}</td>
            <td>${escapeHtml(formatDateTime(updatedAt))}</td>
            <td>
                <button class="btn btn-sm btn-outline-primary" type="button">Xem</button>
            </td>
        `;

        row.querySelector('button')?.addEventListener('click', () => {
            openApplicationDetail(app);
        });

        tableBody.appendChild(row);
    });
}

async function openApplicationDetail(app) {
    const id = pickValue(app, 'id', 'Id');
    const status = pickValue(app, 'status', 'Status');
    const submittedAt = pickValue(app, 'submittedAt', 'SubmittedAt');
    const updatedAt = pickValue(app, 'updatedAt', 'UpdatedAt');
    const failureReason = pickValue(app, 'failureReason', 'FailureReason');

    detailMeta.innerHTML = `
        <div><strong>Ma ho so:</strong> #${escapeHtml(id)}</div>
        <div><strong>Trang thai:</strong> <span class="badge ${statusBadgeClass(status)}">${escapeHtml(status || '-')}</span></div>
        <div><strong>Ngay nop:</strong> ${escapeHtml(formatDateTime(submittedAt))}</div>
        <div><strong>Cap nhat:</strong> ${escapeHtml(formatDateTime(updatedAt))}</div>
        <div><strong>Ghi chu:</strong> ${escapeHtml(failureReason || '-')}</div>
    `;

    renderDetailProfile(id);

    detailTimeline.innerHTML = '<li class="list-group-item text-muted">Đang tải timeline...</li>';

    try {
        const timeline = await getApplicationTimeline(token, id);
        if (!timeline || timeline.length === 0) {
            detailTimeline.innerHTML = '<li class="list-group-item text-muted">Chua co timeline.</li>';
        } else {
            detailTimeline.innerHTML = '';
            timeline.forEach((item) => {
                const createdAt = pickValue(item, 'CreatedAt', 'createdAt');
                const username = pickValue(item, 'Username', 'username');
                const action = pickValue(item, 'Action', 'action');
                const content = pickValue(item, 'Content', 'content');

                const li = document.createElement('li');
                li.className = 'list-group-item';
                li.innerHTML = `
                    <div class="d-flex justify-content-between">
                        <strong>${escapeHtml(action || '-')}</strong>
                        <small class="text-muted">${escapeHtml(formatDateTime(createdAt))}</small>
                    </div>
                    <div>${escapeHtml(content || '')}</div>
                    <small class="text-muted">Nguoi thuc hien: ${escapeHtml(username || '-')}</small>
                `;
                detailTimeline.appendChild(li);
            });
        }
    } catch (error) {
        detailTimeline.innerHTML = `<li class="list-group-item text-danger">${escapeHtml(error.message || 'Không thể tải timeline.')}</li>`;
    }

    if (detailModal) {
        detailModal.show();
    }
}

function renderDetailProfile(applicationId) {
    if (!currentProfile) {
        detailProfileContainer.innerHTML = '<p class="text-muted mb-0">Chua co du lieu profile de hien thi.</p>';
        return;
    }

    const profileRecordId = Number(pickValue(currentProfile, 'RecordId', 'recordId') || 0);
    const appId = Number(applicationId || 0);
    const mismatchNotice = profileRecordId && appId && profileRecordId !== appId
        ? '<div class="alert alert-warning py-2 mb-3">API hien tai chi tra ve profile hien tai. Du lieu chi tiet ben duoi la profile moi nhat cua ban.</div>'
        : '';

    detailProfileContainer.innerHTML = `
        ${mismatchNotice}
        <div class="row g-2">
            ${renderDetailField('Ho va ten', pickValue(currentProfile, 'FullName', 'fullName'))}
            ${renderDetailField('Ngay sinh', formatDateOnly(pickValue(currentProfile, 'DateOfBirth', 'dateOfBirth')))}
            ${renderDetailField('Gioi tinh', formatGender(pickValue(currentProfile, 'Gender', 'gender')))}
            ${renderDetailField('CCCD', pickValue(currentProfile, 'Cccd', 'cccd'))}
            ${renderDetailField('So dien thoai', formatPhone(pickValue(currentProfile, 'PhoneNumber', 'phoneNumber')))}
            ${renderDetailField('Dia chi', pickValue(currentProfile, 'Address', 'address'))}
            ${renderDetailField('Ma so BHXH', formatBhxhCode(pickValue(currentProfile, 'BhxhCode', 'bhxhCode')))}
        </div>
    `;
}

function renderDetailField(label, value) {
    return `
        <div class="col-md-6">
            <div class="border rounded p-2 h-100">
                <div class="text-muted small">${escapeHtml(label)}</div>
                <div class="fw-semibold">${escapeHtml(value || '-')}</div>
            </div>
        </div>
    `;
}

async function onSaveProfile(event) {
    event.preventDefault();
    const payload = getProfilePayload();

    if (!payload.bhxhCode || !/^[0-9]{10}$/.test(payload.bhxhCode)) {
        showProfileAlert('danger', 'Mã số BHXH phải đúng 10 chữ số.');
        return;
    }

    if (!payload.fullName || !payload.dateOfBirth || !payload.gender || !payload.cccd || !payload.phoneNumber || !payload.address) {
        showProfileAlert('danger', 'Vui lòng điền đầy đủ thông tin profile.');
        return;
    }

    btnSaveProfile.disabled = true;
    btnSaveProfile.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Đang lưu...';

    try {
        await submitInsuranceProfile(token, payload);
        showProfileAlert('success', 'Da luu thong tin profile thanh cong.');
        await loadPageData();
    } catch (error) {
        showProfileAlert('danger', error.message || 'Không thể lưu profile.');
    } finally {
        btnSaveProfile.disabled = false;
        btnSaveProfile.innerHTML = '<i class="fas fa-save me-1"></i>Luu thong tin';
    }
}

function getProfilePayload() {
    return {
        fullName: (profileForm.fullName.value || '').trim(),
        dateOfBirth: profileForm.dateOfBirth.value,
        gender: (profileForm.gender.value || '').trim(),
        cccd: (profileForm.cccd.value || '').trim(),
        phoneNumber: (profileForm.phoneNumber.value || '').trim(),
        address: (profileForm.address.value || '').trim(),
        bhxhCode: (profileForm.bhxhCode.value || '').trim()
    };
}

function fillProfileForm(profile) {
    if (!profileForm || !profile) {
        return;
    }

    profileForm.fullName.value = pickValue(profile, 'FullName', 'fullName') || '';
    profileForm.dateOfBirth.value = toInputDate(pickValue(profile, 'DateOfBirth', 'dateOfBirth'));
    profileForm.gender.value = pickValue(profile, 'Gender', 'gender') || '';
    profileForm.cccd.value = pickValue(profile, 'Cccd', 'cccd') || '';
    profileForm.phoneNumber.value = pickValue(profile, 'PhoneNumber', 'phoneNumber') || '';
    profileForm.address.value = pickValue(profile, 'Address', 'address') || '';
    profileForm.bhxhCode.value = pickValue(profile, 'BhxhCode', 'bhxhCode') || '';
}

function renderProfileStatus(profile) {
    if (!profileStatus) {
        return;
    }

    const recordId = pickValue(profile, 'RecordId', 'recordId') || '-';
    const status = pickValue(profile, 'Status', 'status') || '-';
    const note = pickValue(profile, 'Note', 'note') || '-';

    profileStatus.innerHTML = `
        <span class="me-3"><strong>Ma ho so:</strong> #${escapeHtml(recordId)}</span>
        <span class="me-3"><strong>Trang thai:</strong> <span class="badge ${statusBadgeClass(status)}">${escapeHtml(status)}</span></span>
        <span><strong>Ghi chu:</strong> ${escapeHtml(note)}</span>
    `;
}

function showProfileAlert(type, message) {
    if (!profileAlert) {
        return;
    }

    profileAlert.className = `alert alert-${type}`;
    profileAlert.textContent = message;
    profileAlert.classList.remove('d-none');
}

function hideProfileAlert() {
    if (!profileAlert) {
        return;
    }

    profileAlert.classList.add('d-none');
}

function toInputDate(value) {
    if (!value) {
        return '';
    }

    if (typeof value === 'string') {
        const slashMatch = value.match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
        if (slashMatch) {
            return `${slashMatch[3]}-${slashMatch[2]}-${slashMatch[1]}`;
        }

        const isoMatch = value.match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (isoMatch) {
            return `${isoMatch[1]}-${isoMatch[2]}-${isoMatch[3]}`;
        }
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return '';
    }

    return date.toISOString().slice(0, 10);
}

function pickValue(obj, ...keys) {
    for (const key of keys) {
        if (obj && obj[key] !== undefined && obj[key] !== null) {
            return obj[key];
        }
    }
    return '';
}

function statusBadgeClass(status) {
    if (status === 'Approved') return 'bg-success';
    if (status === 'Rejected') return 'bg-danger';
    if (status === 'Cancelled') return 'bg-secondary';
    return 'bg-warning text-dark';
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}
