import { fetchApi, getApiBaseUrl } from '/services/apiClient.js';

const AUTH_PATH = '/Auth';

const toJsonOrEmpty = async (response) => response.json().catch(() => ({}));

export const loginUser = async (identifier, password) => {
    const response = await fetchApi(`${AUTH_PATH}/login`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Accept: 'application/json'
        },
        body: JSON.stringify({ username: identifier, password })
    });

    const result = await toJsonOrEmpty(response);
    if (!response.ok) {
        throw new Error(result.message || 'Đăng nhập thất bại.');
    }

    return { success: true, ...result };
};

export const verifyLoginOtp = async (email, otp) => {
    const response = await fetchApi(`${AUTH_PATH}/verify-login-otp`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Accept: 'application/json'
        },
        body: JSON.stringify({ email, otp })
    });

    const result = await toJsonOrEmpty(response);
    if (!response.ok) {
        throw new Error(result.message || 'Xác thực OTP đăng nhập thất bại.');
    }

    return { success: true, ...result };
};

export const registerUser = async (userData) => {
    try {
        const response = await fetchApi(`${AUTH_PATH}/register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                Accept: 'application/json'
            },
            body: JSON.stringify(userData)
        });

        const result = await toJsonOrEmpty(response);
        if (!response.ok) {
            return { success: false, message: result.message || 'Không thể đăng ký tài khoản.' };
        }

        return { success: true, ...result };
    } catch (error) {
        return {
            success: false,
            message: `Lỗi kết nối tới máy chủ SOC (${getApiBaseUrl()}).`
        };
    }
};

export const verifyRegisterOtp = async (email, otp) => {
    const response = await fetchApi(`${AUTH_PATH}/verify-register-otp`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Accept: 'application/json'
        },
        body: JSON.stringify({ email, otp })
    });

    const result = await toJsonOrEmpty(response);
    if (!response.ok) {
        throw new Error(result.message || 'Xác thực OTP đăng ký thất bại.');
    }

    return { success: true, ...result };
};

export const forgotPassword = async (email) => {
    const response = await fetchApi(`${AUTH_PATH}/forgot-password`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Accept: 'application/json'
        },
        body: JSON.stringify({ email })
    });

    const result = await toJsonOrEmpty(response);
    if (!response.ok) {
        throw new Error(result.message || 'Không thể gửi OTP quên mật khẩu.');
    }

    return { success: true, ...result };
};

export const resetPassword = async (email, otp, newPassword) => {
    const response = await fetchApi(`${AUTH_PATH}/reset-password`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Accept: 'application/json'
        },
        body: JSON.stringify({ email, otp, newPassword })
    });

    const result = await toJsonOrEmpty(response);
    if (!response.ok) {
        throw new Error(result.message || 'Đặt lại mật khẩu thất bại.');
    }

    return { success: true, ...result };
};

export const loginCitizen = loginUser;
export const registerCitizen = registerUser;
