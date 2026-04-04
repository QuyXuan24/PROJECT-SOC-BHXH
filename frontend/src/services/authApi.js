// Địa chỉ gốc của Controller Auth trên Backend .NET
import { fetchApi, getApiBaseUrl } from '/services/apiClient.js';

const AUTH_PATH = '/Auth';

/**
 * Hàm Đăng nhập: Gửi yêu cầu xác thực tới hệ thống SOC
 * @param {string} username 
 * @param {string} password 
 */
export const loginUser = async (username, password) => {
    try {
        const response = await fetchApi(`${AUTH_PATH}/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            // Gửi dữ liệu khớp với LoginDto ở Backend
            body: JSON.stringify({ username, password })
        });

        // Nếu đăng nhập thành công (200 OK)
        if (response.ok) {
            const result = await response.json();
            return { success: true, ...result };
        }

        // Nếu lỗi (401 Unauthorized, 403 Forbidden, v.v.)
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.message || "Xác thực thất bại.");

    } catch (error) {
        console.error("SOC Auth Error:", error.message);
        // Ném lỗi ra để file auth.js hiển thị lên giao diện
        throw error;
    }
};

/**
 * Hàm Đăng ký: Mã hóa và gửi hồ sơ người dân lên Blockchain qua Backend
 * @param {Object} userData - Chứa { fullName, username, password }
 */
export const registerUser = async (userData) => {
    try {
        const response = await fetchApi(`${AUTH_PATH}/register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            // Gửi dữ liệu khớp với RegisterDto ở Backend
            body: JSON.stringify(userData)
        });

        const result = await response.json();

        if (response.ok) {
            // Backend trả về Ok(new { message = "..." }) nên ta thêm success: true
            return { success: true, ...result };
        }

        return { success: false, message: result.message || "Không thể đăng ký hồ sơ." };

    } catch (error) {
        console.error("SOC Register Error:", error);
        return {
            success: false,
            message: `Loi ket noi toi may chu SOC (${getApiBaseUrl()}).`
        };
    }
};

export const loginCitizen = loginUser;
export const registerCitizen = registerUser;
