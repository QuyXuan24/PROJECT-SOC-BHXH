import { lookupBhxh, lookupBhyT } from '/services/lookupApi.js';
import { formatDateOnly, formatBhxhCode, formatCurrency } from '/js/formatters.js';

const bhxhQueryInput = document.getElementById('bhxhQueryInput');
const bhxhCccdInput = document.getElementById('bhxhCccdInput');
const bhxhOtpInput = document.getElementById('bhxhOtpInput');
const bhxhSearchBtn = document.getElementById('bhxhSearchBtn');
const bhxhResultsContainer = document.getElementById('bhxhResults');

const bhytCardInput = document.getElementById('bhytCardInput');
const bhytNameInput = document.getElementById('bhytNameInput');
const bhytDobInput = document.getElementById('bhytDobInput');
const bhytSearchBtn = document.getElementById('bhytSearchBtn');
const bhytResultsContainer = document.getElementById('bhytResults');

const showMessage = (container, message, type = 'info') => {
    if (!container) return;
    container.innerHTML = `
        <div class="alert alert-${type} py-3" role="alert">
            ${message}
        </div>
    `;
};

const safe = (value, fallback = '-') => {
    if (value === null || value === undefined || value === '') return fallback;
    return value;
};

const renderBhxhResult = (record) => {
    // SỬA LỖI: Chuyển từ PascalCase (VD: record.Status) sang camelCase (VD: record.status) để khớp với JSON từ backend
    const status = safe(record.status, 'Chưa xác định');
    const badgeClass = status === 'Approved' ? 'success' : status === 'Rejected' ? 'danger' : 'warning';
    const cccdVerified = Boolean(record.cccdVerified);
    const totalMonths = Number(record.totalMonths || 0);
    const totalDuration = safe(record.totalDuration, totalMonths > 0 ? `${totalMonths} tháng` : 'Chưa xác định');

    const timelineRows = record.timeline?.length
        ? record.timeline.map((item) => `
            <tr>
                <td>${safe(item.period)}</td>
                <td>${safe(item.companyName, 'Chưa cập nhật')}</td>
                <td>${formatCurrency(item.salary)}</td>
                <td>${safe(item.contributionType, 'Bắt buộc')}</td>
                <td>${safe(item.months, 0)}</td>
            </tr>
        `).join('')
        : `<tr><td colspan="5" class="text-center text-muted">Không có dữ liệu lịch sử đóng BHXH chi tiết.</td></tr>`;

    return `
        <div class="card shadow-sm lookup-card mb-4">
            <div class="card-body">
                <div class="d-flex flex-wrap justify-content-between align-items-start">
                    <div>
                        <h5 class="card-title text-primary mb-2">${safe(record.fullName, 'Chưa cập nhật')}</h5>
                        <div class="lookup-key mb-2">Mã số BHXH: <strong>${formatBhxhCode(safe(record.bhxhCode, ''))}</strong></div>
                        <div class="lookup-key mb-2">CCCD: <strong>${safe(record.cccd)}</strong></div>
                        <div class="lookup-key">Ngày sinh: <strong>${formatDateOnly(record.dateOfBirth)}</strong></div>
                    </div>
                    <div class="text-end">
                        <span class="badge bg-${badgeClass} mb-2">${status}</span>
                        <div>${cccdVerified ? '<span class="badge bg-success">CCCD khớp</span>' : '<span class="badge bg-secondary">CCCD chưa xác thực</span>'}</div>
                    </div>
                </div>

                <div class="row mt-4 gy-2">
                    <div class="col-md-4"><strong>Công ty:</strong> ${safe(record.companyName, 'Chưa cập nhật')}</div>
                    <div class="col-md-4"><strong>Lương đóng:</strong> ${formatCurrency(record.salary)}</div>
                    <div class="col-md-4"><strong>Tổng thời gian:</strong> ${totalDuration}</div>
                </div>

                <div class="row mt-3 gy-2">
                    <div class="col-md-4"><strong>Ngày nộp:</strong> ${formatDateOnly(record.submittedAt)}</div>
                    <div class="col-md-4"><strong>Cập nhật cuối:</strong> ${record.lastUpdatedAt ? formatDateOnly(record.lastUpdatedAt) : 'Chưa có'}</div>
                    <div class="col-md-4"><strong>OTP:</strong> ${record.otpProvided ? 'Đã nhập' : 'Không nhập'}</div>
                </div>

                ${record.otpNotice ? `<div class="alert alert-info mt-3 mb-0">${record.otpNotice}</div>` : ''}
            </div>
        </div>

        <div class="card shadow-sm timeline-card mb-3">
            <div class="card-body">
                <div class="d-flex justify-content-between align-items-center mb-3">
                    <h6 class="mb-0">Timeline đóng BHXH</h6>
                    <span class="badge bg-primary">${totalMonths} tháng</span>
                </div>
                <div class="table-responsive">
                    <table class="table table-hover align-middle">
                        <thead>
                            <tr>
                                <th>Thời gian</th>
                                <th>Công ty</th>
                                <th>Lương đóng</th>
                                <th>Loại hình</th>
                                <th>Tháng</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${timelineRows}
                        </tbody>
                    </table>
                </div>
                <div class="alert alert-secondary mb-0">${safe(record.notes, 'Không có ghi chú.')}</div>
            </div>
        </div>
    `;
};

const renderBhytResult = (data) => {
    const status = safe(data.status, 'Chưa có dữ liệu BHYT');
    const badgeType = status === 'Còn hạn' ? 'success' : status === 'Hết hạn' ? 'danger' : 'secondary';

    return `
        <div class="card shadow-sm lookup-card bhyt mb-4">
            <div class="card-body">
                <div class="d-flex justify-content-between align-items-start flex-wrap gap-3">
                    <div>
                        <h5 class="card-title text-success mb-2"><i class="fas fa-id-card me-2"></i>Thẻ BHYT</h5>
                        <div class="lookup-key mb-2">Mã thẻ: <strong>${safe(data.cardNumber)}</strong></div>
                        <div class="lookup-key">Họ tên: <strong>${safe(data.fullName, 'Chưa cung cấp')}</strong></div>
                    </div>
                    <span class="badge bg-${badgeType} py-2 px-3">${status}</span>
                </div>

                <div class="row mt-4 gy-2">
                    <div class="col-md-6"><strong>Hiệu lực:</strong> ${formatDateOnly(data.validFrom)} - ${formatDateOnly(data.validTo)}</div>
                    <div class="col-md-6"><strong>KCB ban đầu:</strong> ${safe(data.registeredHospital, 'Chưa có dữ liệu')}</div>
                    <div class="col-md-6"><strong>Quyền lợi:</strong> ${safe(data.benefitRate, 'Chưa có dữ liệu')}</div>
                    <div class="col-md-6"><strong>Ngày sinh:</strong> ${formatDateOnly(data.dateOfBirth)}</div>
                </div>

                <div class="alert alert-info mt-3 mb-0">
                    ${safe(data.message, 'Tra cứu BHYT chưa có dữ liệu chi tiết backend.')}
                </div>
            </div>
        </div>
    `;
};

const onBhxhSearch = async () => {
    if (!bhxhQueryInput || !bhxhResultsContainer) return;

    const query = bhxhQueryInput.value?.trim();
    const cccd = bhxhCccdInput?.value?.trim() || '';
    const otp = bhxhOtpInput?.value?.trim() || '';

    if (!query) {
        showMessage(bhxhResultsContainer, 'Vui lòng nhập mã số BHXH để tra cứu.', 'warning');
        return;
    }

    bhxhSearchBtn.disabled = true;
    bhxhSearchBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang tra cứu...';
    bhxhResultsContainer.innerHTML = '';

    try {
        const result = await lookupBhxh({ query, cccd, otp });
        if (!result.ok) {
            const errorMessage = result.data?.message || 'Không thể kết nối tới máy chủ tra cứu.';
            showMessage(bhxhResultsContainer, errorMessage, 'danger');
        } else if (!result.data?.results?.length) {
            showMessage(bhxhResultsContainer, result.data?.message || 'Không tìm thấy hồ sơ BHXH.', 'warning');
        } else {
            bhxhResultsContainer.innerHTML = renderBhxhResult(result.data.results[0]);
        }
    } catch {
        showMessage(bhxhResultsContainer, 'Lỗi khi tra cứu. Vui lòng thử lại.', 'danger');
    } finally {
        bhxhSearchBtn.disabled = false;
        bhxhSearchBtn.innerHTML = 'TRA CỨU';
    }
};

const onBhytSearch = async () => {
    if (!bhytCardInput || !bhytResultsContainer) return;

    const cardNumber = bhytCardInput.value?.trim();
    const fullName = bhytNameInput.value?.trim();
    const dateOfBirth = bhytDobInput.value || '';

    if (!cardNumber) {
        showMessage(bhytResultsContainer, 'Vui lòng nhập mã thẻ BHYT để tra cứu.', 'warning');
        return;
    }

    bhytSearchBtn.disabled = true;
    bhytSearchBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang tra cứu...';
    bhytResultsContainer.innerHTML = '';

    try {
        const result = await lookupBhyT({ cardNumber, fullName, dateOfBirth });
        if (!result.ok) {
            const errorMessage = result.data?.message || 'Không thể kết nối tới máy chủ tra cứu BHYT.';
            showMessage(bhytResultsContainer, errorMessage, 'danger');
        } else {
            bhytResultsContainer.innerHTML = renderBhytResult(result.data || {});
        }
    } catch {
        showMessage(bhytResultsContainer, 'Lỗi khi tra cứu BHYT. Vui lòng thử lại.', 'danger');
    } finally {
        bhytSearchBtn.disabled = false;
        bhytSearchBtn.innerHTML = 'TRA CỨU';
    }
};

if (bhxhSearchBtn) {
    bhxhSearchBtn.addEventListener('click', onBhxhSearch);
}

if (bhytSearchBtn) {
    bhytSearchBtn.addEventListener('click', onBhytSearch);
}
