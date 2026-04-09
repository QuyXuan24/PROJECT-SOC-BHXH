import { guardPage, logout } from '/js/auth-guard.js';
import { getToken } from '/services/tokenService.js';
import { createPaymentRequest, getMyPayments } from '/services/paymentApi.js';
import { getMyProfile } from '/services/userApi.js';
import { formatDate, formatCurrency } from '/js/formatters.js';
import QRCode from 'qrcode';

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

btnLogout.addEventListener('click', () => logout());
paymentForm.addEventListener('submit', (e) => {
    e.preventDefault();
    submitPaymentRequest();
});
amountInput.addEventListener('input', (e) => {
    // Only allow numbers and k/m letters
    e.target.value = e.target.value.replace(/[^0-9kmKM]/g, '');
    handleAmountInput();
});
amountInput.addEventListener('blur', () => setAmountValue(parseAmountInput(amountInput.value)));
presetAmountButtons.forEach(button => {
    button.addEventListener('click', (e) => {
        e.preventDefault();
        hideAlert();
        setAmountValue(Number(button.dataset.value));
        amountInput.focus();
    });
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
    } catch (error) {
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
                    ${payments.map(p => renderPaymentRow(p)).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function renderPaymentRow(payment) {
    return `
        <tr>
            <td>${payment.paymentCode}</td>
            <td>${formatCurrency(payment.amount)}${payment.currency && payment.currency !== 'VND' ? ' ' + payment.currency : ''}</td>
            <td>${formatPaymentStatus(payment.status)}</td>
            <td>${formatDate(payment.createdAt)}</td>
            <td>${payment.reviewNote || ''}</td>
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
            return `<span class="badge bg-secondary">${status}</span>`;
    }
}

async function submitPaymentRequest() {
    hideAlert();

    // Validate BHXH
    const bhxhCode = bhxhCodeInput.value.trim();
    if (!bhxhCode) {
        showAlert('Vui lòng tải lại trang để lấy mã BHXH. Nếu vẫn lỗi, liên hệ hỗ trợ.', 'danger');
        return;
    }

    // Validate amount
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
        bhxhCode: bhxhCode,
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
        
        // Reload payment history after 2 seconds
        setTimeout(() => loadPaymentHistory(), 2000);
    } catch (error) {
        showAlert(error.message || 'Lỗi khi gửi yêu cầu thanh toán.', 'danger');
    }
}

function handleAmountInput() {
    const value = parseAmountInput(amountInput.value);
    if (value > 0) {
        amountDisplay.textContent = `Số tiền đã chọn: ${formatCurrency(value)}`;
    } else {
        amountDisplay.textContent = '';
    }
}

function parseAmountInput(input) {
    if (!input) {
        return 0;
    }

    const normalized = input.toString().trim().toLowerCase();
    if (!normalized) {
        return 0;
    }

    // Remove all non-numeric characters except the last character (for 'k', 'm')
    let numericText = normalized.replace(/[^0-9]/g, '');
    let amount = Number(numericText) || 0;

    if (amount === 0) {
        return 0;
    }

    // Check last character for multiplier
    const lastChar = normalized.slice(-1);
    if (lastChar === 'm') {
        amount *= 1000000;
    } else if (lastChar === 'k') {
        amount *= 1000;
    }

    return amount;
}

function setAmountValue(amount) {
    if (!amount || amount <= 0) {
        amountInput.value = '';
        amountDisplay.textContent = '';
        return;
    }

    // Set input value as pure number (no formatting)
    amountInput.value = amount;
    // Display formatted value separately
    amountDisplay.textContent = `Số tiền đã chọn: ${formatCurrency(amount)}`;
}

function showResult(result) {
    if (!result) {
        showAlert('Không nhận được kết quả từ máy chủ.', 'danger');
        return;
    }

    // Check required fields
    if (!result.paymentCode || !result.qrPayload) {
        showAlert('Dữ liệu trả về từ máy chủ không hợp lệ.', 'danger');
        console.error('Invalid response:', result);
        return;
    }

    // Update payment details
    paymentCodeLabel.textContent = result.paymentCode;
    paymentAmountLabel.textContent = `₫ ${formatCurrency(result.amount)}`;
    paymentStatusLabel.innerHTML = '<span class="badge bg-warning text-dark">Đang chờ kiểm tra</span>';
    
    // Show loading indicator
    qrCodeImage.innerHTML = '<div class="text-center"><i class="fas fa-spinner fa-spin"></i> Đang tạo mã QR...</div>';
    
    // Generate QR code
    QRCode.toDataURL(result.qrPayload, {
        width: 300,
        margin: 1,
        color: {
            dark: '#000000',
            light: '#FFFFFF'
        }
    }).then(qrDataUrl => {
        qrCodeImage.innerHTML = `<img src="${qrDataUrl}" alt="QR Code thanh toán MB Bank" class="img-fluid border rounded" style="max-width: 300px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);" />`;
        // Show result section after QR is generated
        resultSection.classList.remove('d-none');
        emptyResult.classList.add('d-none');
        // Smooth scroll to result
        setTimeout(() => {
            resultSection.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }, 100);
    }).catch(err => {
        console.error('QR generation error:', err);
        qrCodeImage.innerHTML = '<div class="alert alert-danger"><i class="fas fa-exclamation-triangle me-2"></i>Không thể tạo mã QR. Vui lòng thử lại.</div>';
        resultSection.classList.remove('d-none');
        emptyResult.classList.add('d-none');
    });
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
