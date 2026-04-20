import { fetchApi } from '/services/apiClient.js';

const withAuthHeaders = (token) => ({
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`
});

export const getApplicationsForEmployee = async (token, filters = {}) => {
    const query = new URLSearchParams();
    if (filters.status) {
        query.set('status', filters.status);
    }
    if (filters.from) {
        query.set('from', filters.from);
    }
    if (filters.to) {
        query.set('to', filters.to);
    }

    const response = await fetchApi(`/Staff/applications${query.toString() ? `?${query.toString()}` : ''}`, {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await response.json().catch(() => []);
    if (!response.ok) {
        throw new Error(result.message || 'Không thể tải danh sách hồ sơ.');
    }

    return result;
};

export const processApplication = async (token, applicationId, action, note = '') => {
    const response = await fetchApi(`/Staff/process-record/${applicationId}`, {
        method: 'PUT',
        headers: withAuthHeaders(token),
        body: JSON.stringify({ action, note })
    });

    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
        throw new Error(result.message || 'Không thể xử lý hồ sơ.');
    }

    return result;
};
