const normalizeBaseUrl = (baseUrl) => String(baseUrl || "").replace(/\/+$/, "");

const getApiBaseCandidates = () => {
    if (typeof window === "undefined") {
        return ["http://localhost:5000/api", "http://localhost:5199/api"];
    }

    const override =
        (typeof window.SOC_API_BASE_URL === "string" && window.SOC_API_BASE_URL.trim()) ||
        localStorage.getItem("soc_api_base_url");

    const protocol = window.location.protocol || "http:";
    const hostname = window.location.hostname || "localhost";
    const port = window.location.port || "";

    const candidates = [];

    if (override) {
        candidates.push(override);
    }

    if (port === "3000") {
        candidates.push(`${protocol}//${hostname}:5000/api`);
        candidates.push(`${protocol}//${hostname}:5199/api`);
        candidates.push("/api");
    } else if (port === "80" || port === "") {
        candidates.push("/api");
        candidates.push(`${protocol}//${hostname}:5000/api`);
        candidates.push(`${protocol}//${hostname}:5199/api`);
    } else {
        candidates.push(`${protocol}//${hostname}:5199/api`);
        candidates.push(`${protocol}//${hostname}:5000/api`);
        candidates.push("/api");
    }

    return [...new Set(candidates.map(normalizeBaseUrl).filter(Boolean))];
};

let cachedApiBase = "";

const shouldTryNextBase = (response) => {
    return [404, 502, 503, 504].includes(response.status);
};

export const getApiBaseUrl = () => {
    if (cachedApiBase) {
        return cachedApiBase;
    }

    const [firstBase] = getApiBaseCandidates();
    return firstBase || "";
};

export const fetchApi = async (path, options = {}) => {
    const safePath = path.startsWith("/") ? path : `/${path}`;
    const bases = [cachedApiBase, ...getApiBaseCandidates()].filter(Boolean);
    const uniqueBases = [...new Set(bases)];
    let lastError = null;

    for (let i = 0; i < uniqueBases.length; i += 1) {
        const base = uniqueBases[i];
        const url = `${base}${safePath}`;

        try {
            const response = await fetch(url, options);

            if (shouldTryNextBase(response) && i < uniqueBases.length - 1) {
                continue;
            }

            cachedApiBase = base;
            return response;
        } catch (error) {
            lastError = error;
        }
    }

    throw lastError || new Error("Không thể kết nối tới SOC");
};
