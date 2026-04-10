import { guardPage, logout } from '/js/auth-guard.js';
import { getToken } from '/services/tokenService.js';
import { createPaymentRequest, getMyPayments } from '/services/paymentApi.js';
import { getMyProfile } from '/services/userApi.js';
import { formatDate, formatCurrency } from '/js/formatters.js';

const token = getToken();
if (!guardPage(['User'])) {
    window.location.href = '/pages/auth.html';
}

const paymentForm = document.getElementById('paymentForm');
const bhxhCodeInput = document.getElementById('bhxhCode');
const amountInput = document.getElementById('amount');
const amountDisplay = document.getElementById('amountDisplay');
const descriptionInput = document.getElementById('description');
const paymentAlert = document.getElementById('paymentAlert');
const resultSection = document.getElementById('resultSection');
const emptyResult = document.getElementById('emptyResult');
const paymentCodeLabel = document.getElementById('paymentCode');
const paymentAmountLabel = document.getElementById('paymentAmount');
const paymentStatusLabel = document.getElementById('paymentStatus');
const qrCodeImage = document.getElementById('qrCodeImage');
const paymentHistory = document.getElementById('paymentHistory');
const presetAmountButtons = document.querySelectorAll('.preset-amount');
const btnLogout = document.getElementById('btnLogout');
const copyAccountBtn = document.getElementById('copyAccountBtn');
const bankAccountNumberLabel = document.getElementById('bankAccountNumber');
const copyAccountNotice = document.getElementById('copyAccountNotice');

btnLogout?.addEventListener('click', () => logout());

paymentForm?.addEventListener('submit', (e) => {
    e.preventDefault();
    submitPaymentRequest();
});

amountInput?.addEventListener('input', (e) => {
    // Allow number input with optional unit suffix (k/m) and separators.
    e.target.value = e.target.value.replace(/[^0-9kmKM.,\s]/g, '');
    handleAmountInput();
});

amountInput?.addEventListener('blur', () => setAmountValue(parseAmountInput(amountInput.value)));

presetAmountButtons.forEach((button) => {
    button.addEventListener('click', (e) => {
        e.preventDefault();
        hideAlert();
        setAmountValue(Number(button.dataset.value));
        amountInput?.focus();
    });
});

copyAccountBtn?.addEventListener('click', async () => {
    const accountNumber = bankAccountNumberLabel?.textContent?.trim() || '';
    if (!accountNumber) {
        showCopyNotice('Không tìm thấy số tài khoản để copy.', false);
        return;
    }

    try {
        await navigator.clipboard.writeText(accountNumber);
        showCopyNotice('Đã copy số tài khoản thành công.', true);
    } catch {
        const fallbackInput = document.createElement('textarea');
        fallbackInput.value = accountNumber;
        fallbackInput.setAttribute('readonly', 'readonly');
        fallbackInput.style.position = 'absolute';
        fallbackInput.style.left = '-9999px';
        document.body.appendChild(fallbackInput);
        fallbackInput.select();
        document.execCommand('copy');
        document.body.removeChild(fallbackInput);
        showCopyNotice('Đã copy số tài khoản thành công.', true);
    }
});

loadPaymentPage();

async function loadPaymentPage() {
    try {
        const profile = await getMyProfile(token);
        if (profile?.bhxhCode) {
            bhxhCodeInput.value = profile.bhxhCode;
        } else {
            showAlert('Không lấy được mã BHXH. Vui lòng tải lại trang.', 'warning');
        }
    } catch (error) {
        console.error('Error loading profile:', error);
        showAlert('Không thể tải thông tin người dùng. Vui lòng tải lại trang.', 'danger');
    }

    await loadPaymentHistory();
}

async function loadPaymentHistory() {
    try {
        const payments = await getMyPayments(token);
        renderPaymentHistory(payments || []);
    } catch {
        showAlert('Không thể tải lịch sử thanh toán.', 'danger');
    }
}

function renderPaymentHistory(payments) {
    if (!payments.length) {
        paymentHistory.innerHTML = '<p class="text-muted">Bạn chưa có yêu cầu thanh toán nào.</p>';
        return;
    }

    paymentHistory.innerHTML = `
        <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
                <thead>
                    <tr>
                        <th>Phiếu</th>
                        <th>Số tiền</th>
                        <th>Trạng thái</th>
                        <th>Ngày tạo</th>
                        <th>Ghi chú</th>
                    </tr>
                </thead>
                <tbody>
                    ${payments.map((payment) => renderPaymentRow(payment)).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function renderPaymentRow(payment) {
    const paymentCode = escapeHtml(payment?.paymentCode || '-');
    const amount = formatCurrency(payment?.amount);
    const currencySuffix = payment?.currency && payment.currency !== 'VND' ? ` ${escapeHtml(payment.currency)}` : '';
    const status = formatPaymentStatus(payment?.status);
    const createdAt = escapeHtml(formatDate(payment?.createdAt));
    const note = escapeHtml(payment?.reviewNote || '');

    return `
        <tr>
            <td>${paymentCode}</td>
            <td>${amount}${currencySuffix}</td>
            <td>${status}</td>
            <td>${createdAt}</td>
            <td>${note}</td>
        </tr>
    `;
}

function formatPaymentStatus(status) {
    switch (status) {
        case 'Pending':
            return '<span class="badge bg-warning text-dark">Đang chờ kiểm tra</span>';
        case 'Confirmed':
            return '<span class="badge bg-success">Đã xác nhận</span>';
        case 'Rejected':
            return '<span class="badge bg-danger">Bị từ chối</span>';
        case 'Cancelled':
            return '<span class="badge bg-secondary">Đã hủy tự động</span>';
        default:
            return `<span class="badge bg-secondary">${escapeHtml(status || 'Unknown')}</span>`;
    }
}

async function submitPaymentRequest() {
    hideAlert();

    const bhxhCode = bhxhCodeInput.value.trim();
    if (!bhxhCode || bhxhCode.length !== 10) {
        showAlert('Vui lòng tải lại trang để lấy mã BHXH. Nếu vẫn lỗi, liên hệ hỗ trợ.', 'danger');
        return;
    }

    const amount = parseAmountInput(amountInput.value);
    if (!amount || amount <= 0) {
        showAlert('Vui lòng nhập số tiền hợp lệ (tối thiểu 100.000 VND).', 'danger');
        return;
    }

    if (amount < 100000) {
        showAlert('Số tiền tối thiểu là 100.000 VND.', 'danger');
        return;
    }

    const payload = {
        bhxhCode,
        amount,
        currency: 'VND',
        description: descriptionInput.value.trim()
    };

    try {
        const result = await createPaymentRequest(token, payload);
        showResult(result);
        showAlert(result.message || 'Yêu cầu thanh toán đã được gửi. Vui lòng chờ xác nhận.', 'success');

        amountInput.value = '';
        amountDisplay.textContent = '';
        descriptionInput.value = '';

        setTimeout(() => loadPaymentHistory(), 2000);
    } catch (error) {
        showAlert(error.message || 'Lỗi khi gửi yêu cầu thanh toán.', 'danger');
    }
}

function handleAmountInput() {
    const value = parseAmountInput(amountInput.value);
    amountDisplay.textContent = value > 0 ? `Số tiền đã chọn: ${formatCurrency(value)}` : '';
}

function parseAmountInput(input) {
    if (!input) {
        return 0;
    }

    const normalized = input.toString().trim().toLowerCase().replace(/[\s.,]/g, '');
    if (!normalized) {
        return 0;
    }

    const lastChar = normalized.slice(-1);
    const multiplier = lastChar === 'm' ? 1000000 : lastChar === 'k' ? 1000 : 1;
    const numericText = normalized.replace(/[^0-9]/g, '');
    const amount = Number(numericText);

    if (!Number.isFinite(amount) || amount <= 0) {
        return 0;
    }

    return amount * multiplier;
}

function setAmountValue(amount) {
    if (!amount || amount <= 0) {
        amountInput.value = '';
        amountDisplay.textContent = '';
        return;
    }

    amountInput.value = String(amount);
    amountDisplay.textContent = `Số tiền đã chọn: ${formatCurrency(amount)}`;
}

function showResult(result) {
    if (!result) {
        showAlert('Không nhận được kết quả từ máy chủ.', 'danger');
        return;
    }

    if (!result.paymentCode) {
        showAlert('Dữ liệu trả về từ máy chủ không hợp lệ.', 'danger');
        console.error('Invalid response:', result);
        return;
    }

    const qrImageUrl = getQrImageUrl(result);
    if (!qrImageUrl) {
        showAlert('Không tạo được đường dẫn QR hợp lệ.', 'danger');
        return;
    }

    paymentCodeLabel.textContent = result.paymentCode;
    paymentAmountLabel.textContent = formatCurrency(result.amount);
    paymentStatusLabel.innerHTML = '<span class="badge bg-warning text-dark">Đang chờ kiểm tra</span>';

    const escapedQrUrl = escapeHtml(qrImageUrl);
    qrCodeImage.innerHTML = `
        <img
            src="${escapedQrUrl}"
            alt="QR Code thanh toán MB Bank"
            class="img-fluid border rounded"
            style="max-width: 300px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);"
        />`;

    resultSection.classList.remove('d-none');
    emptyResult.classList.add('d-none');

    const renderedImage = qrCodeImage.querySelector('img');
    renderedImage?.addEventListener('error', () => {
        qrCodeImage.innerHTML = '<div class="alert alert-danger mb-0"><i class="fas fa-exclamation-triangle me-2"></i>Không thể tải mã QR. Vui lòng thử lại.</div>';
    });

    setTimeout(() => {
        resultSection.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }, 100);
}

function getQrImageUrl(result) {
    if (result?.qrImageUrl && typeof result.qrImageUrl === 'string') {
        return result.qrImageUrl;
    }

    if (result?.qrPayload && typeof result.qrPayload === 'string') {
        return `https://api.vietqr.io/image/${encodeURIComponent(result.qrPayload)}.png`;
    }

    return '';
}

function showAlert(message, type) {
    paymentAlert.textContent = message;
    paymentAlert.className = `alert alert-${type} mt-3`;
    paymentAlert.classList.remove('d-none');
}

function hideAlert() {
    paymentAlert.classList.add('d-none');
    paymentAlert.textContent = '';
}

function showCopyNotice(message, isSuccess) {
    if (!copyAccountNotice) {
        return;
    }

    copyAccountNotice.textContent = message;
    copyAccountNotice.classList.remove('d-none', 'text-success', 'text-danger');
    copyAccountNotice.classList.add(isSuccess ? 'text-success' : 'text-danger');

    window.clearTimeout(showCopyNotice.timeoutId);
    showCopyNotice.timeoutId = window.setTimeout(() => {
        copyAccountNotice.classList.add('d-none');
    }, 1800);
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}
