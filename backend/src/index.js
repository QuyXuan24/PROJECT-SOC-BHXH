const express = require('express');
const app = express();

app.get('/', (req, res) => {
    res.send('API SOC BHXH đang sẵn sàng!');
});

app.listen(5000, '0.0.0.0', () => {
    console.log('Backend đang lắng nghe tại cổng 5000');
});