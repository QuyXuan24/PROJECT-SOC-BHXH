import { getSystemLogs } from '/services/logApi.js'; // 1. Dùng đường dẫn tuyệt đối

// 1. Kiểm tra đăng nhập (Chốt chặn bảo mật)
const token = localStorage.getItem('soc_token');
if (!token) {
    alert("Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại!");
    window.location.href = '/pages/login.html'; // 2. Về trang login chuẩn
}

// 2. Hàm đổ dữ liệu vào bảng
const loadLogs = async () => {
    const tableBody = document.getElementById('logTableBody');
    if (!tableBody) return;

    tableBody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-muted"><i class="fas fa-spinner fa-spin me-2"></i>Đang truy vấn sổ cái Blockchain...</td></tr>';

    try {
        // 3. Truyền token để Backend xác thực quyền Admin
        const logs = await getSystemLogs(token);

        if (logs && logs.length > 0) {
            tableBody.innerHTML = ''; 
            logs.forEach(log => {
                // Logic Badge (Giữ nguyên vì bạn làm phần này rất tốt)
                let actionBadge = `<span class="badge bg-secondary">${log.action || 'Unknown'}</span>`;
                if (log.action?.includes('Attack') || log.action?.includes('Failed')) {
                    actionBadge = `<span class="badge bg-danger">${log.action}</span>`;
                } else if (log.action?.includes('Login')) {
                    actionBadge = `<span class="badge bg-success">${log.action}</span>`;
                }

                tableBody.insertAdjacentHTML('beforeend', `
                    <tr>
                        <td class="py-3 text-muted">#${log.id}</td>
                        <td class="py-3 fw-bold">${actionBadge}</td>
                        <td class="py-3">${log.content || log.details || 'N/A'}</td>
                        <td class="py-3 text-info font-monospace">${log.ipAddress || '127.0.0.1'}</td>
                        <td class="py-3 text-muted">${new Date(log.createdAt).toLocaleString('vi-VN')}</td>
                    </tr>
                `);
            });
        } else {
            tableBody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-warning">Hệ thống chưa ghi nhận log nào trên Blockchain.</td></tr>';
        }
    } catch (error) {
        tableBody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-danger">Khong the ket noi toi Server SOC. Vui long kiem tra Backend.</td></tr>';
        return;
    }
};

// 3. Đăng xuất (Dùng đường dẫn tuyệt đối)
document.getElementById('btnLogout')?.addEventListener('click', () => {
    if(confirm("Xác nhận đăng xuất khỏi hệ thống giám sát?")) {
        localStorage.removeItem('soc_token');
        window.location.href = '/pages/login.html';
    }
});

loadLogs();
