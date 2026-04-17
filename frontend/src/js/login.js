import { loginUser, verifyLoginOtp } from '/services/authApi.js';
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
            const loginResult = await loginUser(userField.value.trim(), passField.value);
            if (!loginResult?.requiresOtp || !loginResult?.email) {
                throw new Error(loginResult?.message || 'Không thể khởi tạo OTP đăng nhập.');
            }

            const otp = window.prompt(`Nhập OTP đã gửi về email ${loginResult.maskedEmail || loginResult.email}:`, '');
            if (!otp) {
                throw new Error('Bạn cần nhập OTP để đăng nhập.');
            }

            const verifyResult = await verifyLoginOtp(loginResult.email, otp.trim());
            if (!verifyResult?.token) {
                throw new Error('Không thể xác thực OTP đăng nhập.');
            }

            setToken(verifyResult.token);
            window.location.href = verifyResult.redirectPath || '/security/dashboard';
        } catch (error) {
            errorBox.textContent = error?.message || 'Lỗi kết nối đến backend.';
            errorBox.classList.remove('d-none');
            btn.innerHTML = originalBtnText;
            btn.disabled = false;
        }
    });
}
