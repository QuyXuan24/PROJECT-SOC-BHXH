const express = require('express');
const cors = require('cors');
const { Gateway, Wallets } = require('fabric-network');
const path = require('path');
const fs = require('fs');

const app = express();
const PORT = Number(process.env.PORT || 3001);

const FABRIC_ENABLED = String(process.env.FABRIC_ENABLED || 'false').toLowerCase() === 'true';
const CHANNEL_NAME = process.env.FABRIC_CHANNEL || 'mychannel';
const CONTRACT_NAME = process.env.FABRIC_CONTRACT || 'soc_bhxh';
const FABRIC_IDENTITY = process.env.FABRIC_IDENTITY || 'appUser';
const FABRIC_AS_LOCALHOST = String(process.env.FABRIC_AS_LOCALHOST || 'true').toLowerCase() !== 'false';

const ccpPath = path.resolve(__dirname, process.env.FABRIC_CONNECTION_FILE || 'connection-org1.json');
const walletPath = path.resolve(__dirname, process.env.FABRIC_WALLET_DIR || 'wallet');

let fabricGateway = null;
let fabricContract = null;

// Mock ledger: dung khi chay demo khong co Hyperledger Fabric that.
const localHashLedger = new Map();

app.use(cors());
app.use(express.json());

const normalizeRecordKey = (recordKey, username, logType) => {
    if (typeof recordKey === 'string' && recordKey.trim()) {
        return recordKey.trim();
    }
    const safeUser = (username || 'system').toString().trim() || 'system';
    const safeType = (logType || 'event').toString().trim() || 'event';
    return `${safeType}:${safeUser}`;
};

async function getFabricContract() {
    if (!FABRIC_ENABLED) {
        return null;
    }

    if (fabricContract) {
        return fabricContract;
    }

    if (!fs.existsSync(ccpPath)) {
        throw new Error(`Fabric connection profile not found: ${ccpPath}`);
    }

    if (!fs.existsSync(walletPath)) {
        throw new Error(`Fabric wallet directory not found: ${walletPath}`);
    }

    const ccp = JSON.parse(fs.readFileSync(ccpPath, 'utf8'));
    const wallet = await Wallets.newFileSystemWallet(walletPath);

    fabricGateway = new Gateway();
    await fabricGateway.connect(ccp, {
        wallet,
        identity: FABRIC_IDENTITY,
        discovery: { enabled: true, asLocalhost: FABRIC_AS_LOCALHOST }
    });

    const network = await fabricGateway.getNetwork(CHANNEL_NAME);
    fabricContract = network.getContract(CONTRACT_NAME);
    return fabricContract;
}

app.post('/submit', async (req, res) => {
    try {
        const { username, logType, message, hash, ipAddress, recordKey } = req.body;
        const safeRecordKey = normalizeRecordKey(recordKey, username, logType);

        if (!hash || typeof hash !== 'string') {
            return res.status(400).json({ success: false, error: 'Missing hash in request body.' });
        }

        console.log(`[BRIDGE] Received submit for user=${username || 'unknown'}, type=${logType || 'unknown'}, key=${safeRecordKey}`);
        console.log(`[HASH] ${hash}`);

        // Luu local cache de ho tro verify trong mode demo.
        localHashLedger.set(safeRecordKey, hash);

        let txId = `tx-${Math.random().toString(36).substring(2, 11)}`;
        let mode = 'mock';

        if (FABRIC_ENABLED) {
            const contract = await getFabricContract();

            // Goi ham chaincode moi: RegisterLog(recordKey, username, logType, hash, ipAddress)
            await contract.submitTransaction(
                'RegisterLog',
                safeRecordKey,
                (username || 'Unknown').toString(),
                (logType || 'SYSTEM_EVENT').toString(),
                hash,
                (ipAddress || 'Unknown IP').toString()
            );

            mode = 'fabric';
            txId = `fabric-${Date.now()}`;
        } else {
            await new Promise((resolve) => setTimeout(resolve, 120));
        }

        return res.status(200).json({
            success: true,
            mode,
            recordKey: safeRecordKey,
            txId,
            blockHeight: Math.floor(Math.random() * 1000),
            message: 'Hash has been recorded successfully.'
        });
    } catch (error) {
        console.error(`[ERROR][SUBMIT] ${error.message}`);
        return res.status(500).json({ success: false, error: error.message });
    }
});

app.post('/verify', async (req, res) => {
    try {
        const { recordKey, hash } = req.body;
        if (!recordKey || typeof recordKey !== 'string') {
            return res.status(400).json({ success: false, error: 'Missing recordKey.' });
        }
        if (!hash || typeof hash !== 'string') {
            return res.status(400).json({ success: false, error: 'Missing hash.' });
        }

        const safeRecordKey = recordKey.trim();

        // Mode demo: doi chieu local cache.
        let chainHash = localHashLedger.get(safeRecordKey) || null;
        let verified = chainHash === hash;
        let source = 'mock_ledger';

        if (FABRIC_ENABLED) {
            source = 'fabric';
            try {
                const contract = await getFabricContract();

                // Neu chaincode da co ham VerifyRecordHash(recordKey, hash), dung evaluate duoi day.
                // Neu chua co, se roi vao catch va fallback local cache.
                const resultBuffer = await contract.evaluateTransaction('VerifyRecordHash', safeRecordKey, hash);
                const raw = (resultBuffer || Buffer.from('')).toString('utf8').trim();
                verified = raw === 'true' || raw === '1' || raw.toLowerCase() === 'verified';
                chainHash = verified ? hash : chainHash;
            } catch (fabricVerifyError) {
                source = 'local_cache_fallback';
                chainHash = localHashLedger.get(safeRecordKey) || null;
                verified = chainHash === hash;
                console.warn(`[WARN][VERIFY] Fabric verify fallback: ${fabricVerifyError.message}`);
            }
        }

        return res.status(200).json({
            success: true,
            verified,
            recordKey: safeRecordKey,
            chainHash,
            source
        });
    } catch (error) {
        console.error(`[ERROR][VERIFY] ${error.message}`);
        return res.status(500).json({ success: false, error: error.message });
    }
});

app.get('/', (_req, res) => {
    res.json({
        service: 'SOC Blockchain Bridge',
        status: 'running',
        port: PORT,
        mode: FABRIC_ENABLED ? 'fabric' : 'mock'
    });
});

app.listen(PORT, '0.0.0.0', () => {
    console.log(`Bridge listening on http://0.0.0.0:${PORT} (mode=${FABRIC_ENABLED ? 'fabric' : 'mock'})`);
});
