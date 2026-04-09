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

const ensureSuccess = async (response, fallbackMessage) => {
    const result = await parseResponseBody(response);
    if (!response.ok) {
        const error = new Error(result?.message || fallbackMessage);
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
    return ensureSuccess(response, 'Không thể tạo yêu cầu thanh toán.');
};

export const getMyPayments = async (token) => {
    const response = await fetchApi('/Payment/my', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });
    return ensureSuccess(response, 'Không thể tải thông tin thanh toán.');
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
    return ensureSuccess(response, 'Không thể tải danh sách yêu cầu thanh toán.');
};

export const reviewPaymentRequest = async (token, paymentId, action, note = '') => {
    const response = await fetchApi(`/Payment/requests/${paymentId}/review`, {
        method: 'PUT',
        headers: withAuthHeaders(token),
        body: JSON.stringify({ action, note })
    });
    return ensureSuccess(response, 'Không thể cập nhật trạng thái thanh toán.');
};
