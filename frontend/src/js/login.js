// 1. Dùng đường dẫn tuyệt đối để tránh lỗi lặp /pages/pages/
import { loginUser } from '/services/authApi.js';

document.getElementById('loginForm').addEventListener('submit', async (e) => {
    e.preventDefault(); 
    
    // Lấy các element cần thiết
    const userField = document.getElementById('username');
    const passField = document.getElementById('password');
    const errorBox = document.getElementById('errorMessage');
    const btn = e.target.querySelector('button[type="submit"]');
    
    // Lưu lại trạng thái nút bấm ban đầu
    const originalBtnText = btn.innerHTML;
    
    // Reset thông báo lỗi
    errorBox.classList.add('d-none');
    errorBox.textContent = "";

    // 2. Hiệu ứng Loading (Rất quan trọng cho SOC)
    btn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang xác thực Blockchain...';
    btn.disabled = true;

    try {
        // 3. Thực hiện gọi API thật (Phải có await vì đây là hàm bất đồng bộ)
        const result = await loginUser(userField.value, passField.value);
        
        // Giả sử Backend trả về object có { success: true, token: "...", message: "..." }
        if (result && result.success) {
            alert("✅ Xác thực thành công! ID phiên làm việc đã được ghi nhận.");
            
            // Lưu token vào LocalStorage
            localStorage.setItem('soc_token', result.token); 
            
            // 4. Chuyển hướng (Dùng đường dẫn tuyệt đối / để an toàn)
            window.location.href = '/pages/dashboard.html';
        } else {
            // Hiển thị lỗi từ Backend trả về (ví dụ: Sai mật khẩu)
            errorBox.textContent = result.message || "Tài khoản hoặc mật khẩu không chính xác!";
            errorBox.classList.remove('d-none');
            
            // Trả lại trạng thái nút bấm
            btn.innerHTML = originalBtnText;
            btn.disabled = false;
        }
    } catch (error) {
        // 5. Xử lý khi rớt mạng hoặc Server Backend (.NET) chưa bật
        console.error("SOC Connection Error:", error);
        errorBox.textContent = "Loi ket noi: Khong the cham toi may chu SOC. Hay kiem tra Backend!";
        errorBox.classList.remove('d-none');
        btn.innerHTML = originalBtnText;
        btn.disabled = false;
        return;
    }
});
