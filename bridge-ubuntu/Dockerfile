# Sử dụng Node.js bản 18 (LTS) chạy trên nền Alpine Linux - nhẹ và nhanh
FROM node:18-alpine

# Tạo thư mục làm việc trong container
WORKDIR /usr/src/app

# Copy file cấu hình thư viện trước để tối ưu hóa cache Docker
COPY package*.json ./

# Cài đặt thư viện (Alpine lightweight)
RUN npm install

# Copy toàn bộ code nguồn vào container
COPY . .

# Mở cổng 3001 cho máy ảo
EXPOSE 3001

# Lệnh khởi chạy server
CMD [ "node", "server.js" ]