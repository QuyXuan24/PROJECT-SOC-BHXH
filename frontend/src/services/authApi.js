const BASE_URL = 'http://localhost:5199/api';

export const loginUser = async (username, password) => {
    try {
        const response = await fetch(`${BASE_URL}/Auth/login`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({ username, password })
        });
        
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || 'Đăng nhập thất bại');
        }
        
        return await response.json(); 
    } catch (error) {
        console.error("Lỗi gọi API:", error);
        return null;
    }
};