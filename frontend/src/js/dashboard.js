import { getSystemLogs } from '../services/logApi.js';

// 1. Kiểm tra đăng nhập ngay khi vào trang
const token = localStorage.getItem('soc_token');
if (!token) {
    alert("Bạn chưa đăng nhập hoặc phiên đã hết hạn!");
    window.location.href = 'login.html'; // Đuổi về trang login
}

// 2. Hàm đổ dữ liệu vào bảng
const loadLogs = async () => {
    const tableBody = document.getElementById('logTableBody');
    tableBody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-muted">Đang tải dữ liệu từ Server...</td></tr>';

    const logs = await getSystemLogs();

    if (logs && logs.length > 0) {
        tableBody.innerHTML = ''; // Xóa dòng chữ "Đang tải..."
        logs.forEach(log => {
            // Tùy chỉnh màu sắc dựa trên hành động
            let actionBadge = `<span class="badge bg-secondary">${log.action || 'Unknown'}</span>`;
            if (log.action?.includes('Attack') || log.action?.includes('Failed')) {
                actionBadge = `<span class="badge bg-danger">${log.action}</span>`;
            } else if (log.action?.includes('Login')) {
                actionBadge = `<span class="badge bg-success">${log.action}</span>`;
            }

            const row = `
                <tr>
                    <td class="py-3 text-muted">#${log.id}</td>
                    <td class="py-3 fw-bold">${actionBadge}</td>
                    <td class="py-3">${log.content || log.details || 'Không có chi tiết'}</td>
                    <td class="py-3 text-info font-monospace">${log.ipAddress || '127.0.0.1'}</td>
                    <td class="py-3 text-muted">${new Date(log.createdAt).toLocaleString('vi-VN')}</td>
                </tr>
            `;
            tableBody.insertAdjacentHTML('beforeend', row);
        });
    } else {
        tableBody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-warning">Chưa có dữ liệu log nào hoặc không đủ quyền truy cập.</td></tr>';
    }
};

// 3. Xử lý nút Đăng xuất
document.getElementById('btnLogout').addEventListener('click', () => {
    if(confirm("Bạn có chắc chắn muốn đăng xuất khỏi hệ thống SOC?")) {
        localStorage.removeItem('soc_token'); // Xóa chìa khóa
        window.location.href = 'login.html'; // Quay về login
    }
});

// Chạy hàm lấy log khi load trang
loadLogs();