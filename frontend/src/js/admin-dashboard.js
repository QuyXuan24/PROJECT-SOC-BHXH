import { guardPage, logout } from '/js/auth-guard.js';
import {
    createUserByAdmin,
    getUsers,
    updateUserByAdmin,
    toggleUserLock
} from '/services/adminApi.js';
import { getToken } from '/services/tokenService.js';

if (!guardPage(['Admin'], '/pages/login.html')) {
    throw new Error('Unauthorized');
}

const token = getToken();
const tableBody = document.getElementById('adminUserTableBody');
const resultBox = document.getElementById('createUserResult');
const createUserForm = document.getElementById('createUserForm');
const supportedRoles = ['User', 'Employee', 'Security', 'Admin'];

document.getElementById('btnLogout')?.addEventListener('click', () => logout('/pages/login.html'));
document.getElementById('btnReloadUsers')?.addEventListener('click', () => loadUsers());
tableBody?.addEventListener('click', onTableAction);

createUserForm?.addEventListener('submit', async (event) => {
    event.preventDefault();
    hideResult();

    const formData = new FormData(createUserForm);
    const payload = {
        username: (formData.get('username') || '').toString().trim(),
        fullName: (formData.get('fullName') || '').toString().trim(),
        role: (formData.get('role') || '').toString(),
        email: (formData.get('email') || '').toString().trim(),
        phoneNumber: (formData.get('phoneNumber') || '').toString().trim(),
        password: (formData.get('password') || '').toString().trim() || null
    };

    try {
        const result = await createUserByAdmin(token, payload);
        const passwordText = result.temporaryPassword
            ? ` Mat khau tam: ${result.temporaryPassword}`
            : '';
        showResult(`Tao tai khoan thanh cong.${passwordText}`, 'success');
        createUserForm.reset();
        await loadUsers();
    } catch (error) {
        showResult(error.message || 'Khong the tao tai khoan.', 'danger');
    }
});

loadUsers();

async function loadUsers() {
    tableBody.innerHTML = '<tr><td colspan="6" class="text-muted">Dang tai...</td></tr>';

    try {
        const users = await getUsers(token);
        if (!users || users.length === 0) {
            tableBody.innerHTML = '<tr><td colspan="6" class="text-muted">Khong co tai khoan.</td></tr>';
            return;
        }

        tableBody.innerHTML = '';
        users.forEach((user) => {
            const locked = isUserLocked(user);
            const statusText = getLockStatusText(user, locked);

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${escapeHtml(user.id)}</td>
                <td>${escapeHtml(user.username)}</td>
                <td>${escapeHtml(user.fullName || '-')}</td>
                <td><span class="badge bg-primary">${escapeHtml(user.role || '-')}</span></td>
                <td>${locked ? `<span class="badge bg-danger">${escapeHtml(statusText)}</span>` : '<span class="badge bg-success">Hoạt động</span>'}</td>
                <td>
                    <button
                        class="btn btn-sm btn-outline-secondary me-2"
                        data-action="edit"
                        data-id="${escapeAttr(user.id)}"
                        data-full-name="${escapeAttr(user.fullName || '')}"
                        data-role="${escapeAttr(user.role || 'User')}"
                        data-email="${escapeAttr(user.email || '')}"
                        data-phone-number="${escapeAttr(user.phoneNumber || '')}">
                        Sửa
                    </button>
                    <button
                        class="btn btn-sm ${locked ? 'btn-outline-success' : 'btn-outline-danger'}"
                        data-action="lock"
                        data-id="${escapeAttr(user.id)}"
                        data-locked="${escapeAttr(locked)}">
                        ${locked ? 'Mở khóa' : 'Khóa'}
                    </button>
                </td>
            `;

            tableBody.appendChild(row);
        });
    } catch (error) {
        tableBody.innerHTML = `<tr><td colspan="6" class="text-danger">${escapeHtml(error.message || 'Khong the tai danh sach tai khoan.')}</td></tr>`;
    }
}

async function onTableAction(event) {
    const button = event.target.closest('button[data-action]');
    if (!button) {
        return;
    }

    const action = button.dataset.action;
    if (action === 'lock') {
        await toggleUserLockAction(button);
        return;
    }

    if (action === 'edit') {
        await editUserAction(button);
    }
}

async function toggleUserLockAction(button) {
    const userId = Number(button.dataset.id);
    if (!userId) {
        return;
    }

    const isLocked = button.dataset.locked === 'true';
    const reason = isLocked
        ? 'Admin mo khoa tai khoan.'
        : prompt('Nhap ly do khoa tai khoan (toi thieu 10 ky tu):', '');

    if (!isLocked && (!reason || reason.trim().length < 10)) {
        alert('Ly do khoa khong hop le.');
        return;
    }

    try {
        await toggleUserLock(token, userId, {
            reason: reason || 'Unlock account',
            durationMinutes: isLocked ? null : 30
        });

        await loadUsers();
    } catch (error) {
        alert(error.message || 'Khong the cap nhat trang thai khoa.');
    }
}

async function editUserAction(button) {
    const userId = Number(button.dataset.id);
    if (!userId) {
        return;
    }

    const fullNameInput = prompt('Ho va ten:', button.dataset.fullName || '');
    if (fullNameInput === null) {
        return;
    }

    const roleInput = prompt(`Role (${supportedRoles.join('/')}):`, button.dataset.role || 'User');
    if (roleInput === null) {
        return;
    }

    const emailInput = prompt('Email:', button.dataset.email || '');
    if (emailInput === null) {
        return;
    }

    const phoneInput = prompt('So dien thoai:', button.dataset.phoneNumber || '');
    if (phoneInput === null) {
        return;
    }

    const passwordInput = prompt('Mat khau moi (bo trong neu khong doi):', '');
    if (passwordInput === null) {
        return;
    }

    const fullName = fullNameInput.trim();
    if (!fullName) {
        alert('Ho va ten khong duoc de trong.');
        return;
    }

    const role = normalizeRole(roleInput);
    if (!role) {
        alert(`Role khong hop le. Chi chap nhan: ${supportedRoles.join(', ')}`);
        return;
    }

    const payload = {
        fullName,
        role,
        email: emailInput.trim(),
        phoneNumber: phoneInput.trim(),
        password: passwordInput.trim() || null
    };

    try {
        await updateUserByAdmin(token, userId, payload);
        showResult('Cap nhat tai khoan thanh cong.', 'success');
        await loadUsers();
    } catch (error) {
        alert(error.message || 'Khong the cap nhat tai khoan.');
    }
}

function normalizeRole(roleValue) {
    const value = (roleValue || '').toString().trim().toLowerCase();

    if (value === 'admin') return 'Admin';
    if (value === 'employee') return 'Employee';
    if (value === 'security') return 'Security';
    if (value === 'user') return 'User';

    return null;
}

function isUserLocked(user) {
    if (user?.isLocked === true) {
        return true;
    }

    const lockoutRaw = user?.lockoutEnd || user?.LockoutEnd;
    if (!lockoutRaw) {
        return false;
    }

    const lockoutTime = Date.parse(lockoutRaw);
    if (Number.isNaN(lockoutTime)) {
        return false;
    }

    return lockoutTime > Date.now();
}

function getLockStatusText(user, locked) {
    if (!locked) {
        return 'Hoạt động';
    }

    if (user?.isLocked === true) {
        return 'Bị khóa';
    }

    return 'Bị khóa (tạm)';
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function escapeAttr(value) {
    return escapeHtml(value).replace(/`/g, '&#96;');
}

function showResult(message, type) {
    resultBox.textContent = message;
    resultBox.className = `alert alert-${type} mt-3`;
    resultBox.classList.remove('d-none');
}

function hideResult() {
    resultBox.classList.add('d-none');
}
