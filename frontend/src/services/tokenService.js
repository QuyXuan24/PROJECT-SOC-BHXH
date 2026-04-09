const TOKEN_KEY = 'soc_token';

export const getToken = () => localStorage.getItem(TOKEN_KEY) || '';

export const setToken = (token) => {
    if (token) {
        localStorage.setItem(TOKEN_KEY, token);
    }
};

export const clearToken = () => {
    localStorage.removeItem(TOKEN_KEY);
};

export const parseJwtPayload = (token) => {
    try {
        const payload = token.split('.')[1];
        if (!payload) {
            return null;
        }

        const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
        const json = atob(normalized);
        return JSON.parse(json);
    } catch {
        return null;
    }
};

export const getCurrentRole = () => {
    const token = getToken();
    const payload = parseJwtPayload(token);
    if (!payload) {
        return '';
    }

    return payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || '';
};

export const isTokenExpired = () => {
    const token = getToken();
    const payload = parseJwtPayload(token);
    if (!payload?.exp) {
        return true;
    }

    const now = Math.floor(Date.now() / 1000);
    return payload.exp <= now;
};
