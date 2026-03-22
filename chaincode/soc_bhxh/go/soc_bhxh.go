package main

import (
    "encoding/json"
    "fmt"
    "github.com/hyperledger/fabric-contract-api-go/contractapi"
)

type SmartContract struct {
    contractapi.Contract
}

// Cấu trúc dữ liệu lưu trên sổ cái
type RecordHash struct {
    RecordID  string `json:"recordId"`
    HashValue string `json:"hashValue"`
    Timestamp string `json:"timestamp"`
}

// Ghi mã Hash lên Blockchain
func (s *SmartContract) CreateRecordHash(ctx contractapi.TransactionContextInterface, recordId string, hashValue string, timestamp string) error {
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

// Xác minh dữ liệu bằng cách so sánh mã Hash
func (s *SmartContract) VerifyRecordHash(ctx contractapi.TransactionContextInterface, recordId string) (*RecordHash, error) {
    recordJSON, err := ctx.GetStub().GetState(recordId)
    if err != nil {
        return nil, fmt.Errorf("Lỗi đọc Ledger: %v", err)
    }
    if recordJSON == nil {
        return nil, fmt.Errorf("Bản ghi không tồn tại")
    }
    var record RecordHash
    err = json.Unmarshal(recordJSON, &record)
    if err != nil {
        return nil, err
    }
    return &record, nil
}

func main() {
    chaincode, _ := contractapi.NewChaincode(&SmartContract{})
    chaincode.Start()
}