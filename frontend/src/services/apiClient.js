// =================================================================
// Tối ưu và đơn giản hóa API Client
// - Loại bỏ logic phức tạp, tự động dò tìm base URL.
// - Sử dụng một base URL duy nhất, đáng tin cậy, dễ cấu hình.
// - Khi chạy với Docker, backend luôn được expose qua port 5000.
// =================================================================

const API_BASE_URL = "http://localhost:5000/api";

/**
 * Hàm fetch API đã được chuẩn hóa.
 * @param {string} path - Đường dẫn endpoint (ví dụ: '/auth/login')
 * @param {object} options - Tùy chọn cho hàm fetch (method, body, headers...)
 * @returns {Promise<Response>} - Đối tượng Response gốc từ fetch
 */
export const fetchApi = async (path, options = {}) => {
    const safePath = path.startsWith("/") ? path : `/${path}`;
    const url = `${API_BASE_URL}${safePath}`;

    const defaultHeaders = {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
    };

    const config = {
        ...options,
        headers: {
            ...defaultHeaders,
            ...options.headers,
        },
    };

    try {
        const response = await fetch(url, config);
        return response;
    } catch (error) {
        // Lỗi mạng (ví dụ: ERR_CONNECTION_REFUSED) sẽ được bắt ở đây.
        console.error(`Network error when calling ${url}:`, error);
        throw new Error("Không thể kết nối đến máy chủ API. Vui lòng kiểm tra xem backend đã chạy chưa.");
    }
};

/**
 * Hàm tiện ích để lấy base URL, hữu ích cho các trường hợp cần URL đầy đủ (ví dụ: src của ảnh).
 * @returns {string}
 */
export const getApiBaseUrl = () => {
    return API_BASE_URL;
};
