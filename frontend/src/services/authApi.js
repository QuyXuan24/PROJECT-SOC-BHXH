import { fetchApi, getApiBaseUrl } from '/services/apiClient.js';

const AUTH_PATH = '/Auth';

export const loginUser = async (identifier, password) => {
    try {
        const response = await fetchApi(`${AUTH_PATH}/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                Accept: 'application/json'
            },
            body: JSON.stringify({ username: identifier, password })
        });

        const result = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(result.message || 'Xác thực thất bại , vui lòng kiểm tra lại thông tin đăng nhập.');
        }

        return { success: true, ...result };
    } catch (error) {
        console.error('SOC Auth Error:', error.message);
        throw error;
    }
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

        const result = await response.json().catch(() => ({}));
        if (!response.ok) {
            return { success: false, message: result.message || 'Không thể đăng ký tài khoản.' };
        }

        return { success: true, ...result };
    } catch (error) {
        console.error('SOC Register Error:', error);
        return {
            success: false,
            message: `Lỗi kết nối tới máy chủ SOC (${getApiBaseUrl()}).`
        };
    }
};

export const loginCitizen = loginUser;
export const registerCitizen = registerUser;
