const BASE_URL = 'http://localhost:5199/api';

export const getSystemLogs = async () => {
    // Lấy token từ LocalStorage
    const token = localStorage.getItem('soc_token');
    
    if (!token) return null; // Nếu không có token thì từ chối gọi

    try {
        const response = await fetch(`${BASE_URL}/Log`, { // Giả định API của Quý là /api/Log
            method: 'GET',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}` // Gắn chìa khóa vào Header
            }
        });
        
        if (!response.ok) {
            if (response.status === 401) throw new Error('Hết hạn đăng nhập');
            throw new Error('Lỗi lấy dữ liệu');
        }
        
        return await response.json(); 
    } catch (error) {
        console.error("Lỗi:", error);
        return null;
    }
};