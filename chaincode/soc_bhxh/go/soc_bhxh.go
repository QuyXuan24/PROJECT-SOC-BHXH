package main

import (
    "encoding/json"
    "fmt"
    "strings"
    "time"

    "github.com/hyperledger/fabric-contract-api-go/contractapi"
)

type SmartContract struct {
    contractapi.Contract
}

// RecordHash la du lieu hash duoc luu tren so cai.
type RecordHash struct {
    RecordID   string `json:"recordId"`
    Username   string `json:"username"`
    LogType    string `json:"logType"`
    HashValue  string `json:"hashValue"`
    IPAddress  string `json:"ipAddress"`
    Timestamp  string `json:"timestamp"`
}

// RegisterLog ghi hash vao ledger theo recordKey.
// Chu ky nay khop voi bridge hien tai: RegisterLog(recordKey, username, logType, hash, ipAddress)
func (s *SmartContract) RegisterLog(
    ctx contractapi.TransactionContextInterface,
    recordKey string,
    username string,
    logType string,
    hashValue string,
    ipAddress string,
) error {
    if strings.TrimSpace(recordKey) == "" {
        return fmt.Errorf("recordKey is required")
    }
    if strings.TrimSpace(hashValue) == "" {
        return fmt.Errorf("hashValue is required")
    }

    txTs, err := ctx.GetStub().GetTxTimestamp()
    if err != nil {
        return fmt.Errorf("failed to read tx timestamp: %v", err)
    }

    ts := time.Unix(txTs.Seconds, int64(txTs.Nanos)).UTC().Format(time.RFC3339Nano)
    record := RecordHash{
        RecordID:  recordKey,
        Username:  username,
        LogType:   logType,
        HashValue: hashValue,
        IPAddress: ipAddress,
        Timestamp: ts,
    }

    recordJSON, err := json.Marshal(record)
    if err != nil {
        return err
    }

    return ctx.GetStub().PutState(recordKey, recordJSON)
}

// VerifyRecordHash so sanh hash hien tai voi hash da luu tren ledger.
func (s *SmartContract) VerifyRecordHash(
    ctx contractapi.TransactionContextInterface,
    recordKey string,
    expectedHash string,
) (bool, error) {
    if strings.TrimSpace(recordKey) == "" {
        return false, fmt.Errorf("recordKey is required")
    }

    recordJSON, err := ctx.GetStub().GetState(recordKey)
    if err != nil {
        return false, fmt.Errorf("failed to read ledger: %v", err)
    }
    if recordJSON == nil {
        return false, nil
    }

    var record RecordHash
    if err := json.Unmarshal(recordJSON, &record); err != nil {
        return false, err
    }

    return strings.EqualFold(record.HashValue, strings.TrimSpace(expectedHash)), nil
}

// QueryRecordHash tra ve ban ghi hash day du theo recordKey.
func (s *SmartContract) QueryRecordHash(
    ctx contractapi.TransactionContextInterface,
    recordKey string,
) (*RecordHash, error) {
    if strings.TrimSpace(recordKey) == "" {
        return nil, fmt.Errorf("recordKey is required")
    }

    recordJSON, err := ctx.GetStub().GetState(recordKey)
    if err != nil {
        return nil, fmt.Errorf("failed to read ledger: %v", err)
    }
    if recordJSON == nil {
        return nil, fmt.Errorf("record not found")
    }

    var record RecordHash
    if err := json.Unmarshal(recordJSON, &record); err != nil {
        return nil, err
    }

    return &record, nil
}

// Backward compatibility cho ham cu (neu script cu dang goi).
func (s *SmartContract) CreateRecordHash(
    ctx contractapi.TransactionContextInterface,
    recordId string,
    hashValue string,
    timestamp string,
) error {
    if strings.TrimSpace(recordId) == "" {
        return fmt.Errorf("recordId is required")
    }

    record := RecordHash{
        RecordID:  recordId,
        HashValue: hashValue,
        Timestamp: timestamp,
    }

    recordJSON, err := json.Marshal(record)
    if err != nil {
        return err
    }

    return ctx.GetStub().PutState(recordId, recordJSON)
}

func main() {
    chaincode, err := contractapi.NewChaincode(&SmartContract{})
    if err != nil {
        panic(fmt.Sprintf("failed to create chaincode: %v", err))
    }

    if err := chaincode.Start(); err != nil {
        panic(fmt.Sprintf("failed to start chaincode: %v", err))
    }
}
