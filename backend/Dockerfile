# Sử dụng phiên bản Node.js ổn định
FROM node:20-alpine

# Tạo thư mục làm việc trong container
WORKDIR /usr/src/app

# Copy các file quản lý thư viện trước để tối ưu cache Docker
COPY package*.json ./

# Cài đặt thư viện
RUN npm install

# Copy toàn bộ mã nguồn backend vào container
COPY . .

# Mở cổng 5000 như đã cấu hình trong docker-compose
EXPOSE 5000

# Lệnh khởi chạy server
CMD ["npm", "start"]