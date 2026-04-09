import { guardPage, logout } from '/js/auth-guard.js';
import { getSystemLogs } from '/services/logApi.js';
import {
    blockIp,
    createIncident,
    getRealtimeAlerts,
    getSecurityOverview,
    lockAccount
} from '/services/securityApi.js';
import { getToken } from '/services/tokenService.js';
import { formatDateTime, formatTime, formatNumber } from '/js/formatters.js';

if (!guardPage(['Security', 'SOC', 'Admin'], '/pages/login.html')) {
    throw new Error('Unauthorized');
}

const AUTO_REFRESH_MS = 10000;

const token = getToken();

const elements = {
    btnLogout: document.getElementById('btnLogout'),
    btnRefreshNow: document.getElementById('btnRefreshNow'),
    btnToggleAutoRefresh: document.getElementById('btnToggleAutoRefresh'),
    btnRefreshAlerts: document.getElementById('btnRefreshAlerts'),
    btnRefreshLogs: document.getElementById('btnRefreshLogs'),
    lastRefreshText: document.getElementById('lastRefreshText'),

    metricTotalLogs: document.getElementById('metricTotalLogs'),
    metricFailedLogins: document.getElementById('metricFailedLogins'),
    metricBlockedIps: document.getElementById('metricBlockedIps'),
    metricOpenIncidents: document.getElementById('metricOpenIncidents'),
    metricCriticalAlerts: document.getElementById('metricCriticalAlerts'),
    metricLogMode: document.getElementById('metricLogMode'),

    topIpTableBody: document.getElementById('topIpTableBody'),

    alertSeverityFilter: document.getElementById('alertSeverityFilter'),
    alertTableBody: document.getElementById('alertTableBody'),

    logSearchInput: document.getElementById('logSearchInput'),
    logSeverityFilter: document.getElementById('logSeverityFilter'),
    logActionFilter: document.getElementById('logActionFilter'),
    logSummaryText: document.getElementById('logSummaryText'),
    logTableBody: document.getElementById('logTableBody'),

    detailModalTitle: document.getElementById('detailModalTitle'),
    detailModalBody: document.getElementById('detailModalBody'),
    toastBody: document.getElementById('socToastBody'),
    toastTime: document.getElementById('socToastTime')
};

const detailModal = window.bootstrap?.Modal
    ? new window.bootstrap.Modal(document.getElementById('detailModal'))
    : null;

const socToast = window.bootstrap?.Toast
    ? new window.bootstrap.Toast(document.getElementById('socToast'), { delay: 3200 })
    : null;

const state = {
    autoRefreshEnabled: true,
    timer: null,
    overview: null,
    alerts: [],
    alertMap: new Map(),
    logs: [],
    logTotal: 0
};

wireEvents();
await refreshAll();
startAutoRefresh();

function wireEvents() {
    elements.btnLogout?.addEventListener('click', () => {
        logout('/pages/login.html');
    });

    elements.btnRefreshNow?.addEventListener('click', async () => {
        await refreshAll();
    });

    elements.btnToggleAutoRefresh?.addEventListener('click', () => {
        state.autoRefreshEnabled = !state.autoRefreshEnabled;
        if (state.autoRefreshEnabled) {
            startAutoRefresh();
            showToast('Auto refresh da bat (10 giay/lap).', 'success');
        } else {
            stopAutoRefresh();
            showToast('Auto refresh da tat.', 'warning');
        }
        renderAutoRefreshButton();
    });

    elements.btnRefreshAlerts?.addEventListener('click', async () => {
        await loadAlerts();
    });

    elements.btnRefreshLogs?.addEventListener('click', async () => {
        await loadLogs();
    });

    elements.alertSeverityFilter?.addEventListener('change', () => {
        renderAlerts();
    });

    elements.logSearchInput?.addEventListener('input', () => {
        renderLogs();
    });

    elements.logSeverityFilter?.addEventListener('change', () => {
        renderLogs();
    });

    elements.logActionFilter?.addEventListener('change', () => {
        renderLogs();
    });

    elements.alertTableBody?.addEventListener('click', onAlertTableClick);
    elements.logTableBody?.addEventListener('click', onLogTableClick);
}

async function refreshAll() {
    await Promise.all([loadOverview(), loadAlerts(), loadLogs()]);
    elements.lastRefreshText.textContent = `Cap nhat luc ${formatDateTime(new Date())}`;
}

function startAutoRefresh() {
    stopAutoRefresh();
    state.timer = setInterval(async () => {
        if (!state.autoRefreshEnabled) {
            return;
        }

        try {
            await refreshAll();
        } catch {
            // ignore background refresh error
        }
    }, AUTO_REFRESH_MS);

    renderAutoRefreshButton();
}

function stopAutoRefresh() {
    if (state.timer) {
        clearInterval(state.timer);
        state.timer = null;
    }
}

function renderAutoRefreshButton() {
    if (!elements.btnToggleAutoRefresh) {
        return;
    }

    if (state.autoRefreshEnabled) {
        elements.btnToggleAutoRefresh.className = 'btn btn-success btn-sm';
        elements.btnToggleAutoRefresh.innerHTML = '<i class="bi bi-broadcast-pin me-1"></i>Tự động làm mới: BẬT (10s)';
    } else {
        elements.btnToggleAutoRefresh.className = 'btn btn-outline-secondary btn-sm';
        elements.btnToggleAutoRefresh.innerHTML = '<i class="bi bi-pause-circle me-1"></i>Tự động làm mới: TẮT';
    }
}

async function loadOverview() {
    try {
        const overview = await getSecurityOverview(token);
        state.overview = overview;

        elements.metricTotalLogs.textContent = formatNumber(overview.totalLogs24h || 0);
        elements.metricFailedLogins.textContent = formatNumber(overview.failedLogins24h || 0);
        elements.metricBlockedIps.textContent = formatNumber(overview.activeBlockedIps || 0);
        elements.metricOpenIncidents.textContent = formatNumber(overview.openIncidents || 0);
        elements.metricCriticalAlerts.textContent = formatNumber(overview.criticalAlerts || 0);
        elements.metricLogMode.textContent = overview.detailedLoggingEnabled ? 'ON' : 'OFF';

        renderTopIpTable(overview.topSourceIps || []);
        renderAttackLevels(overview.attackByType || []);
    } catch (error) {
        showToast(error.message || 'Không tải được tổng quan SOC.', 'danger');
    }
}

async function loadAlerts() {
    elements.alertTableBody.innerHTML = '<tr><td colspan="6" class="text-center text-secondary">Đang tải cảnh báo...</td></tr>';

    try {
        const alerts = await getRealtimeAlerts(token, 60);
        state.alerts = alerts;
        state.alertMap = new Map(alerts.map((item) => [String(item.alertId), item]));
        renderAlerts();
    } catch (error) {
        elements.alertTableBody.innerHTML = `<tr><td colspan="6" class="text-danger text-center">${escapeHtml(error.message || 'Không tải được cảnh báo.')}</td></tr>`;
    }
}

function renderAlerts() {
    const severityFilter = (elements.alertSeverityFilter?.value || '').trim();
    const rows = state.alerts.filter((alert) => !severityFilter || normalizeSeverity(alert.severity) === severityFilter);

    if (!rows.length) {
        elements.alertTableBody.innerHTML = '<tr><td colspan="6" class="text-center text-secondary">Không có cảnh báo phù hợp.</td></tr>';
        return;
    }

    elements.alertTableBody.innerHTML = rows.map((alert) => {
        const severity = normalizeSeverity(alert.severity);
        const source = [alert.sourceIp || '-', alert.username || '-'].join(' / ');

        return `
            <tr>
                <td>${severityChip(severity)}</td>
                <td>${escapeHtml(alert.category || '-')}</td>
                <td>
                    <div class="fw-semibold">${escapeHtml(alert.title || '-')}</div>
                    <div class="text-secondary small">${escapeHtml(alert.description || '-')}</div>
                </td>
                <td class="font-monospace small">${escapeHtml(source)}</td>
                <td class="small text-secondary">${formatDateTime(alert.createdAt)}</td>
                <td class="text-end">
                    <div class="btn-group btn-group-sm" role="group">
                        <button class="btn btn-outline-danger" data-action="block" data-alert-id="${escapeAttr(alert.alertId)}">🚫 BLOCK IP</button>
                        <button class="btn btn-outline-warning" data-action="lock" data-alert-id="${escapeAttr(alert.alertId)}">🔒 KHOA TK</button>
                        <button class="btn btn-outline-info" data-action="incident" data-alert-id="${escapeAttr(alert.alertId)}">⚠️ TAO INCIDENT</button>
                        <button class="btn btn-outline-light" data-action="detail" data-alert-id="${escapeAttr(alert.alertId)}">🔍 CHI TIET</button>
                    </div>
                </td>
            </tr>
        `;
    }).join('');
}

async function onAlertTableClick(event) {
    const button = event.target.closest('button[data-action]');
    if (!button) {
        return;
    }

    const alertId = button.dataset.alertId;
    const action = button.dataset.action;
    const alert = state.alertMap.get(String(alertId));

    if (!alert) {
        showToast('Không tìm thấy dữ liệu cảnh báo.', 'warning');
        return;
    }

    if (action === 'detail') {
        openDetailModal(`Cảnh báo ${alert.alertId}`, alert);
        return;
    }

    if (action === 'block') {
        if (!alert.sourceIp) {
            showToast('Cảnh báo này không có IP để block.', 'warning');
            return;
        }

        await withButtonLoading(button, async () => {
            await blockIp(token, {
                ipAddress: alert.sourceIp,
                reason: `SOC block từ cảnh báo ${alert.alertId}`,
                durationMinutes: 120
            });

            showToast(`Da chan IP ${alert.sourceIp}.`, 'success');
            await Promise.all([loadOverview(), loadAlerts()]);
        });
        return;
    }

    if (action === 'lock') {
        if (!alert.username) {
            showToast('Cảnh báo này không có username để khóa.', 'warning');
            return;
        }

        await withButtonLoading(button, async () => {
            await lockAccount(token, {
                username: alert.username,
                reason: `SOC khóa tài khoản từ cảnh báo ${alert.alertId}`
            });

            showToast(`Đã khóa tài khoản ${alert.username}.`, 'success');
            await Promise.all([loadOverview(), loadAlerts(), loadLogs()]);
        });
        return;
    }

    if (action === 'incident') {
        await withButtonLoading(button, async () => {
            await createIncident(token, {
                title: `[${normalizeSeverity(alert.severity)}] ${alert.title || 'Cảnh báo SOC'}`,
                severity: normalizeSeverity(alert.severity),
                description: alert.description || 'Cảnh báo được tạo từ SOC dashboard.',
                category: alert.category || 'RealtimeAlert',
                sourceIp: alert.sourceIp || null,
                username: alert.username || null,
                relatedLogId: alert.relatedLogId || null
            });

            showToast('Da tao incident thanh cong.', 'success');
            await Promise.all([loadOverview(), loadAlerts()]);
        });
    }
}

async function loadLogs() {
    elements.logTableBody.innerHTML = '<tr><td colspan="7" class="text-center text-secondary">Đang tải log...</td></tr>';

    try {
        const result = await getSystemLogs(token, {
            page: 1,
            pageSize: 300,
            includeTotal: true
        });

        state.logs = result.items || [];
        state.logTotal = result.total || state.logs.length;

        refillActionFilter(state.logs);
        renderLogs();
    } catch (error) {
        elements.logTableBody.innerHTML = `<tr><td colspan="7" class="text-danger text-center">${escapeHtml(error.message || 'Không thể tải log.')}</td></tr>`;
        elements.logSummaryText.textContent = 'Tai log that bai.';
    }
}

function renderLogs() {
    const searchText = (elements.logSearchInput?.value || '').trim().toLowerCase();
    const severityFilter = (elements.logSeverityFilter?.value || '').trim();
    const actionFilter = (elements.logActionFilter?.value || '').trim();

    const filtered = state.logs.filter((log) => {
        const severity = deriveLogSeverity(log);
        const inSeverity = !severityFilter || severity === severityFilter;
        const inAction = !actionFilter || String(log.action || '').includes(actionFilter);

        if (!inSeverity || !inAction) {
            return false;
        }

        if (!searchText) {
            return true;
        }

        const target = [log.action, log.content, log.username, log.ipAddress]
            .map((x) => String(x || '').toLowerCase())
            .join(' ');

        return target.includes(searchText);
    });

    elements.logSummaryText.textContent = `Đang hiển thị ${filtered.length}/${state.logs.length} log (tổng trên server: ${state.logTotal}).`;

    if (!filtered.length) {
        elements.logTableBody.innerHTML = '<tr><td colspan="7" class="text-center text-secondary">Không có log phù hợp bộ lọc.</td></tr>';
        return;
    }

    elements.logTableBody.innerHTML = filtered.map((log) => {
        const severity = deriveLogSeverity(log);

        return `
            <tr>
                <td class="text-secondary">#${escapeHtml(log.id)}</td>
                <td>${severityChip(severity)} <span class="ms-1">${escapeHtml(log.action || '-')}</span></td>
                <td>${escapeHtml(shorten(log.content || '-', 120))}</td>
                <td>${escapeHtml(log.username || '-')}</td>
                <td class="font-monospace small">${escapeHtml(log.ipAddress || '-')}</td>
                <td class="text-secondary small">${formatDateTime(log.createdAt)}</td>
                <td>
                    <button class="btn btn-outline-light btn-sm" data-action="log-detail" data-log-id="${escapeAttr(log.id)}">
                        Chi tiet
                    </button>
                </td>
            </tr>
        `;
    }).join('');
}

function onLogTableClick(event) {
    const button = event.target.closest('button[data-action="log-detail"]');
    if (!button) {
        return;
    }

    const logId = Number(button.dataset.logId);
    const log = state.logs.find((item) => Number(item.id) === logId);
    if (!log) {
        return;
    }

    openDetailModal(`Log #${logId}`, log);
}

function refillActionFilter(logs) {
    const current = elements.logActionFilter.value;
    const actions = [...new Set(logs.map((log) => String(log.action || '').trim()).filter(Boolean))]
        .sort((a, b) => a.localeCompare(b))
        .slice(0, 80);

    elements.logActionFilter.innerHTML = '<option value="">Tất cả action</option>';
    actions.forEach((action) => {
        const option = document.createElement('option');
        option.value = action;
        option.textContent = action;
        elements.logActionFilter.appendChild(option);
    });

    if (actions.includes(current)) {
        elements.logActionFilter.value = current;
    }
}

async function withButtonLoading(button, fn) {
    const originalHtml = button.innerHTML;
    button.disabled = true;
    button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';

    try {
        await fn();
    } catch (error) {
        showToast(error.message || 'Thao tác thất bại.', 'danger');
    } finally {
        button.disabled = false;
        button.innerHTML = originalHtml;
    }
}

function renderTopIpTable(items) {
    if (!Array.isArray(items) || !items.length) {
        elements.topIpTableBody.innerHTML = '<tr><td colspan="2" class="text-center text-secondary">Không có dữ liệu.</td></tr>';
        return;
    }

    elements.topIpTableBody.innerHTML = items.map((item) => `
        <tr>
            <td class="font-monospace">${escapeHtml(item.ipAddress || '-')}</td>
            <td class="text-end">${formatNumber(item.count || 0)}</td>
        </tr>
    `).join('');
}



function renderAttackLevels(items) {
    // Phân loại items theo severity level
    const attacks = items || [];
    
    // Giả định: nếu có field 'severity' hoặc 'level', sử dụng đó
    // Nếu không, dùng index/position để phân loại
    const low = attacks.filter(a => 
        (a.severity || '').toLowerCase() === 'low' || 
        (a.level || '').toLowerCase() === 'low'
    ).slice(0, 5);
    
    const medium = attacks.filter(a => 
        (a.severity || '').toLowerCase() === 'medium' || 
        (a.level || '').toLowerCase() === 'medium'
    ).slice(0, 5);
    
    const high = attacks.filter(a => 
        (a.severity || '').toLowerCase() === 'high' || 
        (a.level || '').toLowerCase() === 'high'
    ).slice(0, 5);
    
    // Nếu không có severity/level field, phân chia dựa trên count (thấp -> cao)
    if (low.length === 0 && medium.length === 0 && high.length === 0) {
        const sorted = [...attacks].sort((a, b) => (a.count || 0) - (b.count || 0));
        const third = Math.floor(sorted.length / 3);
        low.push(...sorted.slice(0, third));
        medium.push(...sorted.slice(third, third * 2));
        high.push(...sorted.slice(third * 2));
    }
    
    renderLevelList('countLow', 'listLow', low);
    renderLevelList('countMedium', 'listMedium', medium);
    renderLevelList('countHigh', 'listHigh', high);
}

function renderLevelList(countId, listId, items) {
    const countEl = document.getElementById(countId);
    const listEl = document.getElementById(listId);
    
    if (!countEl || !listEl) return;
    
    const count = (items || []).length;
    const total = (items || []).reduce((sum, item) => sum + (item.count || 0), 0);
    
    countEl.textContent = count > 0 ? `${count} loại (${formatNumber(total)} lần)` : '0';
    
    listEl.innerHTML = (items || []).map(item => `
        <div class="list-group-item bg-transparent border-0 py-2 px-0 d-flex justify-content-between">
            <span class="text-secondary">${escapeHtml(item.type || item.name || item.label || '-')}</span>
            <span class="fw-bold">${formatNumber(item.count || 0)}</span>
        </div>
    `).join('') || '<div class="list-group-item bg-transparent border-0 py-2 px-0 text-secondary">Không có dữ liệu</div>';
}

function openDetailModal(title, payload) {
    elements.detailModalTitle.textContent = title;
    elements.detailModalBody.textContent = JSON.stringify(payload, null, 2);
    detailModal?.show();
}

function showToast(message, level = 'info') {
    if (!elements.toastBody) {
        return;
    }

    elements.toastBody.textContent = message;
    elements.toastTime.textContent = formatTime(new Date());

    const toastElement = document.getElementById('socToast');
    toastElement.classList.remove('border-danger', 'border-success', 'border-warning', 'border-info');

    if (level === 'danger') toastElement.classList.add('border-danger');
    if (level === 'success') toastElement.classList.add('border-success');
    if (level === 'warning') toastElement.classList.add('border-warning');
    if (level === 'info') toastElement.classList.add('border-info');

    socToast?.show();
}

function deriveLogSeverity(log) {
    const action = String(log.action || '').toUpperCase();
    const content = String(log.content || '').toUpperCase();

    if (action.includes('ERROR') || action.includes('FORBIDDEN') || action.includes('UNAUTHORIZED')) {
        return 'Critical';
    }

    if (action.includes('FAILED') || action.includes('LOCK') || action.includes('BLOCK') || content.includes('SQL INJECTION')) {
        return 'High';
    }

    if (action.includes('PROCESS') || action.includes('MODE') || action.includes('INCIDENT')) {
        return 'Medium';
    }

    return 'Low';
}

function severityChip(severity) {
    const normalized = normalizeSeverity(severity);
    const cssClass = normalized === 'Critical'
        ? 'chip-critical'
        : normalized === 'High'
            ? 'chip-high'
            : normalized === 'Medium'
                ? 'chip-medium'
                : 'chip-low';

    return `<span class="chip ${cssClass}">${escapeHtml(normalized)}</span>`;
}

function normalizeSeverity(value) {
    const normalized = String(value || '').trim().toLowerCase();
    if (normalized === 'critical') return 'Critical';
    if (normalized === 'high') return 'High';
    if (normalized === 'medium') return 'Medium';
    if (normalized === 'low') return 'Low';
    return 'Medium';
}

function shorten(value, maxLength) {
    const text = String(value || '');
    if (text.length <= maxLength) {
        return text;
    }
    return `${text.slice(0, maxLength - 1)}...`;
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function escapeAttr(value) {
    return escapeHtml(value).replace(/`/g, '&#96;');
}
