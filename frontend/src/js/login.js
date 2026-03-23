import { loginUser } from '../services/authApi.js';

document.getElementById('loginForm').addEventListener('submit', async (e) => {
    e.preventDefault(); 
    
    const user = document.getElementById('username').value;
    const pass = document.getElementById('password').value;
    const errorBox = document.getElementById('errorMessage');
    
    errorBox.classList.add('d-none');
    
    const result = await loginUser(user, pass);
    
    if (result) {
        alert("Đăng nhập thành công! Đang chuyển hướng...");
        
        // 1. Lưu token vào LocalStorage để dùng cho các trang sau
        localStorage.setItem('soc_token', result.token); 
        
        // 2. Chuyển hướng sang trang Dashboard
        window.location.href = 'dashboard.html';
    } else {
        errorBox.textContent = "Sai tài khoản hoặc mật khẩu, hoặc server từ chối kết nối!";
        errorBox.classList.remove('d-none');
    }
});