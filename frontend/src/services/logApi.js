import { fetchApi } from '/services/apiClient.js';

const withAuthHeaders = (token) => ({
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`
});

const parseBody = async (response) => {
    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
        return response.json().catch(() => null);
    }

    const text = await response.text().catch(() => '');
    return text || null;
};

const ensureSuccess = async (response, fallbackMessage) => {
    const result = await parseBody(response);
    if (!response.ok) {
        const message = typeof result === 'string'
            ? result
            : result?.message || fallbackMessage;
        throw new Error(message || fallbackMessage);
    }

    return result;
};

export const getSystemLogs = async (token, options = {}) => {
    const query = new URLSearchParams();

    if (options.page) query.set('page', String(options.page));
    if (options.pageSize) query.set('pageSize', String(options.pageSize));
    if (options.search) query.set('search', options.search);
    if (options.action) query.set('action', options.action);
    if (options.severity) query.set('severity', options.severity);
    if (options.ipAddress) query.set('ipAddress', options.ipAddress);
    if (options.from) query.set('from', options.from);
    if (options.to) query.set('to', options.to);
    if (options.includeTotal) query.set('includeTotal', 'true');

    const response = await fetchApi(`/Log${query.toString() ? `?${query.toString()}` : ''}`, {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    const result = await ensureSuccess(response, 'Không thể tải nhật ký hệ thống.');

    if (options.includeTotal) {
        return {
            items: Array.isArray(result?.items) ? result.items : [],
            total: Number(result?.total || 0),
            page: Number(result?.page || options.page || 1),
            pageSize: Number(result?.pageSize || options.pageSize || 50)
        };
    }

    return Array.isArray(result) ? result : [];
};

export const getLogMode = async (token) => {
    const response = await fetchApi('/Log/mode', {
        method: 'GET',
        headers: withAuthHeaders(token)
    });

    return ensureSuccess(response, 'Không thể lấy chế độ ghi log.');
};

export const setLogMode = async (token, enabled) => {
    const response = await fetchApi('/Log/mode', {
        method: 'PUT',
        headers: withAuthHeaders(token),
        body: JSON.stringify({ enabled })
    });

    return ensureSuccess(response, 'Không thể cập nhật chế độ ghi log.');
};
