// Đường dẫn gốc tới Backend .NET (Cổng 5199)
import { fetchApi } from '/services/apiClient.js';

/**
 * Hàm lấy danh sách nhật ký hệ thống (System Logs)
 * Được bảo mật bằng JWT và phân quyền Admin/SOC
 */
export const getSystemLogs = async () => {
    // 1. Lấy token "chìa khóa" từ kho lưu trữ của trình duyệt
    const token = localStorage.getItem('soc_token');
    
    // Nếu chưa đăng nhập (không có token), trả về null ngay lập tức
    if (!token) {
        console.warn("SOC Trace: Truy cập bị từ chối do thiếu Token.");
        return null; 
    }

    try {
        // 2. Gọi API chuẩn theo LogController của Quý (/api/Log)
        // Lưu ý: Không thêm /all vì Backend của Quý dùng Route mặc định
        const response = await fetchApi(`/Log`, { 
            method: 'GET',
            headers: { 
                'Content-Type': 'application/json',
                // Gửi token vào Header Authorization theo chuẩn Bearer
                'Authorization': `Bearer ${token}` 
            }
        });
        
        // 3. Xử lý các kịch bản phản hồi từ Server
        if (!response.ok) {
            // Trường hợp 1: Token hết hạn hoặc không hợp lệ (401)
            if (response.status === 401) {
                alert("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại!");
                localStorage.removeItem('soc_token');
                window.location.href = '/pages/login.html';
                throw new Error('Unauthorized');
            }
            
            // Trường hợp 2: Có đăng nhập nhưng không phải quyền Admin/SOC (403)
            if (response.status === 403) {
                alert("Bạn không có quyền truy cập vào nhật ký SOC (Yêu cầu quyền Admin).");
                throw new Error('Forbidden');
            }

            throw new Error(`Lỗi Server: ${response.status}`);
        }
        
        // 4. Trả về dữ liệu JSON (Danh sách logs) cho Dashboard hiển thị
        return await response.json(); 

    } catch (error) {
        // Ghi log lỗi ra console để chúng ta dễ "bắt bệnh" khi debug
        console.error("SOC API Connection Error:", error.message);
        return null;
    }
};
