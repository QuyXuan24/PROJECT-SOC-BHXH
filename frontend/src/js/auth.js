// Xử lý Đăng nhập người dân
document.getElementById('citizenLoginForm').addEventListener('submit', (e) => {
    e.preventDefault(); // Chặn load lại trang
    
    // Giả lập đang xử lý mạng
    const btn = e.target.querySelector('button[type="submit"]');
    const originalText = btn.innerHTML;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang kết nối SOC...';
    btn.disabled = true;

    setTimeout(() => {
        // Giả lập đăng nhập thành công 100% để demo
        alert("Xác thực thành công! Chuyển đến trang quản lý cá nhân.");
        window.location.href = 'user-dashboard.html';
    }, 1000);
});

// Xử lý Đăng ký người dân
document.getElementById('citizenRegisterForm').addEventListener('submit', (e) => {
    e.preventDefault();
    alert("Đăng ký hồ sơ thành công! Dữ liệu của bạn đang được mã hóa và đồng bộ.");
    // Chuyển tab sang form đăng nhập (tự động click vào tab đăng nhập)
    document.getElementById('login-tab').click();
});