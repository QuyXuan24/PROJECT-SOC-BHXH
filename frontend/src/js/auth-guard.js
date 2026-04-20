import { clearToken, getCurrentRole, getToken, isTokenExpired } from '/services/tokenService.js';

export const guardPage = (allowedRoles, redirectTo = '/pages/auth.html') => {
    const token = getToken();
    if (!token || isTokenExpired()) {
        clearToken();
        window.location.href = redirectTo;
        return false;
    }

    const role = getCurrentRole();
    if (!allowedRoles.includes(role)) {
        window.location.href = '/pages/auth.html';
        return false;
    }

    return true;
};

export const logout = (redirectTo = '/pages/auth.html') => {
    clearToken();
    window.location.href = redirectTo;
};
