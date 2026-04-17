import {
    forgotPassword,
    loginCitizen,
    registerCitizen,
    resetPassword,
    verifyLoginOtp,
    verifyRegisterOtp
} from '/services/authApi.js';
import { getCurrentRole, setToken } from '/services/tokenService.js';

const loginForm = document.getElementById('citizenLoginForm');
const registerForm = document.getElementById('citizenRegisterForm');
const forgotPasswordLink = document.getElementById('forgotPasswordLink');

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
            const loginResult = await loginCitizen(username, password);
            if (!loginResult?.requiresOtp || !loginResult?.email) {
                throw new Error(loginResult?.message || 'Không thể khởi tạo phiên OTP đăng nhập.');
            }

            const otp = window.prompt(`Nhập OTP đã gửi về email ${loginResult.maskedEmail || loginResult.email}:`, '');
            if (!otp) {
                throw new Error('Bạn cần nhập OTP để hoàn tất đăng nhập.');
            }

            const verifyResult = await verifyLoginOtp(loginResult.email, otp.trim());
            if (!verifyResult?.token) {
                throw new Error('Xác thực OTP thành công nhưng không nhận được token.');
            }

            setToken(verifyResult.token);
            redirectByRole(verifyResult.role || getCurrentRole(), verifyResult.redirectPath);
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

if (registerForm) {
    registerForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const btn = e.target.querySelector('button[type="submit"]');
        const originalText = btn.innerHTML;
        btn.innerHTML = '<i class="fas fa-sync fa-spin me-2"></i>Đang gửi OTP...';
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
            const registerResult = await registerCitizen(userData);
            if (!registerResult.success) {
                throw new Error(registerResult.message || 'Đăng ký thất bại.');
            }

            const otp = window.prompt(`Nhập OTP đăng ký đã gửi về email ${userData.email}:`, '');
            if (!otp) {
                throw new Error('Bạn cần nhập OTP để hoàn tất đăng ký.');
            }

            await verifyRegisterOtp(userData.email, otp.trim());
            alert('Đăng ký thành công. Bạn có thể đăng nhập ngay.');
            e.target.reset();
            document.getElementById('login-tab')?.click();
        } catch (error) {
            alert(error?.message || 'Đăng ký thất bại.');
        } finally {
            btn.innerHTML = originalText;
            btn.disabled = false;
        }
    });
}

forgotPasswordLink?.addEventListener('click', async (event) => {
    event.preventDefault();

    const email = window.prompt('Nhập email tài khoản để nhận OTP đặt lại mật khẩu:', '');
    if (!email) {
        return;
    }

    try {
        const forgotResult = await forgotPassword(email.trim());
        alert(forgotResult.message || 'OTP đặt lại mật khẩu đã được gửi.');

        const otp = window.prompt('Nhập OTP 6 chữ số:', '');
        if (!otp) {
            return;
        }

        const newPassword = window.prompt('Nhập mật khẩu mới:', '');
        if (!newPassword) {
            return;
        }

        const resetResult = await resetPassword(email.trim(), otp.trim(), newPassword);
        alert(resetResult.message || 'Đặt lại mật khẩu thành công.');
    } catch (error) {
        alert(error?.message || 'Không thể xử lý quên mật khẩu.');
    }
});

function redirectByRole(role, redirectPath) {
    if (role === 'User') {
        window.location.href = '/pages/user-dashboard.html';
        return;
    }

    if (role === 'Employee') {
        window.location.href = '/pages/employee-dashboard.html';
        return;
    }

    if (role === 'Security') {
        window.location.href = '/pages/security-dashboard.html';
        return;
    }

    if (role === 'Admin') {
        window.location.href = '/pages/admin-dashboard.html';
        return;
    }

    window.location.href = redirectPath || '/pages/user-dashboard.html';
}
