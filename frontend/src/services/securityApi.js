import { fetchApi } from '/services/apiClient.js';

const withAuthHeaders = (token) => ({
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`
});

const parseBody = async (response) => {
    const type = response.headers.get('content-type') || '';
    if (type.includes('application/json')) {
        return response.json().catch(() => null);
    }

    const text = await response.text().catch(() => '');
    return text || null;
};

const ensureSuccess = async (response, fallbackMessage) => {
    const body = await parseBody(response);
    if (!response.ok) {
        const message = typeof body === 'string'
            ? body
            : body?.message || body?.title || fallbackMessage;
        const error = new Error(message || fallbackMessage);
        error.status = response.status;
        error.payload = body;
        throw error;
    }

    return body;
};

export const getSecurityOverview = async (token) => {
    const response = await fetchApi('/security/overview', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    return ensureSuccess(response, 'Không thể tải tổng quan SOC.');
};

export const getRealtimeAlerts = async (token, minutes = 60) => {
    const response = await fetchApi(`/security/alerts/realtime?minutes=${encodeURIComponent(minutes)}`, {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await ensureSuccess(response, 'Không thể tải cảnh báo realtime.');
    return Array.isArray(result) ? result : [];
};

export const blockIp = async (token, payload) => {
    const response = await fetchApi('/security/block-ip', {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    return ensureSuccess(response, 'Không thể block IP.');
};

export const lockAccount = async (token, payload) => {
    const response = await fetchApi('/security/lock-account', {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    return ensureSuccess(response, 'Không thể khóa tài khoản.');
};

export const createIncident = async (token, payload) => {
    const response = await fetchApi('/incidents', {
        method: 'POST',
        headers: withAuthHeaders(token),
        body: JSON.stringify(payload)
    });

    return ensureSuccess(response, 'Không thể tạo incident.');
};

export const getIncidents = async (token, { status = '', page = 1, pageSize = 50 } = {}) => {
    const query = new URLSearchParams();
    query.set('page', String(page));
    query.set('pageSize', String(pageSize));
    if (status) {
        query.set('status', status);
    }

    const response = await fetchApi(`/incidents?${query.toString()}`, {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    return ensureSuccess(response, 'Không thể tải danh sách incident.');
};
