import { guardPage, logout } from '/js/auth-guard.js';
import { getMyProfile, submitInsuranceProfile } from '/services/userApi.js';
import { getToken } from '/services/tokenService.js';

if (!guardPage(['User'])) {
    throw new Error('Unauthorized');
}

const form = document.getElementById('insuranceInputForm');
const resultBox = document.getElementById('resultBox');
const submitBtn = document.getElementById('btnSubmitData');
const logoutBtn = document.getElementById('btnLogout');
const btnBackDashboard = document.getElementById('btnBackDashboard');
const token = getToken();

logoutBtn?.addEventListener('click', () => logout('/pages/auth.html'));
btnBackDashboard?.addEventListener('click', () => {
    window.location.href = '/pages/user-dashboard.html';
});

form?.addEventListener('submit', onSubmitForm);

loadProfileIfAny();

async function loadProfileIfAny() {
    try {
        const profile = await getMyProfile(token);
        fillForm(profile);
    } catch (error) {
        if (error.status !== 404) {
            showResult(error.message || 'Không thể tải dữ liệu hồ sơ.', 'danger');
        }
    }
}

async function onSubmitForm(event) {
    event.preventDefault();

    const payload = getPayloadFromForm();

    if (!payload.bhxhCode || !/^[0-9]{10}$/.test(payload.bhxhCode)) {
        showResult('Mã số BHXH phải đúng 10 chữ số.', 'danger');
        return;
    }

    if (!payload.fullName || !payload.dateOfBirth || !payload.gender || !payload.cccd || !payload.phoneNumber || !payload.address || !payload.companyName) {
        showResult('Vui lòng điền đầy đủ thông tin.', 'danger');
        return;
    }
    if (payload.salary === null || Number.isNaN(payload.salary) || payload.salary < 0) {
        showResult('Lương phải là số hợp lệ (>= 0).', 'danger');
        return;
    }

    submitBtn.disabled = true;
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang nộp...';
    hideResult();

    try {
        const result = await submitInsuranceProfile(token, payload);
        const recordId = result?.recordId ? ` #${result.recordId}` : '';
        showResult(`Nop ho so thanh cong${recordId}.`, 'success');
    } catch (error) {
        showResult(error.message || 'Không thể nộp hồ sơ.', 'danger');
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = '<i class="fas fa-paper-plane me-2"></i>Nộp hồ sơ';
    }
}

function getPayloadFromForm() {
    const formData = new FormData(form);
    return {
        fullName: (formData.get('fullName') || '').toString().trim(),
        dateOfBirth: (formData.get('dateOfBirth') || '').toString(),
        gender: (formData.get('gender') || '').toString().trim(),
        cccd: (formData.get('cccd') || '').toString().trim(),
        phoneNumber: (formData.get('phoneNumber') || '').toString().trim(),
        address: (formData.get('address') || '').toString().trim(),
        bhxhCode: (formData.get('bhxhCode') || '').toString().trim(),
        companyName: (formData.get('companyName') || '').toString().trim(),
        salary: parseFloat((formData.get('salary') || '').toString())
    };
}

function fillForm(profile) {
    if (!form || !profile) {
        return;
    }

    form.fullName.value = profile.FullName || profile.fullName || '';
    form.dateOfBirth.value = toInputDate(profile.DateOfBirth || profile.dateOfBirth || '');
    form.gender.value = profile.Gender || profile.gender || '';
    form.cccd.value = profile.Cccd || profile.cccd || '';
    form.phoneNumber.value = profile.PhoneNumber || profile.phoneNumber || '';
    form.address.value = profile.Address || profile.address || '';
    form.bhxhCode.value = profile.BhxhCode || profile.bhxhCode || '';
    form.companyName.value = profile.CompanyName || profile.companyName || '';
    form.salary.value = profile.Salary || profile.salary || '';
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

function showResult(message, type) {
    resultBox.textContent = message;
    resultBox.className = `alert alert-${type} mt-3`;
    resultBox.classList.remove('d-none');
}

function hideResult() {
    resultBox.classList.add('d-none');
}
