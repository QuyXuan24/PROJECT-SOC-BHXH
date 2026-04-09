import { fetchApi } from '/services/apiClient.js';

export const lookupBhxh = async ({ query, cccd = '', otp = '' }) => {
    const params = new URLSearchParams({
        query: query || '',
        cccd,
        otp
    });
    const response = await fetchApi(`/lookup/bhxh?${params.toString()}`);
    const data = await response.json().catch(() => null);
    return { ok: response.ok, status: response.status, data };
};

export const lookupBhyT = async ({ cardNumber, fullName = '', dateOfBirth = '' }) => {
    const params = new URLSearchParams({
        cardNumber: cardNumber || '',
        fullName,
        dateOfBirth
    });
    const response = await fetchApi(`/lookup/bhyt?${params.toString()}`);
    const data = await response.json().catch(() => null);
    return { ok: response.ok, status: response.status, data };
};
