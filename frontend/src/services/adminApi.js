import { fetchApi } from '/services/apiClient.js';

const withAuthHeaders = (token) => ({
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`
});

export const getUsers = async (token) => {
    const response = await fetchApi('/Admin/users', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await response.json().catch(() => []);
    if (!response.ok) {
        throw new Error(result.message || 'Không thể tải danh sách tài khoản');
    }

    return result;
};

export const createUserByAdmin = async (token, payload) => {
    const response = await fetchApi('/Admin/users', {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
        throw new Error(result.message || 'Không thể tạo tài khoản.');
    }

    return result;
};

export const updateUserByAdmin = async (token, userId, payload) => {
    const response = await fetchApi(`/Admin/users/${userId}`, {
        method: 'PUT',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
        throw new Error(result.message || 'Không thể cập nhật tài khoản.');
    }

    return result;
};

export const toggleUserLock = async (token, userId, payload) => {
    const response = await fetchApi(`/Admin/users/${userId}/lock`, {
        method: 'PUT',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
        throw new Error(result.message || 'Không thể khóa/mở khóa tài khoản.');
    }

    return result;
};
