export const formatDateTime = (value) => {
    if (!value) return '-';
    if (typeof value === 'string' && /^\d{2}\/\d{2}\/\d{4}$/.test(value)) {
        return value;
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    return date.toLocaleString('vi-VN');
};

export const formatDateOnly = (value) => {
    if (!value) return '-';
    if (typeof value === 'string' && /^\d{2}\/\d{2}\/\d{4}$/.test(value)) {
        return value;
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
};

export const formatDate = formatDateTime;

export const formatGender = (value) => {
    if (!value) return '-';
    const normalized = String(value).trim().toLowerCase();
    if (['nam', 'male', 'm'].includes(normalized)) return 'Nam';
    if (['nữ', 'nu', 'female', 'f'].includes(normalized)) return 'Nữ';
    if (['khác', 'other', 'o'].includes(normalized)) return 'Khác';
    return String(value);
};

export const formatPhone = (value) => {
    if (!value) return '-';
    const digits = String(value).replace(/[^0-9+]/g, '');
    if (!digits) return '-';
    if (digits.startsWith('+84')) {
        return digits.replace(/\+84(\d{1})(\d{3})(\d{3})(\d{2})$/, '+84 $1 $2 $3 $4');
    }
    if (digits.startsWith('0')) {
        return digits.replace(/(0\d{1})(\d{3})(\d{3})(\d{2})$/, '$1 $2 $3 $4');
    }
    return digits;
};

export const formatBhxhCode = (value) => {
    if (!value) return '-';
    const digits = String(value).replace(/\D/g, '');
    if (digits.length !== 10) return String(value);
    return `${digits.slice(0, 2)} ${digits.slice(2, 5)} ${digits.slice(5, 8)} ${digits.slice(8)}`;
};

export const formatCurrency = (value) => {
    if (value === null || value === undefined || value === '') {
        return 'Chưa cập nhật';
    }
    const number = Number(value);
    return Number.isFinite(number) ? `${number.toLocaleString('vi-VN')} đ` : String(value);
};

export const formatTime = (value) => {
    if (!value) return '-';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    return date.toLocaleTimeString('vi-VN', { hour12: false });
};

export const formatNumber = (value) => {
    const number = Number(value || 0);
    return Number.isFinite(number) ? number.toLocaleString('vi-VN') : String(value);
};
