import { loginCitizen, registerCitizen } from '/services/authApi.js';

const loginForm = document.getElementById('citizenLoginForm');
const registerForm = document.getElementById('citizenRegisterForm');

// --- XỬ LÝ ĐĂNG NHẬP NGƯỜI DÂN ---
if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const btn = e.target.querySelector('button[type="submit"]');
        const originalText = btn.innerHTML;
        const errorBox = document.getElementById('errorMessage');

        if (errorBox) {
            errorBox.textContent = '';
            errorBox.classList.add('d-none');
        }

        btn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang xác thực...';
        btn.disabled = true;

        const username = e.target.elements.username?.value?.trim() || '';
        const password = e.target.elements.password?.value || '';

        try {
            const result = await loginCitizen(username, password);
            if (!result?.success || !result?.token) {
                throw new Error(result?.message || 'Đăng nhập thất bại.');
            }

            localStorage.setItem('soc_token', result.token);
            window.location.href = '/pages/user-dashboard.html';
        } catch (error) {
            const message = error?.message || 'Không thể đăng nhập. Vui lòng thử lại.';
            if (errorBox) {
                errorBox.textContent = message;
                errorBox.classList.remove('d-none');
            } else {
                alert(message);
            }
            btn.innerHTML = originalText;
            btn.disabled = false;
        }
    });
}

// --- XỬ LÝ ĐĂNG KÝ NGƯỜI DÂN ---
if (registerForm) {
    registerForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const btn = e.target.querySelector('button[type="submit"]');
        const originalText = btn.innerHTML;
        btn.innerHTML = '<i class="fas fa-sync fa-spin me-2"></i>Đang gửi hồ sơ...';
        btn.disabled = true;

        const userData = {
            fullName: e.target.elements.fullName?.value?.trim() || '',
            username: e.target.elements.username?.value?.trim() || '',
            email: e.target.elements.email?.value?.trim() || '',
            phoneNumber: e.target.elements.phoneNumber?.value?.trim() || '',
            bhxhCode: e.target.elements.bhxhCode?.value?.trim() || '',
            password: e.target.elements.password?.value || ''
        };

        try {
            const result = await registerCitizen(userData);

            if (result.success) {
                alert('Đăng ký thành công! Bạn có thể đăng nhập ngay.');
                e.target.reset();
                document.getElementById('login-tab')?.click();
            } else {
                alert(`Đăng ký thất bại: ${result.message}`);
            }
        } finally {
            btn.innerHTML = originalText;
            btn.disabled = false;
        }
    });
}
