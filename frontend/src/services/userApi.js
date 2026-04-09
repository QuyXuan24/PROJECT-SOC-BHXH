import { fetchApi } from '/services/apiClient.js';

const withAuthHeaders = (token) => ({
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`
});

const parseResponseBody = async (response) => {
    const contentType = response.headers.get('content-type') || '';

    if (contentType.includes('application/json')) {
        return response.json().catch(() => null);
    }

    const text = await response.text().catch(() => '');
    if (!text) {
        return null;
    }

    try {
        return JSON.parse(text);
    } catch {
        return text;
    }
};

const extractMessage = (payload, fallback) => {
    if (!payload) {
        return fallback;
    }

    if (typeof payload === 'string') {
        return payload || fallback;
    }

    return payload.message || payload.title || fallback;
};

const ensureSuccess = async (response, fallbackMessage) => {
    const result = await parseResponseBody(response);
    if (!response.ok) {
        const error = new Error(extractMessage(result, fallbackMessage));
        error.status = response.status;
        error.payload = result;
        throw error;
    }

    return result;
};

export const submitInsuranceProfile = async (token, payload) => {
    const response = await fetchApi('/User/profile', {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    return ensureSuccess(response, 'Không thể nộp hồ sơ.');
};

export const getMyProfile = async (token) => {
    const response = await fetchApi('/User/profile', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    return ensureSuccess(response, 'Không thể lấy hồ sơ.');
};

export const getMyApplications = async (token) => {
    const response = await fetchApi('/User/applications', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await ensureSuccess(response, 'Không thể lấy danh sách hồ sơ.');
    return Array.isArray(result) ? result : [];
};

export const getApplicationTimeline = async (token, id) => {
    const response = await fetchApi(`/User/applications/${id}/timeline`, {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await ensureSuccess(response, 'Không thể lấy timeline.');
    return Array.isArray(result) ? result : [];
};

export const cancelApplication = async (token, id, reason) => {
    const response = await fetchApi(`/User/applications/${id}/cancel`, {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify({ reason })
    });

    return ensureSuccess(response, 'Không thể hủy hồ sơ.');
};
