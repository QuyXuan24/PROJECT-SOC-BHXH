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

export const createPaymentRequest = async (token, payload) => {
    const response = await fetchApi('/Payment/request', {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    return ensureSuccess(response, 'Khong the tao yeu cau thanh toan.');
};

export const getMyPayments = async (token) => {
    const response = await fetchApi('/Payment/my', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    return ensureSuccess(response, 'Khong the tai thong tin thanh toan.');
};

export const getPaymentRequestsForStaff = async (token, filters = {}) => {
    const query = new URLSearchParams();
    if (filters.status) {
        query.set('status', filters.status);
    }

    const response = await fetchApi(`/Payment/requests${query.toString() ? `?${query.toString()}` : ''}`, {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    return ensureSuccess(response, 'Khong the tai danh sach yeu cau thanh toan.');
};

export const reviewPaymentRequest = async (token, paymentId, action, note = '') => {
    const response = await fetchApi(`/Payment/requests/${paymentId}/review`, {
        method: 'PUT',
        headers: withAuthHeaders(token),
        body: JSON.stringify({ action, note })
    });

    return ensureSuccess(response, 'Khong the cap nhat trang thai thanh toan.');
};
