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

const looksLikeHtml = (value) => {
    if (typeof value !== 'string') {
        return false;
    }

    return /<\s*(!doctype|html|head|body)\b/i.test(value);
};

const extractMessage = (payload, fallback) => {
    if (!payload) {
        return fallback;
    }

    if (typeof payload === 'string') {
        return looksLikeHtml(payload)
            ? `${fallback} (API dang tra ve HTML thay vi JSON)`
            : (payload || fallback);
    }

    const message = payload.message || payload.title || fallback;
    return looksLikeHtml(message)
        ? `${fallback} (API dang tra ve HTML thay vi JSON)`
        : message;
};

const ensureSuccess = async (response, fallbackMessage) => {
    const result = await parseResponseBody(response);
    if (!response.ok) {
        const error = new Error(extractMessage(result, fallbackMessage));
        error.status = response.status;
        error.payload = result;
        throw error;
    }

    if (looksLikeHtml(result)) {
        const error = new Error(`${fallbackMessage} (API dang tra ve HTML thay vi JSON)`);
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

    return ensureSuccess(response, 'Khong the nop ho so.');
};

export const getMyProfile = async (token) => {
    const response = await fetchApi('/User/profile', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    return ensureSuccess(response, 'Khong the lay ho so.');
};

export const getMyApplications = async (token) => {
    const response = await fetchApi('/User/applications', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await ensureSuccess(response, 'Khong the lay danh sach ho so.');
    return Array.isArray(result) ? result : [];
};

export const getApplicationTimeline = async (token, id) => {
    const response = await fetchApi(`/User/applications/${id}/timeline`, {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await ensureSuccess(response, 'Khong the lay timeline.');
    return Array.isArray(result) ? result : [];
};

export const cancelApplication = async (token, id, reason) => {
    const response = await fetchApi(`/User/applications/${id}/cancel`, {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify({ reason })
    });

    return ensureSuccess(response, 'Khong the huy ho so.');
};
