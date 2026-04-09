import { loginUser } from '/services/authApi.js';
import { setToken } from '/services/tokenService.js';

const loginForm = document.getElementById('loginForm');

if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const userField = document.getElementById('username');
        const passField = document.getElementById('password');
        const errorBox = document.getElementById('errorMessage');
        const btn = e.target.querySelector('button[type="submit"]');

        const originalBtnText = btn.innerHTML;

        errorBox.classList.add('d-none');
        errorBox.textContent = '';

        btn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang xác thực...';
        btn.disabled = true;

        try {
            const result = await loginUser(userField.value.trim(), passField.value);
            if (!result?.success || !result?.token) {
                throw new Error(result?.message || 'Đăng nhập thất bại.');
            }

            setToken(result.token);
            window.location.href = result.redirectPath || '/security/dashboard';
        } catch (error) {
            errorBox.textContent = error?.message || 'Loi ket noi den backend.';
            errorBox.classList.remove('d-none');
            btn.innerHTML = originalBtnText;
            btn.disabled = false;
        }
    });
}
